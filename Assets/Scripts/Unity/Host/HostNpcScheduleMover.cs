using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Construction;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Npc;
using XianXia.Core.Navigation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

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
        readonly Dictionary<ulong, int> _lastPathIndex = new Dictionary<ulong, int>();
        readonly Dictionary<ulong, float> _lastRemainingDistance = new Dictionary<ulong, float>();
        readonly List<Vector3> _remainingPathScratch = new List<Vector3>(32);
        readonly List<Vector3> _bedPathScratch = new List<Vector3>(32);
        readonly List<WorldVec2> _bedAccessScratch = new List<WorldVec2>(4);
        readonly Dictionary<ulong, string> _lastTargetKey = new Dictionary<ulong, string>();
        // 目标几何缓存：只在 target 变／首次／interact spot 重新注册时才解析（禁止每帧 TryResolveWorldTarget）。
        readonly Dictionary<ulong, Vector3> _resolvedCenter = new Dictionary<ulong, Vector3>();
        readonly Dictionary<ulong, int> _resolvedSpotGeneration = new Dictionary<ulong, int>();
        readonly Dictionary<ulong, int> _gridRevisionAtPath = new Dictionary<ulong, int>();
        readonly List<PathRequest> _pathRequests = new List<PathRequest>(64);
        readonly List<PathRequest> _orderedPathRequests = new List<PathRequest>(64);
        readonly HashSet<ulong> _deferredPathRequestIds = new HashSet<ulong>();
        readonly HashSet<string> _pathUnavailableReported = new HashSet<string>(System.StringComparer.Ordinal);
        readonly Dictionary<ulong, uint> _issuedIntentRevision = new Dictionary<ulong, uint>();
        readonly Dictionary<ulong, uint> _issuedHostMovementRevision = new Dictionary<ulong, uint>();
        object _trackedWorld;

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
            public PathRequest(XianXia.Core.Domain.Ids.EntityId entity, string targetKey, bool targetChanged, uint revision)
            {
                Entity = entity;
                TargetKey = targetKey;
                TargetChanged = targetChanged;
                Revision = revision;
            }

            public XianXia.Core.Domain.Ids.EntityId Entity { get; }
            public string TargetKey { get; }
            public bool TargetChanged { get; }
            public uint Revision { get; }
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

        public bool TryGetResolvedPresentationTarget(EntityId entityId, out Vector3 target)
        {
            target = default;
            return !entityId.IsNone && _resolvedCenter.TryGetValue(entityId.Value, out target);
        }

        public float ResolveArrivalRadiusPresentation(MovementIntentComponent intent)
        {
            if (intent == null || !intent.HasWorldTarget || intent.ArrivalRadiusWorld <= 0f)
                return Mathf.Max(0f, arriveRadius);
            var surface = bootstrap?.ContinuousOutdoorSurfaceRuntime;
            if (surface == null || !surface.IsActive ||
                !string.Equals(surface.ActiveSurfaceId, intent.TargetSurfaceId, System.StringComparison.Ordinal))
                return Mathf.Max(0f, arriveRadius);
            surface.Mapper.WorldToPresentation(intent.TargetWorldX, intent.TargetWorldY, out var x0, out var y0);
            surface.Mapper.WorldToPresentation(intent.TargetWorldX + intent.ArrivalRadiusWorld,
                intent.TargetWorldY, out var x1, out var y1);
            return Mathf.Max(.01f, Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1)));
        }

        void Update()
        {
            using var timing = HostDebugHud.NpcScheduleUpdate.Auto();
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;
            if (bootstrap.Session.IsPaused)
                return;
            if (bootstrap.Session.World.ContentEvents.HasActive)
                return;
            if (moveController == null || viewSpawner == null)
                return;

            var session = bootstrap.Session;
            if (!ReferenceEquals(_trackedWorld, session.World))
            {
                _trackedWorld = session.World;
                ClearTrackingCaches();
            }
            var grid = moveController.WalkGrid;
            // Path computation 是 CPU presentation work，不是 Simulation progress：repath／stuck
            // 窗口用 real-time（unscaled），绝不除以 game speed（20x 会把冷却压到 0.15s）。
            var now = Time.unscaledTime;
            var repathInterval = Mathf.Max(MinRepathIntervalSeconds, repathIntervalSeconds);
            var stuckLimit = Mathf.Max(MinStuckSeconds, stuckSeconds);
            // Grid identity/generation matters as much as its mutation counter: a recomposed grid
            // can have the same number of blockers and therefore the same Revision value.
            var gridRevision = moveController.WalkGridGeneration;
            var spotGeneration = HostInteractSpots.LayoutGeneration;

            NpcPathRequestsThisFrame = 0;
            MovingNpcCount = 0;
            TickCounterWindow(now);
            _pathRequests.Clear();
            _orderedPathRequests.Clear();
            var farmLabor = bootstrap.GetComponent<HostFarmFieldLabor>();
            var destructibleAssault = bootstrap.GetComponent<HostDestructibleAssault>();

            foreach (var entity in session.World.Entities.All)
            {
                if ((entity.Tags & (EntityTag.Character | EntityTag.Npc)) == 0)
                    continue;
                // Release only the old schedule path before examining intent revisions.
                // Tactical pursuit now owns this body and must never be reconciled as work.
                if (CharacterEncounterService.OwnsParticipantSpatialState(session.World, entity.Id))
                {
                    CancelCivilianIntent(entity.Id);
                    continue;
                }
                if ((entity.Tags & EntityTag.Character) != 0 &&
                    !entity.TryGet<PersonalLifeComponent>(out _)) continue;
                if ((entity.Tags & EntityTag.Character) != 0 &&
                    session.World.Strategic.PlayerPartyContext?.IsMember(entity.Id) == true) continue;
                // A valid civilian logging session owns the tree approach and chopping loop.
                // Do not reconcile its old Core intent revision here: that would cancel the
                // newly-issued tree path and send the logger back to the work-area center.
                if (destructibleAssault != null &&
                    destructibleAssault.IsCivilianLoggingOwner(entity.Id))
                    continue;
                entity.TryGet<MovementIntentComponent>(out var intent);
                ReconcileIssuedIntent(entity.Id, intent);
                if (!LocalMapVisibility.IsEntityVisible(session.World, entity.Id))
                    continue;
                // 弥留／尸体不跑日程寻路
                if (!CombatLifeStateService.CanFight(entity) && !PrisonConfinementService.CanWalk(session.World, entity))
                    continue;
                MortalCivilianState civilian = null;
                var managedMortal = MortalCivilianQuery.IsManagedCivilian(session.World, entity) &&
                                    session.World.Civilians.TryGet(entity.Id, out civilian);
                var civilianFarmActivity = managedMortal &&
                    (civilian.Activity == MortalActivity.FarmerWork ||
                     civilian.Activity == MortalActivity.HerbFarmerWork);
                // Farm presentation owns managed mortals only while their current civilian
                // activity is farm work. Release stale farm ownership synchronously so this
                // frame can consume the new MovementIntent; never clear that Core intent here.
                if (managedMortal && !civilianFarmActivity && farmLabor != null &&
                    farmLabor.IsFarming(entity.Id))
                    farmLabor.StopNpcScheduleFarmOwnershipOnly(entity.Id);
                // Core and Host share one managed-mortal owner decision. A stationary singleton
                // squad is placement context and must not swallow civilian MovementIntent.
                if (managedMortal)
                {
                    if (!MortalCivilianMovementAuthority.CanCivilianOwnLocalMovement(
                            session.World, entity.Id, civilian))
                        continue;
                }
                else
                {
                    // Non-civilian encounter and squad ownership retains the existing gate.
                    if (ActualBattleParticipantQuery.TryFind(
                            session.World.Strategic.Participants, entity.Id, out _) ||
                        SquadCommandService.OwnsIndividualSchedule(session.World, entity.Id))
                        continue;
                }
                if (farmLabor != null && farmLabor.IsFarming(entity.Id))
                    continue;
                if (intent == null || !intent.Active)
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

                    if (intent.ArrivalRadiusWorld > 0f && !IsPersonApproachIntent(intent) &&
                        TryRetargetUnreachableBedAccess(session.World, entity.Id, intent, pos, rawCenter))
                        continue;

                    if (intent.ArrivalRadiusWorld > 0f && !IsPersonApproachIntent(intent) && grid != null &&
                        (!grid.TryWorldToCell(rawCenter.x, rawCenter.y, out var exactX, out var exactY) ||
                         !grid.IsWalkable(exactX, exactY)))
                    {
                        intent.MarkRetryablePathUnavailable("正式交付目标格不可走，不能执行精确到达");
                        _resolvedCenter[id] = rawCenter;
                        _resolvedSpotGeneration[id] = spotGeneration;
                        continue;
                    }
                    _resolvedCenter[id] = intent.ArrivalRadiusWorld > 0f ||
                        IsCultivationSlotTarget(session.World, intent) ||
                        IsGenericConstructionTarget(session.World, entity.Id, intent)
                        ? rawCenter : SnapGoalToWalkable(grid, rawCenter, goalSnapCells);
                    _resolvedSpotGeneration[id] = spotGeneration;
                }

                var center = _resolvedCenter[id];
                if (moveController.IsCharacterStandClear(entity.Id) &&
                    TryMarkArrivedWithinRadius(intent, pos, center,
                        ResolveStandingArrivalRadius(session.World, entity.Id, intent)))
                {
                    // A valid nearby slot is the stop, not a reason to keep walking to the
                    // shared center after Core has already accepted arrival.
                    if (_issuedIntentRevision.TryGetValue(id, out var arrivedRevision) &&
                        arrivedRevision == intent.Revision &&
                        _issuedHostMovementRevision.TryGetValue(id, out var arrivedHostRevision))
                        moveController.CancelPresentationMovementIfRevision(entity.Id, arrivedHostRevision);
                    _issuedIntentRevision.Remove(id);
                    _issuedHostMovementRevision.Remove(id);
                    bootstrap.ContinuousOutdoorSurfaceRuntime?.TryCaptureAtSiteAnchor(
                        entity.Id, view.transform.position);
                    _lastTargetKey.Remove(id);
                    _resolvedCenter.Remove(id);
                    _resolvedSpotGeneration.Remove(id);
                    _gridRevisionAtPath.Remove(id);
                    _nextRepathAt[id] = 0f;
                    continue;
                }

                if (intent.ArrivalRadiusWorld > 0f && intent.HostPathAccepted && !isMoving &&
                    _issuedIntentRevision.TryGetValue(id, out var issuedRevision) &&
                    issuedRevision == intent.Revision)
                {
                    intent.MarkRetryablePathUnavailable(
                        "Host 已完成精确路径，但最终站位仍在 Core 交付半径之外");
                    ReportPathUnavailableOnce(entity, intent, targetKey, pos, center);
                    continue;
                }

                if (moveController.IsNpcHeldForInteraction(entity.Id))
                    continue;

                TrackProgress(entity.Id, pos, now);
                var stuck = IsStuck(id, now, stuckLimit);
                var due = !_nextRepathAt.TryGetValue(id, out var t) || now >= t;
                var gridRevisionChanged = _gridRevisionAtPath.TryGetValue(id, out var settledRevision) &&
                                          settledRevision != gridRevision;
                var remainingPathInvalid = isMoving && !moveController.IsRemainingPathValid(entity.Id);
                if (remainingPathInvalid)
                {
                    moveController.CancelPresentationMovementPublic(entity.Id);
                    isMoving = false;
                }

                if (!NpcSchedulePathRequestPolicy.ShouldRequestPath(
                        targetChanged, stuck, isMoving, due,
                        gridRevisionChanged || remainingPathInvalid))
                    continue;

                if (targetChanged)
                    _lastTargetKey[id] = targetKey;
                _pathRequests.Add(new PathRequest(entity.Id, targetKey, targetChanged, intent.Revision));
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
                !entity.TryGet<MovementIntentComponent>(out var intent) || !intent.Active ||
                intent.Revision != request.Revision)
                return;
            if (!_resolvedCenter.TryGetValue(id, out var center))
                return;

            NpcPathRequestsThisFrame++;
            _pathRequestsInWindow++;
            if (!request.TargetChanged)
                _repathRequestsInWindow++;

            var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            var personApproach = IsPersonApproachIntent(intent);
            var cultivationSlot = IsCultivationSlotTarget(world, intent);
            var freeStandRadius = cultivationSlot ? 0f : personApproach
                ? ResolveStandingArrivalRadius(world, request.Entity, intent) * .8f
                : intent.ArrivalRadiusWorld > 0f
                    ? ResolveStandingArrivalRadius(world, request.Entity, intent) * .95f :
                    Mathf.Min(1.25f, ResolveStandingArrivalRadius(world, request.Entity, intent) * .8f);
            var standPoint = center;
            if (intent.ArrivalRadiusWorld > 0f && !personApproach &&
                !moveController.TryResolveFreeStandPoint(request.Entity, center, freeStandRadius, out standPoint))
            {
                intent.MarkRetryablePathUnavailable("目标到达范围内没有可达的空闲站位");
                _nextRepathAt[id] = now + repathInterval;
                ReportPathUnavailableOnce(entity, intent, request.TargetKey, view.transform.position, center);
                return;
            }
            var accepted = moveController.OrderEntityToWorldPoint(
                request.Entity, standPoint, null, issueStop: false,
                exactGoal: !personApproach && (cultivationSlot || intent.ArrivalRadiusWorld > 0f ||
                    IsGenericConstructionTarget(world, request.Entity, intent)),
                speedMultiplier: intent.SpeedMultiplier,
                freeStandRadius: personApproach || intent.ArrivalRadiusWorld <= 0f ? freeStandRadius : 0f);
            var elapsedMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - startedAt) *
                                    1000.0 / System.Diagnostics.Stopwatch.Frequency);
            LastNpcPathBuildMs = elapsedMs;
            if (elapsedMs > _maxPathBuildMsInWindow)
                _maxPathBuildMsInWindow = elapsedMs;

            _nextRepathAt[id] = now + repathInterval;
            _gridRevisionAtPath[id] = gridRevision;
            if (!accepted)
            {
                DescribeGridPoint(moveController != null ? moveController.WalkGrid : null,
                    view.transform.position, out var sourceInGrid, out var sourceWalkable);
                DescribeGridPoint(moveController != null ? moveController.WalkGrid : null,
                    center, out var targetInGrid, out var targetWalkable);
                var reason = !sourceInGrid || !sourceWalkable
                    ? "起点不在可走导航格"
                    : freeStandRadius > 0f
                        ? "目标附近没有可达的空闲站位"
                    : !targetInGrid || !targetWalkable
                        ? "目标不在可走导航格"
                        : "WalkGrid 起点与目标路径不连通";
                intent.MarkRetryablePathUnavailable(reason);
                ReportPathUnavailableOnce(entity, intent, request.TargetKey, view.transform.position, center);
                return;
            }

            intent.MarkPathRequested();
            _issuedIntentRevision[id] = intent.Revision;
            _issuedHostMovementRevision[id] = moveController.GetPresentationMovementRevision(request.Entity);
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
            (intent.HasWorldTarget ? intent.TargetSurfaceId + "@" +
             intent.TargetWorldX.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," +
             intent.TargetWorldY.ToString("R", System.Globalization.CultureInfo.InvariantCulture) : string.Empty) + "|" +
            (intent.DestinationKey ?? string.Empty) + "|" +
            intent.ArrivalRadiusWorld.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" +
            (intent.TargetWorkAreaId ?? string.Empty) + "|" +
            (intent.TargetLocationId ?? string.Empty) + "|" + intent.SlotIndex;

        static bool IsPersonApproachIntent(MovementIntentComponent intent) =>
            intent != null && (intent.DestinationKey == "huangcun:discipline" ||
                (intent.DestinationKey ?? string.Empty).StartsWith("rescue-target:", System.StringComparison.Ordinal));

        void SyncCarriedCharacters(XianXia.Core.Simulation.SimulationWorld world)
        {
            var surface = bootstrap?.ContinuousOutdoorSurfaceRuntime;
            if (surface == null || !surface.IsActive || viewSpawner?.Registry == null) return;
            var battle = world.Strategic.CharacterEncounter;
            if (battle != null && (battle.Phase == CharacterEncounterPhase.Active || battle.Phase == CharacterEncounterPhase.ReadyToEnd))
                MortalCivilianService.ReconcileEscortsForEncounter(world, battle);
            foreach (var pair in world.Civilians.All)
            {
                var state = pair.Value;
                var targetId = state.RescueCarrying ? state.RescueTargetId :
                    state.Disposition == CivilianDisposition.CapturedEscorted ? pair.Key : EntityId.None;
                if (!HostCharacterVisualContext.TryGetCarrier(world, targetId, out var carrierId) ||
                    !viewSpawner.Registry.TryGet(carrierId, out var carrier) || carrier == null ||
                    !viewSpawner.Registry.TryGet(targetId, out var target) || target == null) continue;
                // The transported body stays in front of the carrier in every facing.
                // This is only a View offset; WorldPresence remains at the carrier's feet.
                target.transform.position = HostCharacterVisualContext.CarriedPresentationPosition(carrier);
                target.SetCarriedPresentationOrder(HostArtDepth.Order(carrier.transform.position.y) + 1);
                surface.Mapper.PresentationToWorld(carrier.transform.position.x,
                    carrier.transform.position.y, out var wx, out var wy);
                var encounter = world.Strategic.CharacterEncounter;
                if (encounter != null && encounter.Find(carrierId.Value) != null &&
                    (encounter.Phase == CharacterEncounterPhase.Active ||
                     encounter.Phase == CharacterEncounterPhase.ReadyToEnd))
                {
                    var participant = encounter.Find(targetId.Value);
                    if (participant != null)
                    { participant.TacticalX = wx; participant.TacticalY = wy; }
                    var presence = world.WorldPresence.GetOrCreate(targetId);
                    presence.Mode = PartyWorldPresenceMode.InEncounter;
                    presence.SiteId = string.Empty;
                    presence.PersonalSurfaceId = encounter.SourceSurfaceId;
                    presence.WorldPosX = wx;
                    presence.WorldPosY = wy;
                    presence.HasContinuousWorldPosition = true;
                }
                else
                    world.WorldPresence.SetAtWorldPosition(targetId,
                        new XianXia.Core.World.WorldVec2(wx, wy), surface.ActiveSurfaceId);
            }
        }

        void LateUpdate()
        {
            using var timing = HostDebugHud.NpcLateUpdate.Auto();
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized ||
                bootstrap.Session.World == null)
                return;
            // Bind transported views only after all ordinary Update movement has completed, so a
            // carried character never becomes an independent obstacle one frame behind its carrier.
            SyncCarriedCharacters(bootstrap.Session.World);
        }

        void ReconcileIssuedIntent(EntityId entityId, MovementIntentComponent intent)
        {
            if (entityId.IsNone || !_issuedIntentRevision.TryGetValue(entityId.Value, out var issued)) return;
            if (intent != null && intent.Active && intent.Revision == issued) return;
            CancelCivilianIntent(entityId);
        }

        /// <summary>Cancels only a path previously accepted for a civilian MovementIntent.</summary>
        public void CancelCivilianIntent(EntityId entityId)
        {
            if (entityId.IsNone) return;
            if (_issuedIntentRevision.Remove(entityId.Value) &&
                _issuedHostMovementRevision.TryGetValue(entityId.Value, out var hostRevision))
                moveController?.CancelPresentationMovementIfRevision(entityId, hostRevision);
            _issuedHostMovementRevision.Remove(entityId.Value);
            ClearTracking(entityId.Value);
        }

        /// <summary>
        /// Transfers presentation-path ownership from the Core work-area intent to logging.
        /// Never clears or completes the Core intent itself.
        /// </summary>
        public void RelinquishCivilianIntentPathForLogging(EntityId entityId, bool cancelCurrentPath)
        {
            if (entityId.IsNone)
                return;
            var hadIssuedPath = _issuedIntentRevision.Remove(entityId.Value);
            if (cancelCurrentPath && hadIssuedPath &&
                _issuedHostMovementRevision.TryGetValue(entityId.Value, out var hostRevision))
                moveController?.CancelPresentationMovementIfRevision(entityId, hostRevision);
            _issuedHostMovementRevision.Remove(entityId.Value);
            ClearTracking(entityId.Value);
        }

        void ClearTracking(ulong id)
        {
            _lastTargetKey.Remove(id);
            _resolvedCenter.Remove(id);
            _resolvedSpotGeneration.Remove(id);
            _gridRevisionAtPath.Remove(id);
            _nextRepathAt.Remove(id);
            _lastPos.Remove(id);
            _lastProgressAt.Remove(id);
            _lastPathIndex.Remove(id);
            _lastRemainingDistance.Remove(id);
            _deferredPathRequestIds.Remove(id);
        }

        void ClearTrackingCaches()
        {
            _issuedIntentRevision.Clear();
            _issuedHostMovementRevision.Clear();
            _lastTargetKey.Clear();
            _resolvedCenter.Clear();
            _resolvedSpotGeneration.Clear();
            _gridRevisionAtPath.Clear();
            _nextRepathAt.Clear();
            _lastPos.Clear();
            _lastProgressAt.Clear();
            _lastPathIndex.Clear();
            _lastRemainingDistance.Clear();
            _deferredPathRequestIds.Clear();
            _pathUnavailableReported.Clear();
        }

        public static bool TryMarkArrivedWithinRadius(
            MovementIntentComponent intent, Vector3 current, Vector3 target, float radius)
        {
            if (intent == null || !intent.Active || radius < 0f ||
                (current - target).sqrMagnitude > radius * radius)
                return false;
            intent.HostArrived = true;
            return true;
        }

        float ResolveStandingArrivalRadius(
            XianXia.Core.Simulation.SimulationWorld world, EntityId id, MovementIntentComponent intent)
        {
            var radius = ResolveArrivalRadiusPresentation(intent);
            // A cushion is a specific work slot. A broad arrival disk can include the
            // doorway and let a scheduled cultivator stop there, blocking everyone else.
            if (IsCultivationSlotTarget(world, intent))
                return Mathf.Min(radius, .75f); // A 2x2 cushion can have its centre on a grid intersection.
            // Construction already checks a 3-world-unit radius in Core. Its generic
            // (zero-radius) intent must not inherit a larger presentation-only arrival disk.
            if (!IsGenericConstructionTarget(world, id, intent))
                return radius;
            var surface = bootstrap?.ContinuousOutdoorSurfaceRuntime;
            if (surface == null || !surface.IsActive)
                return 0f;
            surface.Mapper.WorldToPresentation(intent.TargetWorldX, intent.TargetWorldY, out var x0, out var y0);
            surface.Mapper.WorldToPresentation(intent.TargetWorldX + 3f, intent.TargetWorldY, out var x1, out var y1);
            return Mathf.Min(radius, Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1)));
        }

        static bool IsCultivationSlotTarget(
            XianXia.Core.Simulation.SimulationWorld world, MovementIntentComponent intent)
        {
            if (world == null || intent == null || intent.HasWorldTarget || intent.SlotIndex < 0 ||
                string.IsNullOrEmpty(intent.TargetWorkAreaId) ||
                !world.TryGetWorkArea(intent.TargetWorkAreaId, out var area) ||
                area.AllowedActivities == null)
                return false;
            for (var i = 0; i < area.AllowedActivities.Count; i++)
                if (string.Equals(area.AllowedActivities[i], "Cultivate", System.StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        static bool IsGenericConstructionTarget(
            XianXia.Core.Simulation.SimulationWorld world, EntityId id, MovementIntentComponent intent) =>
            intent != null && intent.HasWorldTarget && intent.ArrivalRadiusWorld <= 0f &&
            world != null && world.Civilians.TryGet(id, out var civilian) &&
            (civilian.Activity == MortalActivity.Construction || civilian.Activity == MortalActivity.Haul);

        void TrackProgress(EntityId entityId, Vector3 pos, float now)
        {
            var id = entityId.Value;
            var pathIndex = 0;
            var remaining = float.PositiveInfinity;
            if (moveController != null &&
                moveController.TryGetPathProgress(entityId, out pathIndex, out _) &&
                moveController.TryGetRemainingPath(entityId, _remainingPathScratch))
            {
                remaining = 0f;
                for (var i = 1; i < _remainingPathScratch.Count; i++)
                    remaining += Vector3.Distance(_remainingPathScratch[i - 1], _remainingPathScratch[i]);
            }

            var progressed = !_lastPathIndex.TryGetValue(id, out var previousIndex) ||
                             pathIndex > previousIndex ||
                             !_lastRemainingDistance.TryGetValue(id, out var previousRemaining) ||
                             remaining + stuckMoveEpsilon < previousRemaining;
            if (progressed)
            {
                _lastPathIndex[id] = pathIndex;
                _lastRemainingDistance[id] = remaining;
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

        bool TryRetargetUnreachableBedAccess(
            XianXia.Core.Simulation.SimulationWorld world,
            EntityId entityId,
            MovementIntentComponent intent,
            Vector3 current,
            Vector3 currentTarget)
        {
            if (world == null || intent == null || moveController == null ||
                bootstrap?.ContinuousOutdoorSurfaceRuntime == null ||
                moveController.TryBuildPathPreview(current, currentTarget, _bedPathScratch, exactGoal: true))
                return false;
            var key = intent.DestinationKey ?? string.Empty;
            const string sleepPrefix = "sleep-bed:";
            const string personalPrefix = "personal-bed:";
            const string schedulePrefix = "schedule-home-bed:";
            var bedId = key.StartsWith(sleepPrefix, System.StringComparison.Ordinal)
                ? key.Substring(sleepPrefix.Length) :
                key.StartsWith(personalPrefix, System.StringComparison.Ordinal)
                    ? key.Substring(personalPrefix.Length) :
                key.StartsWith(schedulePrefix, System.StringComparison.Ordinal)
                    ? key.Substring(schedulePrefix.Length) : key;
            if (!world.OutdoorConstructedAssets.Assets.TryGetValue(bedId, out var bed) ||
                !RoomBedGeometry.IsBed(bed))
                return false;

            RoomBedGeometry.CollectAccessPoints(
                world.OutdoorConstructedAssets, bed, _bedAccessScratch);
            var surface = bootstrap.ContinuousOutdoorSurfaceRuntime;
            for (var i = 0; i < _bedAccessScratch.Count; i++)
            {
                var candidate = _bedAccessScratch[i];
                if (Mathf.Abs(candidate.X - intent.TargetWorldX) < .0001f &&
                    Mathf.Abs(candidate.Y - intent.TargetWorldY) < .0001f)
                    continue;
                surface.Mapper.WorldToPresentation(candidate.X, candidate.Y, out var px, out var py);
                var presentation = new Vector3(px, py, HostPresentationSpace.EntityZ);
                if (!moveController.TryBuildPathPreview(
                        current, presentation, _bedPathScratch, exactGoal: true))
                    continue;
                intent.BeginWorldPoint(intent.TargetSurfaceId, candidate.X, candidate.Y,
                    intent.SpeedMultiplier, intent.DestinationKey, intent.ArrivalRadiusWorld);
                Debug.LogWarning("[NpcNavigation] Retargeted unreachable bed access. EntityId=" +
                                 entityId.Value + " BedId=" + bedId + " Candidate=" +
                                 candidate.X.ToString("0.###") + "," +
                                 candidate.Y.ToString("0.###"), this);
                return true;
            }
            return false;
        }

        bool TryResolveWorldTarget(
            XianXia.Core.Simulation.SimulationWorld world,
            MovementIntentComponent intent,
            out Vector3 worldCenter)
        {
            worldCenter = default;
            if (world == null || intent == null)
                return false;
            if (intent.HasWorldTarget)
            {
                var surface = bootstrap?.ContinuousOutdoorSurfaceRuntime;
                if (surface == null || !surface.IsActive ||
                    !string.Equals(surface.ActiveSurfaceId, intent.TargetSurfaceId, System.StringComparison.Ordinal))
                    return false;
                surface.Mapper.WorldToPresentation(intent.TargetWorldX, intent.TargetWorldY, out var px, out var py);
                worldCenter = new Vector3(px, py, HostPresentationSpace.EntityZ);
                return true;
            }

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
