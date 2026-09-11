using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Npc;
using XianXia.Core.Navigation;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Drives Host pathfinding from Core <see cref="MovementIntentComponent"/> (MoveAction).
    /// Shared WalkGrid with player RTS — no wall clipping. No hardcoded location ids.
    /// </summary>
    public sealed class HostNpcScheduleMover : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostMoveController moveController;
        [SerializeField] EntityViewSpawner viewSpawner;
        [Tooltip("受控 repath 冷却（real-time / unscaled 秒）；不随 game speed 缩放。")]
        [SerializeField] float repathIntervalSeconds = 1.25f;
        [SerializeField] float arriveRadius = 2.5f;
        [Tooltip("stuck 判定窗口（real-time / unscaled 秒）；不随 game speed 缩放。")]
        [SerializeField] float stuckSeconds = 4f;
        [SerializeField] float stuckMoveEpsilon = 0.15f;
        [SerializeField] int goalSnapCells = 6;
        [Tooltip("每帧最多执行的 NPC A* 请求数（摊平换班时的 CPU 峰值）。")]
        [SerializeField] int maxPathRequestsPerFrame = 2;

        // 冷却下限：path computation 是 CPU presentation work，不是 Simulation progress。
        const float MinRepathIntervalSeconds = 0.75f;
        const float MinStuckSeconds = 2f;

        readonly Dictionary<ulong, float> _nextRepathAt = new Dictionary<ulong, float>();
        readonly Dictionary<ulong, Vector3> _lastPos = new Dictionary<ulong, Vector3>();
        readonly Dictionary<ulong, float> _lastProgressAt = new Dictionary<ulong, float>();
        readonly Dictionary<ulong, string> _lastTargetKey = new Dictionary<ulong, string>();
        // 目标几何缓存：只在 target 变／首次／interact spot 重新注册时才解析（禁止每帧 TryResolveWorldTarget）。
        readonly Dictionary<ulong, Vector3> _resolvedCenter = new Dictionary<ulong, Vector3>();
        readonly Dictionary<ulong, int> _resolvedSpotGeneration = new Dictionary<ulong, int>();
        readonly Dictionary<ulong, int> _gridRevisionAtPath = new Dictionary<ulong, int>();
        readonly List<PathRequest> _pathRequests = new List<PathRequest>(64);
        readonly List<PathRequest> _orderedPathRequests = new List<PathRequest>(64);
        readonly HashSet<ulong> _deferredPathRequestIds = new HashSet<ulong>();
        readonly HashSet<string> _pathUnavailableReported = new HashSet<string>(System.StringComparer.Ordinal);

        /// <summary>本帧真正执行的 NPC A* 请求数（性能诊断）。</summary>
        public int NpcPathRequestsThisFrame { get; private set; }
        /// <summary>上一秒 NPC A* 请求数（性能诊断）。</summary>
        public int NpcPathRequestsLastSecond { get; private set; }
        /// <summary>上一秒非 target-change 引起的 repath 数（性能诊断）。</summary>
        public int NpcRepathRequestsLastSecond { get; private set; }
        /// <summary>最近一次 A* 构建耗时（ms）。</summary>
        public float LastNpcPathBuildMs { get; private set; }
        /// <summary>上一秒内单次 A* 最大耗时（ms）——判断卡顿是否 A* storm 的关键指标。</summary>
        public float MaxNpcPathBuildMsLastSecond { get; private set; }
        /// <summary>当前在移动的 NPC 数（性能诊断）。</summary>
        public int MovingNpcCount { get; private set; }

        float _counterWindowStart;
        int _pathRequestsInWindow;
        int _repathRequestsInWindow;
        float _maxPathBuildMsInWindow;

        readonly struct PathRequest
        {
            public PathRequest(XianXia.Core.Domain.Ids.EntityId entity, string targetKey, bool targetChanged)
            {
                Entity = entity;
                TargetKey = targetKey;
                TargetChanged = targetChanged;
            }

            public XianXia.Core.Domain.Ids.EntityId Entity { get; }
            public string TargetKey { get; }
            public bool TargetChanged { get; }
        }

        public void Bind(PlayableHostBootstrap host, HostMoveController move, EntityViewSpawner spawner)
        {
            bootstrap = host;
            moveController = move;
            viewSpawner = spawner;
        }

        /// <summary>Call when dialogue／menu releases an NPC so they repath immediately.</summary>
        public void NotifyNpcReleased(EntityId npc)
        {
            if (npc.IsNone)
                return;
            _nextRepathAt[npc.Value] = 0f;
            _lastProgressAt[npc.Value] = Time.unscaledTime;
            _deferredPathRequestIds.Add(npc.Value);
        }

        void Update()
        {
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;
            if (bootstrap.Session.IsPaused)
                return;
            if (bootstrap.Session.World.ContentEvents.HasActive)
                return;
            if (moveController == null || viewSpawner == null)
                return;

            var session = bootstrap.Session;
            var grid = moveController.WalkGrid;
            // Path computation 是 CPU presentation work，不是 Simulation progress：repath／stuck
            // 窗口用 real-time（unscaled），绝不除以 game speed（20x 会把冷却压到 0.15s）。
            var now = Time.unscaledTime;
            var repathInterval = Mathf.Max(MinRepathIntervalSeconds, repathIntervalSeconds);
            var stuckLimit = Mathf.Max(MinStuckSeconds, stuckSeconds);
            var gridRevision = grid != null ? grid.Revision : 0;
            var spotGeneration = HostInteractSpots.LayoutGeneration;

            NpcPathRequestsThisFrame = 0;
            MovingNpcCount = 0;
            TickCounterWindow(now);
            _pathRequests.Clear();
            _orderedPathRequests.Clear();

            foreach (var entity in session.World.Entities.All)
            {
                if ((entity.Tags & EntityTag.Character) != 0)
                    continue;
                if ((entity.Tags & EntityTag.Npc) == 0)
                    continue;
                if (!LocalMapVisibility.IsEntityVisible(session.World, entity.Id))
                    continue;
                // 弥留／尸体不跑日程寻路
                if (!CombatLifeStateService.CanFight(entity))
                    continue;
                if (!entity.TryGet<MovementIntentComponent>(out var intent) || !intent.Active)
                    continue;
                if (!viewSpawner.Registry.TryGet(entity.Id, out var view) || view == null)
                    continue;
                // §16：起点不在 CompositeWalkGrid 时不得启动日程寻路。materialize 会先用 baked
                // anchor 重定位；重定位前让该 NPC 保持不动，避免无限 retry A*。
                if (bootstrap.ContinuousOutdoorSurfaceRuntime != null &&
                    bootstrap.ContinuousOutdoorSurfaceRuntime.IsEntitySpawnPositionInvalid(entity.Id))
                    continue;

                var id = entity.Id.Value;
                var pos = view.transform.position;
                var isMoving = moveController.IsMoving(entity.Id);
                if (isMoving)
                    MovingNpcCount++;

                var targetKey = BuildTargetKey(intent);
                var targetChanged = !_lastTargetKey.TryGetValue(id, out var prev) ||
                                    !string.Equals(prev, targetKey, System.StringComparison.Ordinal);

                // 目标几何只在必要时解析：目标变／首次／interact spot 重新注册。
                // 正常移动中不再每帧 TryResolveWorldTarget + SnapGoalToWalkable。
                var geometryStale = targetChanged || !_resolvedCenter.ContainsKey(id) ||
                                    (_resolvedSpotGeneration.TryGetValue(id, out var cachedGen) &&
                                     cachedGen != spotGeneration);
                if (geometryStale)
                {
                    if (!TryResolveWorldTarget(session.World, intent, out var rawCenter))
                    {
                        // 解析失败（目标地点还没可用）：清缓存，与旧行为一致地下一帧重试。
                        _resolvedCenter.Remove(id);
                        _resolvedSpotGeneration.Remove(id);
                        continue;
                    }

                    _resolvedCenter[id] = SnapGoalToWalkable(grid, rawCenter, goalSnapCells);
                    _resolvedSpotGeneration[id] = spotGeneration;
                }

                var center = _resolvedCenter[id];
                if (TryMarkArrivedWithinRadius(intent, pos, center, arriveRadius))
                {
                    bootstrap.ContinuousOutdoorSurfaceRuntime?.TryCaptureAtSiteAnchor(
                        entity.Id, view.transform.position);
                    _lastTargetKey.Remove(id);
                    _resolvedCenter.Remove(id);
                    _resolvedSpotGeneration.Remove(id);
                    _gridRevisionAtPath.Remove(id);
                    _nextRepathAt[id] = 0f;
                    continue;
                }

                if (moveController.IsNpcHeldForInteraction(entity.Id))
                    continue;

                TrackProgress(id, pos, now);
                var stuck = IsStuck(id, now, stuckLimit);
                var due = !_nextRepathAt.TryGetValue(id, out var t) || now >= t;
                var gridRevisionChanged = _gridRevisionAtPath.TryGetValue(id, out var settledRevision) &&
                                          settledRevision != gridRevision;

                if (!NpcSchedulePathRequestPolicy.ShouldRequestPath(
                        targetChanged, stuck, isMoving, due, gridRevisionChanged))
                    continue;

                if (targetChanged)
                    _lastTargetKey[id] = targetKey;
                _pathRequests.Add(new PathRequest(entity.Id, targetKey, targetChanged));
            }

            // 上一帧被 budget 推迟的请求优先（防饥饿）；其余下一帧继续。
            for (var i = 0; i < _pathRequests.Count; i++)
                if (_deferredPathRequestIds.Contains(_pathRequests[i].Entity.Value))
                    _orderedPathRequests.Add(_pathRequests[i]);
            for (var i = 0; i < _pathRequests.Count; i++)
                if (!_deferredPathRequestIds.Contains(_pathRequests[i].Entity.Value))
                    _orderedPathRequests.Add(_pathRequests[i]);

            var budget = Mathf.Max(1, maxPathRequestsPerFrame);
            var executed = 0;
            for (var i = 0; i < _orderedPathRequests.Count && executed < budget; i++)
            {
                ExecutePathRequest(session.World, _orderedPathRequests[i], now, repathInterval, gridRevision);
                executed++;
            }

            _deferredPathRequestIds.Clear();
            for (var i = executed; i < _orderedPathRequests.Count; i++)
                _deferredPathRequestIds.Add(_orderedPathRequests[i].Entity.Value);
        }

        /// <summary>真正执行一次 NPC A*（按 per-frame budget 调用）。</summary>
        void ExecutePathRequest(
            XianXia.Core.Simulation.SimulationWorld world,
            PathRequest request,
            float now,
            float repathInterval,
            int gridRevision)
        {
            var id = request.Entity.Value;
            if (!viewSpawner.Registry.TryGet(request.Entity, out var view) || view == null)
                return;
            if (!world.Entities.TryGet(request.Entity, out var entity) ||
                !entity.TryGet<MovementIntentComponent>(out var intent) || !intent.Active)
                return;
            if (!_resolvedCenter.TryGetValue(id, out var center))
                return;

            NpcPathRequestsThisFrame++;
            _pathRequestsInWindow++;
            if (!request.TargetChanged)
                _repathRequestsInWindow++;

            var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            var accepted = moveController.OrderEntityToWorldPoint(
                request.Entity, center, null, issueStop: false);
            var elapsedMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - startedAt) *
                                    1000.0 / System.Diagnostics.Stopwatch.Frequency);
            LastNpcPathBuildMs = elapsedMs;
            if (elapsedMs > _maxPathBuildMsInWindow)
                _maxPathBuildMsInWindow = elapsedMs;

            _nextRepathAt[id] = now + repathInterval;
            _gridRevisionAtPath[id] = gridRevision;
            if (!accepted)
            {
                intent.MarkRetryablePathUnavailable();
                ReportPathUnavailableOnce(entity, intent, request.TargetKey, view.transform.position, center);
                return;
            }

            intent.MarkPathRequested();
            _lastProgressAt[id] = now;
            _lastPos[id] = view.transform.position;
        }

        void ReportPathUnavailableOnce(
            Entity entity,
            MovementIntentComponent intent,
            string targetKey,
            Vector3 current,
            Vector3 target)
        {
            var reportKey = entity.Id.Value + "|" + (targetKey ?? string.Empty);
            if (!_pathUnavailableReported.Add(reportKey))
                return;

            var displayName = entity.DefinitionId.ToString();
            if (entity.TryGet<IdentityComponent>(out var identity) && identity != null &&
                !string.IsNullOrWhiteSpace(identity.DisplayName))
                displayName = identity.DisplayName;
            DescribeGridPoint(moveController != null ? moveController.WalkGrid : null, current,
                out var sourceInGrid, out var sourceWalkable);
            DescribeGridPoint(moveController != null ? moveController.WalkGrid : null, target,
                out var targetInGrid, out var targetWalkable);
            Debug.LogWarning(
                "[NpcSchedulePathUnavailable] EntityId=" + entity.Id.Value +
                " DisplayName=" + displayName +
                " TargetWorkAreaId=" + (intent.TargetWorkAreaId ?? string.Empty) +
                " TargetLocationId=" + (intent.TargetLocationId ?? string.Empty) +
                " SlotIndex=" + intent.SlotIndex +
                " CurrentPresentation=(" + current.x.ToString("0.###") + "," +
                current.y.ToString("0.###") + ") SourceInGrid=" + sourceInGrid +
                " SourceWalkable=" + sourceWalkable +
                " TargetPresentation=(" + target.x.ToString("0.###") + "," +
                target.y.ToString("0.###") + ") TargetInGrid=" + targetInGrid +
                " TargetWalkable=" + targetWalkable,
                this);
        }

        static void DescribeGridPoint(
            WalkGrid grid,
            Vector3 point,
            out bool inGrid,
            out bool walkable)
        {
            inGrid = false;
            walkable = false;
            if (grid == null || !grid.TryWorldToCell(point.x, point.y, out var cellX, out var cellY))
                return;
            inGrid = true;
            walkable = grid.IsWalkable(cellX, cellY);
        }

        void TickCounterWindow(float now)
        {
            if (_counterWindowStart <= 0f)
                _counterWindowStart = now;
            if (now - _counterWindowStart < 1f)
                return;
            NpcPathRequestsLastSecond = _pathRequestsInWindow;
            NpcRepathRequestsLastSecond = _repathRequestsInWindow;
            MaxNpcPathBuildMsLastSecond = _maxPathBuildMsInWindow;
            _pathRequestsInWindow = 0;
            _repathRequestsInWindow = 0;
            _maxPathBuildMsInWindow = 0f;
            _counterWindowStart = now;
        }

        static string BuildTargetKey(MovementIntentComponent intent) =>
            (intent.TargetWorkAreaId ?? string.Empty) + "|" +
            (intent.TargetLocationId ?? string.Empty) + "|" + intent.SlotIndex;

        public static bool TryMarkArrivedWithinRadius(
            MovementIntentComponent intent, Vector3 current, Vector3 target, float radius)
        {
            if (intent == null || !intent.Active || radius < 0f ||
                (current - target).sqrMagnitude > radius * radius)
                return false;
            intent.HostArrived = true;
            return true;
        }

        void TrackProgress(ulong id, Vector3 pos, float now)
        {
            if (!_lastPos.TryGetValue(id, out var prev) ||
                (pos - prev).sqrMagnitude >= stuckMoveEpsilon * stuckMoveEpsilon)
            {
                _lastPos[id] = pos;
                _lastProgressAt[id] = now;
            }
        }

        bool IsStuck(ulong id, float now, float stuckLimit)
        {
            if (!_lastProgressAt.TryGetValue(id, out var t))
                return false;
            return now - t >= stuckLimit;
        }

        static Vector3 SnapGoalToWalkable(WalkGrid grid, Vector3 world, int snapRadius)
        {
            if (grid == null)
                return world;
            if (!grid.TryWorldToCell(world.x, world.y, out var cx, out var cy))
                return world;
            if (grid.IsWalkable(cx, cy))
                return world;
            if (!grid.TryFindNearestWalkable(cx, cy, snapRadius > 0 ? snapRadius : 8, out var nx, out var ny))
                return world;
            grid.CellToWorldCenter(nx, ny, out var wx, out var wy);
            return new Vector3(wx, wy, HostPresentationSpace.EntityZ);
        }

        static bool TryResolveWorldTarget(
            XianXia.Core.Simulation.SimulationWorld world,
            MovementIntentComponent intent,
            out Vector3 worldCenter)
        {
            worldCenter = default;
            if (world == null || intent == null)
                return false;

            float ox = 0f, oz = 0f;
            string locationId = intent.TargetLocationId;
            if (!string.IsNullOrEmpty(intent.TargetWorkAreaId) &&
                world.TryGetWorkArea(intent.TargetWorkAreaId, out var area))
            {
                if (!string.IsNullOrEmpty(area.LocationId))
                    locationId = area.LocationId;
                ox = area.OffsetX;
                oz = area.OffsetZ;
            }

            if (string.IsNullOrEmpty(locationId))
                return false;
            if (!HostZoneQuery.TryGetLocationCenter(world, locationId, out var center))
                return false;

            // Soft slot → interact spot (农田多点) or ring around area offset.
            if (intent.SlotIndex >= 0)
            {
                var kind = HostInteractSpotKind.Work;
                if (!string.IsNullOrEmpty(intent.TargetWorkAreaId) &&
                    world.TryGetWorkArea(intent.TargetWorkAreaId, out var slotArea) &&
                    slotArea.AllowedActivities != null)
                {
                    for (var i = 0; i < slotArea.AllowedActivities.Count; i++)
                    {
                        if (string.Equals(slotArea.AllowedActivities[i], "Cultivate", System.StringComparison.OrdinalIgnoreCase))
                        {
                            kind = HostInteractSpotKind.Cultivate;
                            break;
                        }
                    }
                }

                if (HostInteractSpots.TryGetSlotSpot(
                        locationId, kind, intent.SlotIndex, out var spot, world))
                {
                    worldCenter = spot.WorldPosition;
                    worldCenter.z = HostPresentationSpace.EntityZ;
                    return true;
                }

                worldCenter = center + new Vector3(ox, oz, 0f) + HostInteractSpots.RingOffset(intent.SlotIndex);
                worldCenter.z = HostPresentationSpace.EntityZ;
                return true;
            }

            // Presentation offset from content → world (XY = presentation X/Z).
            worldCenter = center + new Vector3(ox, oz, 0f);
            worldCenter.z = HostPresentationSpace.EntityZ;
            return true;
        }
    }
}
