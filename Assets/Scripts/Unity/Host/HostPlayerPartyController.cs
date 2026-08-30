using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Actions;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Input;
using XianXia.Core.Persistence;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>Active character camera follow policy (final Phase 1 rule).</summary>
    public enum HostActiveCameraFollowMode
    {
        /// <summary>Free look; middle-mouse pan owns the camera. RTS path never engages follow.</summary>
        Free = 0,
        /// <summary>WASD Direct Movement only: snap + continuous Hard Follow.</summary>
        WasdHardFollow = 1
    }

    /// <summary>
    /// Phase 1: PlayerParty follow AI, Active WASD, camera Hard Follow on WASD only.
    /// RTS / click path movement is fully decoupled from Camera.
    /// </summary>
    public sealed class HostPlayerPartyController : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] float followStopDistance = 2.2f;
        [SerializeField] float followRepathInterval = 0.35f;
        [SerializeField] float followerSpreadRadius = 1.1f;
        [SerializeField] float wasdMoveSpeed = 5.5f;
        [SerializeField] bool enableCameraFollow = true;
        [SerializeField] float localVisibleAutoTravelFollowLerp = 5f;

        int _autoTravelLegSegmentIndex = -1;
        Vector3 _lastAutoTravelTarget;
        // Back-off after a failed drive attempt (no Exit / A* blocked): avoid per-frame re-path.
        float _autoTravelRetryCooldownUntil;
        // Phase 5C-W1 (3rd pass): rising-edge of ExecutionMode -> LocalVisible marks a fresh
        // takeover; every takeover re-arms the CURRENT leg (independent of WorldMap open/close).
        bool _localVisibleTakeoverActive;
        // Phase 5C-W2: driving toward the final Wilderness LocalMap safe interior before
        // FinishArrival (no instant arrival at the hex edge).
        bool _finalArrivalApproachInProgress;

        // Phase 5C-W2 diagnostics: last LocalVisible AutoTravel transition outcome.
        // Read by HostHudSnapshot (Runtime Diagnostics) so LevelTester can see the real failure.
        public static string LastTransitionStatus = "Idle";
        public static string LastTransitionFailureReason = string.Empty;
        public static string LastExitSourceHex = "-";
        public static string LastExitDestinationHex = "-";
        public static string LastExitSlotRect = "-";
        public static bool LastActiveInsideExitSlot;

        // Phase 5C-W1 (2nd pass): middle-mouse pan detaches camera until a NEW AutoTravel session.
        bool _cameraDetachedByPlayer;
        bool _hasAutoTravelSession;

        readonly Dictionary<ulong, float> _nextFollowRepath = new Dictionary<ulong, float>();
        readonly Dictionary<ulong, HostPartySharedActivity> _followerSharedActivity =
            new Dictionary<ulong, HostPartySharedActivity>();
        HostPartySharedActivity _lastActiveSharedActivity = HostPartySharedActivity.FollowIdle;
        bool _wasdHeldLastFrame;
        bool _pendingSnapshotFollowRebind;
        HostActiveCameraFollowMode _cameraMode = HostActiveCameraFollowMode.Free;

        // Phase 5R-B4: transient Site LocalVisible→Canonical sync state（不落盘、不是 Position truth）。
        // _siteSyncHeld：Materialize 完成帧标记 —— OnLocalMapMaterialized 置 true，下一次 sync tick
        // 只清标记不反写（ownership transition：materialize 完成帧禁止同帧 Local→Canonical）。
        bool _siteSyncHeld;
        string _siteSyncCacheSiteId = string.Empty;
        string _siteSyncCacheMapId = string.Empty;
        WorldSiteSpatialMapping.WorldSiteLocalMapBounds _siteSyncCacheBounds;
        HexFootprintSpatialGeometry _siteSyncCacheGeometry;
        string _siteSyncLastFailureKind = string.Empty;
        float _siteSyncLastFailureTime = -10f;

        // Phase 5R-B6.1：WorldSite→Wilderness 正式 egress 后的一次性 recenter 请求。
        // egress 成功分支置 true；下一次 OnLocalMapMaterialized（materialize + 实体重建完成后）
        // 消费 → SnapCameraToActiveOnce。普通 WorldMap open/close 不设此标志，自由镜头语义不受影响。
        bool _pendingEgressRecenter;
        // Phase 5R-B6.2：egress 时所在 LocalMapId —— 消费时要求 LocalMap 已切换（egress 必然换图），
        // 防止标志泄漏到后续无关的同图 materialize（普通 WorldMap 开关）而误 recenter。
        string _pendingEgressRecenterMapId = string.Empty;

        [SerializeField] float followerChopSearchRadius = 10f;

        HostMoveController _move;
        HostCommandBridge _commands;
        HostNpcMeleeAssault _melee;
        HostFarmFieldLabor _farm;
        HostDestructibleAssault _chop;
        HostWorkLoop _workLoop;
        PlayableHostCameraRig _cameraRig;
        EntityViewSpawner _spawner;

        public PlayerPartyRuntime Party =>
            bootstrap != null && bootstrap.Session != null
                ? bootstrap.Session.PlayerParty
                : null;

        public EntityId ActiveCharacterId =>
            Party != null ? Party.ActiveCharacterId : EntityId.None;

        public void Bind(PlayableHostBootstrap host)
        {
            bootstrap = host;
            if (host == null)
                return;

            _move = host.MoveController;
            _commands = host.CommandBridge;
            _melee = host.GetComponent<HostNpcMeleeAssault>();
            _farm = host.GetComponent<HostFarmFieldLabor>();
            _chop = host.GetComponent<HostDestructibleAssault>();
            _workLoop = host.GetComponent<HostWorkLoop>();
            _cameraRig = host.GetComponent<PlayableHostCameraRig>();
            _spawner = host.ViewSpawner;
        }

        public bool TryFollowActive(EntityId candidate, out string error)
        {
            error = null;
            var session = bootstrap?.Session;
            if (session == null || !session.IsInitialized || Party == null)
            {
                error = "Session not ready.";
                return false;
            }

            if (!Party.TryAddMember(session.World, session.CharacterIds, candidate, out error))
                return false;

            BackgroundCharacterTravelService.CancelTravelIfAny(session.World, candidate);
            session.World.LocalMap.AddOccupant(candidate);
            _nextFollowRepath.Remove(candidate.Value);
            OrderFollowerTowardActive(candidate);
            return true;
        }

        public bool TryStopFollow(EntityId id, out string error)
        {
            error = null;
            if (Party == null || !Party.TryRemoveMember(id, out error))
                return false;

            _nextFollowRepath.Remove(id.Value);
            _followerSharedActivity.Remove(id.Value);
            StopFollowerPartyDerivedWork(id);
            StopFollowerDirectControl(id);
            return true;
        }

        public bool TrySwitchActive(EntityId newActive, out string error)
        {
            error = null;
            var session = bootstrap?.Session;
            if (session == null || Party == null)
            {
                error = "Session not ready.";
                return false;
            }

            var oldActive = Party.ActiveCharacterId;
            if (oldActive == newActive)
                return true;

            if (!Party.TrySetActive(session.World, newActive, out error))
                return false;

            ClearDirectControlFor(oldActive);
            _cameraMode = HostActiveCameraFollowMode.Free;
            // One-shot focus on new Active; does not enter permanent follow.
            FrameCameraOn(newActive);
            if (bootstrap?.SelectionController != null)
                bootstrap.SelectionController.SelectEntity(newActive, false);
            return true;
        }

        /// <summary>
        /// Snapshot Load / Active switch：一次性对准 Active Presentation，不进入 WASD Hard Follow。
        /// </summary>
        public void SnapCameraToActiveOnce()
        {
            if (Party == null || !Party.HasActive)
                return;

            _cameraMode = HostActiveCameraFollowMode.Free;
            SnapCameraTo(Party.ActiveCharacterId);
        }

        /// <summary>
        /// PlayerParty LocalMap materialize / transition: invalidate old-map locomotion and restore follow.
        /// </summary>
        public void OnLocalMapMaterialized(string localMapId)
        {
            if (Party == null)
                return;

            // Phase 5R-B4: Materialize 完成 → 本帧禁止 Local→Canonical 反写（ownership transition）；
            // 同时失效 V2 geometry cache（Site/LocalMap 可能已变，下一次 sync tick 重建）。
            _siteSyncHeld = true;
            _siteSyncCacheGeometry = null;
            _siteSyncCacheSiteId = string.Empty;
            _siteSyncCacheMapId = string.Empty;

            var mapId = localMapId?.Trim() ?? string.Empty;
            _move?.BindLocalMapContext(mapId);
            _move?.InvalidatePartyLocalMovement(Party.Members);
            InvalidatePartyDerivedLocalActions();
            ResetFollowAfterMaterialize();

            // Phase 5R-B6.1：WorldSite→Wilderness egress 的 materialize 完成点（entity 重建后、
            // Active Character final transform 已确定）→ one-shot 对准主控。
            // 不进入永久 Follow（SnapCameraToActiveOnce 置 Free + 一次性对准）。
            // Phase 5R-B6.2：消费要求"已换图 + 已 AtWorldPosition"（egress 必然换图）；同图
            // materialize（普通 WorldMap 开关 / materialize 失败后的残留）仅清理标志，不 recenter，
            // 防泄漏到无关的后续 materialize。
            if (_pendingEgressRecenter)
            {
                var pendingMapId = _pendingEgressRecenterMapId ?? string.Empty;
                _pendingEgressRecenter = false;
                _pendingEgressRecenterMapId = string.Empty;
                var egressMotion = bootstrap?.Session?.World?.PlayerPartyTravel;
                var isEgressCompletion =
                    !string.IsNullOrEmpty(localMapId) &&
                    !string.Equals(localMapId, pendingMapId, System.StringComparison.OrdinalIgnoreCase) &&
                    egressMotion != null &&
                    egressMotion.LocationKind == PlayerPartyLocationKind.AtWorldPosition;
                if (isEgressCompletion)
                    SnapCameraToActiveOnce();
            }
        }

        void InvalidatePartyDerivedLocalActions()
        {
            _lastActiveSharedActivity = HostPartySharedActivity.FollowIdle;
            ClearFollowerPartyDerivedWork();
        }

        void ResetFollowAfterMaterialize()
        {
            _nextFollowRepath.Clear();
            if (Party == null || _spawner == null)
                return;

            if (LoadedLocalMapPlacementSnapshotRestore.DeferFollowRebind)
            {
                _pendingSnapshotFollowRebind = true;
                return;
            }

            RebindAllFollowers();
        }

        void RebindAllFollowers()
        {
            if (Party == null || _spawner == null)
                return;

            for (var i = 0; i < Party.Members.Count; i++)
            {
                var id = Party.Members[i];
                if (Party.IsActive(id))
                    continue;
                OrderFollowerTowardActive(id);
            }
        }

        public void ClearDirectControlFor(EntityId id)
        {
            if (id.IsNone)
                return;

            _move?.CancelPresentationMovementPublic(id);
            _workLoop?.StopLoop(id);
            _farm?.Stop(id);
            _melee?.DisengageIfAttacker(id);
            if (_commands != null && bootstrap?.Session != null)
                _commands.IssueOne(id, PlayerCommandKind.Stop, 0);
        }

        void Update()
        {
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized || Party == null)
                return;
            if (Party.IsAwaitingSuccession)
                return;

            Party.RefreshActiveAfterLifeState(bootstrap.Session.World);
            if (Party.IsAwaitingSuccession || Party.ActiveCharacterId.IsNone)
                return;

            if (_pendingSnapshotFollowRebind)
            {
                _pendingSnapshotFollowRebind = false;
                RebindAllFollowers();
            }

            TickWasdForActive();
            TickPartyDerivedGroupActivity();
            TickFollowers();
            TickCombatFollow();
            TickWildernessWorldSyncAndEdge();
            TickLocalVisibleAutoTravelMovement();
        }

        void TickWasdForActive()
        {
            if (HostInputGate.BlockWorldInteraction)
                return;

            var active = Party.ActiveCharacterId;
            if (active.IsNone || _move == null)
                return;

            if (!bootstrap.Session.World.Entities.TryGet(active, out var ent) ||
                !CombatLifeStateService.CanFight(ent))
                return;

            var dir = ReadWasdDirection();
            var wasdHeld = dir.sqrMagnitude > 0.01f;
            if (wasdHeld)
            {
                if (!_wasdHeldLastFrame)
                {
                    // Phase 5C-W1: WASD interrupt cancels LocalVisible AutoTravel (keep position).
                    CancelLocalVisibleAutoTravelForPlayerInterrupt();
                    // Cancel RTS/Click path; Camera snaps + Hard Follow (CAMERA-E).
                    _move.CancelPresentationMovementPublic(active);
                    EnterWasdHardFollow(active);
                }

                // Crossing Intent 必须在 WalkGrid Clamp 之前处理，否则永远无法 OutOfBounds。
                if (TryConsumeSurfaceEdgeCrossingFromMovement(active, dir, wasdMoveSpeed))
                {
                    _wasdHeldLastFrame = true;
                    return;
                }

                _move.TickDirectWasdMove(active, dir, wasdMoveSpeed);
            }
            else if (_wasdHeldLastFrame && _cameraMode == HostActiveCameraFollowMode.WasdHardFollow)
            {
                _cameraMode = HostActiveCameraFollowMode.Free;
            }

            _wasdHeldLastFrame = wasdHeld;
        }

        /// <summary>
        /// WASD 本帧位移若试图跨出 LocalMap playable bounds，先 Resolve Neighbor 再 Transition。
        /// 邻格非法时返回 false，交由 WalkGrid Clamp 挡住。
        /// </summary>
        bool TryConsumeSurfaceEdgeCrossingFromMovement(EntityId active, Vector2 dir, float speed)
        {
            var session = bootstrap?.Session;
            var world = session?.World;
            var party = Party;
            if (world == null || party == null || active.IsNone)
                return false;
            if (!PlayerPartyWildernessTransitionService.IsSurfaceHexEdgeTransitionEnabled(world))
                return false;

            var gate = world.PlayerPartyTravel?.SurfaceEdgeGate;
            if (gate != null && !gate.CanAttemptEdgeTransition)
                return false;

            if (_spawner == null ||
                !_spawner.Registry.TryGet(active, out var view) ||
                view == null)
                return false;
            if (!TryResolveWildernessBounds(out var bounds))
                return false;

            var depth = SurfaceExitZoneCalculator.ResolveDepthFromSession(world, bounds);
            SyncExitTriggerDepthToSession(world, depth);

            var pos = view.transform.position;
            var dt = Time.deltaTime;
            if (dt <= 0f)
                dt = Time.unscaledDeltaTime;
            var proposedX = pos.x + dir.x * speed * dt;
            var proposedY = pos.y + dir.y * speed * dt;

            // Canonical Exit Trigger：Zone 内 + 向外；进入 Zone 本身不触发。
            if (!WildernessLocalWorldProjection.TryResolveExitTriggerConnection(
                    world, pos.x, pos.y, proposedX, proposedY, bounds, depth, out var edgeConnection))
            {
                // WalkGrid 出界但尚无正式 intent：用上一帧 Local（若有）再判一次。
                var grid = _move != null ? _move.WalkGrid : null;
                var leavesWalkGrid = grid != null &&
                                     !grid.TryWorldToCell(proposedX, proposedY, out _, out _);
                if (!leavesWalkGrid)
                    return false;
                if (!WildernessLocalWorldProjection.TryResolveExitTriggerConnection(
                        world, pos.x, pos.y, proposedX, proposedY, bounds, depth, out edgeConnection))
                    return false;
            }

            var cross = PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                world, party, edgeConnection);
            if (!cross.IsSuccess)
                return false;

            bootstrap.ExpandLocalMapForCurrentPartyWorld(closeWorldMap: false);
            EnsureEdgeGateCompletedAfterExpand(world);
            return true;
        }

        void TickWildernessWorldSyncAndEdge()
        {
            if (HostInputGate.BlockWorldInteraction)
                return;
            if (bootstrap?.WorldMapPanel != null && bootstrap.WorldMapPanel.IsOpen)
                return;

            var session = bootstrap.Session;
            var world = session.World;
            var party = Party;
            if (world?.PlayerPartyTravel == null || party == null)
                return;

            var motion = world.PlayerPartyTravel;
            if (!motion.HasPosition)
                return;

            // Phase 5C-W1: LocalVisible AutoTravel in Wilderness keeps Host sync + edge enabled;
            // normal moving (World execution) still early-returns (World Advance drives position).
            var localVisibleAutoTravel =
                PlayerPartyLocalVisibleAutoTravelService.IsActiveLocalVisibleAutoTravel(motion);
            if (motion.IsMoving && !localVisibleAutoTravel)
                return;
            if (localVisibleAutoTravel && motion.LocationKind != PlayerPartyLocationKind.AtWorldPosition)
                return; // WorldSite LocalVisible: keep Phase 5B (stand still, no Site Egress logic).
            if (!PlayerPartyWildernessTransitionService.IsSurfaceHexEdgeTransitionEnabled(world))
                return;

            if (_spawner == null ||
                !_spawner.Registry.TryGet(party.ActiveCharacterId, out var activeView) ||
                activeView == null)
                return;

            var pos = activeView.transform.position;
            var localX = pos.x;
            var localY = pos.y;
            if (!TryResolveWildernessBounds(out var bounds))
                return;

            var depth = SurfaceExitZoneCalculator.ResolveDepthFromSession(world, bounds);
            SyncExitTriggerDepthToSession(world, depth);

            var gate = motion.SurfaceEdgeGate;
            var prevX = localX;
            var prevY = localY;
            var hasPrev = gate != null && gate.HasLastLocal;
            if (hasPrev)
            {
                prevX = gate.LastLocalX;
                prevY = gate.LastLocalY;
            }

            gate?.TickRearm(localX, localY, bounds);

            // TransitionInProgress 或 Disarmed：禁止任何跨边（仍更新 LastLocal 供下帧）。
            if (gate != null && !gate.CanAttemptEdgeTransition)
            {
                gate.NoteLocalPosition(localX, localY);
                return;
            }

            if (motion.LocationKind == PlayerPartyLocationKind.AtWorldSite)
            {
                if (hasPrev &&
                    WildernessLocalWorldProjection.TryResolveExitTriggerConnection(
                        world, prevX, prevY, localX, localY, bounds, depth, out var siteConnection))
                {
                    var exit = PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                        world, party, siteConnection);
                    if (exit.IsSuccess)
                    {
                        bootstrap.ExpandLocalMapForCurrentPartyWorld(closeWorldMap: false);
                        EnsureEdgeGateCompletedAfterExpand(world);
                        return;
                    }
                }

                gate?.NoteLocalPosition(localX, localY);
                return;
            }

            if (motion.LocationKind != PlayerPartyLocationKind.AtWorldPosition)
            {
                gate?.NoteLocalPosition(localX, localY);
                return;
            }

            PlayerPartyWildernessTransitionService.TrySyncLocalMovementToWorldPosition(
                world, localX, localY, bounds);

            // Phase 5C-W2 修复 2：LocalVisible AutoTravel 时唯一 Executor 是
            // TickLocalVisibleAutoTravelMovement（TravelPlan official NextHex → resolved
            // connection → TryAttemptSurfaceEdgeTransition）。这里保留 Local→World sync /
            // SurfaceEdgeGate.TickRearm / NoteLocalPosition，但禁止 Generic Edge Detector
            // 自行选出口触发 Transition，以免物理 Trigger 选了另一个 Exit →
            // "Exit destination is not the active NextHex"。
            if (localVisibleAutoTravel)
            {
                gate?.NoteLocalPosition(localX, localY);
                return;
            }

            if (hasPrev &&
                WildernessLocalWorldProjection.TryResolveExitTriggerConnection(
                    world, prevX, prevY, localX, localY, bounds, depth, out var connection))
            {
                var cross = PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                    world, party, connection);
                if (cross.IsSuccess)
                {
                    bootstrap.ExpandLocalMapForCurrentPartyWorld(closeWorldMap: false);
                    EnsureEdgeGateCompletedAfterExpand(world);
                    return;
                }
            }

            gate?.NoteLocalPosition(localX, localY);
        }

        static void SyncExitTriggerDepthToSession(
            XianXia.Core.Simulation.SimulationWorld world,
            float depth)
        {
            if (world?.LocalMap == null)
                return;
            if (world.LocalMap.ExitTriggerDepth > 0.0001f)
                return;
            world.LocalMap.ExitTriggerDepth = depth;
        }

        void EnsureEdgeGateCompletedAfterExpand(XianXia.Core.Simulation.SimulationWorld world)
        {
            var gate = world?.PlayerPartyTravel?.SurfaceEdgeGate;
            if (gate == null || !gate.TransitionInProgress)
                return;
            if (!TryResolveWildernessBounds(out var bounds))
                return;

            float sx = bounds.CenterX;
            float sy = bounds.CenterY;
            var party = Party;
            if (party != null &&
                party.HasActive &&
                _spawner != null &&
                _spawner.Registry.TryGet(party.ActiveCharacterId, out var view) &&
                view != null)
            {
                sx = view.transform.position.x;
                sy = view.transform.position.y;
            }

            PlayerPartyWildernessTransitionService.CompleteEdgeTransitionPresentation(
                world, bounds, sx, sy);
        }

        bool TryResolveWildernessBounds(
            out WildernessLocalWorldProjection.WildernessLocalMapBounds bounds)
        {
            bounds = default;
            // 真源优先：与 HostSurfaceExitZonePresenter 同一 MapLayout 解析，保证 Debug 方块 /
            // AutoTravel 到达判定 / materialize 使用同一 bounds（WalkGrid 由同一 layout 构建，
            // 但 layout 显式解析更稳，避免依赖 WalkGrid 时序）。
            var session = bootstrap?.Session;
            var mapId = session?.World?.LocalMap?.ActiveMapLayoutId?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(mapId) && session?.Registry != null)
            {
                var parsed = DefinitionId.Parse(mapId.Trim());
                if (parsed.IsSuccess &&
                    session.Registry.TryGetMapLayout(parsed.Value, out var layout) &&
                    layout != null &&
                    layout.Width > 0 &&
                    layout.Height > 0)
                {
                    var cs = layout.CellSize > 0.0001f ? layout.CellSize : 1f;
                    bounds = WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(
                        layout.OriginX, layout.OriginY, cs, layout.Width, layout.Height);
                    return true;
                }
            }

            var grid = bootstrap != null ? bootstrap.MoveController?.WalkGrid : null;
            if (grid == null)
            {
                bounds = WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(
                    -20f, -20f, 1f, 40, 40);
                return true;
            }

            bounds = WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(
                grid.OriginX, grid.OriginY, grid.CellSize, grid.Width, grid.Height);
            return true;
        }

        void LateUpdate()
        {
            // After CameraRig middle-pan so WASD Hard Follow wins the same frame.
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized || Party == null)
                return;
            if (Party.IsAwaitingSuccession || Party.ActiveCharacterId.IsNone)
                return;

            // Phase 5R-B4: 本帧所有 Local 移动 writer（WASD / RTS / AutoTravel / SnapWalkable）都已在
            // Update 阶段结束，Active Transform 已最终确定 → 在此做 LocalVisible→Canonical sync。
            TickWorldSiteCanonicalSync();
            TickCameraFollow();
        }

        /// <summary>
        /// Phase 5R-B4：WorldSite LocalVisible → Canonical 单向同步（唯一 Local→Canonical writer）。
        /// 时机：LateUpdate（最终 Local Transform 已确定之后）。
        /// 单向 ownership：仅 WorldMap closed + AtWorldSite + LocalMap Materialized（held=false）+
        /// Active View 有效 + 非 departure/transition 时 Local→Canonical；
        /// WorldMap OPEN / Materialize 完成帧 / departure → 禁止（保留旧 Canonical）。
        /// </summary>
        void TickWorldSiteCanonicalSync()
        {
            // Materialize 完成帧（OnLocalMapMaterialized 已置 held）：本帧不反写，下一帧 ownership 接管。
            if (_siteSyncHeld)
            {
                _siteSyncHeld = false;
                return;
            }

            if (HostInputGate.BlockWorldInteraction)
                return;
            var session = bootstrap?.Session;
            var world = session?.World;
            var motion = world?.PlayerPartyTravel;
            if (world == null || motion == null || !motion.HasPosition)
                return;

            var active = Party.ActiveCharacterId;
            if (active.IsNone || _spawner == null ||
                !_spawner.Registry.TryGet(active, out var view) ||
                view == null)
                return;

            if (!TryResolveSiteSyncGeometry(world, motion, out var bounds, out var geometry))
            {
                LogSiteSyncFailureThrottled("GeometryUnavailable", motion.SiteId);
                return;
            }

            var isWorldMapOpen = bootstrap.WorldMapPanel != null && bootstrap.WorldMapPanel.IsOpen;
            var ctx = new WorldSiteLocalVisibleSyncContext(
                inputBlocked: HostInputGate.BlockWorldInteraction,
                isWorldMapOpen: isWorldMapOpen,
                hasActiveView: true,
                isAtWorldSite: motion.LocationKind == PlayerPartyLocationKind.AtWorldSite,
                hasSiteId: !string.IsNullOrEmpty(motion.SiteId),
                isDepartureTransitionCommit: motion.DeparturePhase == PlayerPartyDeparturePhase.TransitionCommit,
                usesTravelPresentation: motion.UsesTravelPresentation,
                isMaterializeHeld: false,
                hasGeometry: true);
            if (!WorldSiteLocalVisibleSyncPolicy.CanSync(ctx))
                return;

            var local = new WorldVec2(view.transform.position.x, view.transform.position.y);
            var outcome = PlayerPartyWorldSiteLocalVisibleSync.TrySync(
                motion, geometry, bounds, local, out _);
            if (outcome == WorldSiteSyncOutcome.MappingFailed)
                LogSiteSyncFailureThrottled("MappingFailed", motion.SiteId);
            else if (outcome == WorldSiteSyncOutcome.SiteIdRejected)
                LogSiteSyncFailureThrottled("SiteIdRejected", motion.SiteId);
        }

        /// <summary>
        /// Phase 5R-B4：解析 / 复用 V2 geometry cache。绑定 SiteId + ActiveMapLayoutId；任一变化即重建
        /// （Site/LocalMap 切换时失效）。构建一次后 B4 每帧复用（V2_07 已验证零堆分配热路径）。
        /// </summary>
        bool TryResolveSiteSyncGeometry(
            XianXia.Core.Simulation.SimulationWorld world,
            PlayerPartyWorldMotion motion,
            out WorldSiteSpatialMapping.WorldSiteLocalMapBounds bounds,
            out HexFootprintSpatialGeometry geometry)
        {
            bounds = default;
            geometry = null;

            var mapId = world.LocalMap != null
                ? (world.LocalMap.ActiveMapLayoutId ?? string.Empty).Trim()
                : string.Empty;
            var siteId = motion.SiteId ?? string.Empty;
            if (_siteSyncCacheGeometry != null &&
                string.Equals(_siteSyncCacheSiteId, siteId, System.StringComparison.Ordinal) &&
                string.Equals(_siteSyncCacheMapId, mapId, System.StringComparison.Ordinal))
            {
                bounds = _siteSyncCacheBounds;
                geometry = _siteSyncCacheGeometry;
                return true;
            }

            if (string.IsNullOrEmpty(siteId))
                return false;

            WorldSite site = null;
            if (world.Strategic?.Sites == null ||
                !world.Strategic.Sites.TryGet(siteId, out site) ||
                site == null)
                return false;

            if (!TryResolveSiteSyncBounds(out var newBounds))
                return false;

            var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                ? world.HexWorld.HexSize
                : 1f;
            if (!WorldSiteSpatialMapping.TryBuildGeometry(site, hexSize, out var newGeometry) ||
                !newGeometry.HasKernel)
                return false;

            _siteSyncCacheSiteId = siteId;
            _siteSyncCacheMapId = mapId;
            _siteSyncCacheBounds = newBounds;
            _siteSyncCacheGeometry = newGeometry;
            bounds = newBounds;
            geometry = newGeometry;
            return true;
        }

        /// <summary>与 <see cref="TryResolveWildernessBounds"/> 同源解析：MapLayout → WorldSiteLocalMapBounds。</summary>
        bool TryResolveSiteSyncBounds(out WorldSiteSpatialMapping.WorldSiteLocalMapBounds bounds)
        {
            bounds = default;
            var session = bootstrap?.Session;
            var mapId = session?.World?.LocalMap?.ActiveMapLayoutId?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(mapId) && session?.Registry != null)
            {
                var parsed = DefinitionId.Parse(mapId.Trim());
                if (parsed.IsSuccess &&
                    session.Registry.TryGetMapLayout(parsed.Value, out var layout) &&
                    layout != null &&
                    layout.Width > 0 &&
                    layout.Height > 0)
                {
                    var cs = layout.CellSize > 0.0001f ? layout.CellSize : 1f;
                    bounds = WorldSiteSpatialMapping.WorldSiteLocalMapBounds.FromOriginSize(
                        layout.OriginX, layout.OriginY, cs, layout.Width, layout.Height);
                    return true;
                }
            }

            var grid = bootstrap != null ? bootstrap.MoveController?.WalkGrid : null;
            if (grid == null)
                return false;
            bounds = WorldSiteSpatialMapping.WorldSiteLocalMapBounds.FromOriginSize(
                grid.OriginX, grid.OriginY, grid.CellSize, grid.Width, grid.Height);
            return true;
        }

        /// <summary>mapping failure 诊断：once / state-change / throttled（&gt;2s 才再打），避免每帧刷 Console。</summary>
        void LogSiteSyncFailureThrottled(string kind, string siteId)
        {
            if (string.Equals(_siteSyncLastFailureKind, kind, System.StringComparison.Ordinal) &&
                Time.unscaledTime - _siteSyncLastFailureTime < 2f)
                return;
            _siteSyncLastFailureKind = kind;
            _siteSyncLastFailureTime = Time.unscaledTime;
            Debug.LogWarning(
                "[B4 SiteSync] " + kind +
                " site=" + (siteId ?? "") +
                " map=" + (_siteSyncCacheMapId ?? "") +
                " geometry=" + (_siteSyncCacheGeometry != null ? "cached" : "null") +
                " held=" + _siteSyncHeld);
        }

        /// <summary>
        /// Phase 5C-W1: drive Active via existing Local A* toward the formal Wilderness Exit
        /// approach point while ExecutionMode=LocalVisible and LocationKind=AtWorldPosition.
        /// WorldSite LocalVisible is deliberately ignored (Phase 5B stands still).
        /// Wilderness Continuous Visible AutoTravel: after crossing into the next hex the driver
        /// waits for the new LocalMap presentation to finish, then re-arms the NEW current leg and
        /// keeps going automatically (no one-map pause, no repeated WorldMap dance).
        /// </summary>
        void TickLocalVisibleAutoTravelMovement()
        {
            if (HostInputGate.BlockWorldInteraction)
                return;

            var session = bootstrap?.Session;
            var world = session?.World;
            var party = Party;
            if (world == null || party == null || !party.HasActive)
                return;

            var motion = world.PlayerPartyTravel;
            // Phase 5R-B6：WorldSite departure approach —— 角色在 Site LocalMap 内自动走向正式出口。
            // 条件：AtWorldSite + AutoTravel + ExecutionMode=LocalVisible + departure pending
            // （BeginTravel 已生成 DeparturePlan；CloseWorldMapTakeover 已把 ExecutionMode 切 LocalVisible）。
            if (motion != null &&
                motion.IsMoving &&
                motion.ExecutionMode == PlayerPartyTravelExecutionMode.LocalVisible &&
                motion.LocationKind == PlayerPartyLocationKind.AtWorldSite &&
                motion.IsSiteDeparturePending)
            {
                TickWorldSiteDepartureApproach(world, motion, party);
                return;
            }

            // LocalVisible active = travel preserved + ExecutionMode LocalVisible + continuous
            // Wilderness position (WorldSite LocalVisible stays Phase 5B: stand still).
            var isLocalVisible =
                motion != null &&
                motion.IsMoving &&
                motion.ExecutionMode == PlayerPartyTravelExecutionMode.LocalVisible &&
                motion.LocationKind == PlayerPartyLocationKind.AtWorldPosition;

            if (!isLocalVisible)
            {
                // Left LocalVisible (e.g. WorldMap reopened, travel cancelled): only clear Host
                // execution state. Never touch TravelPlan / Segment / Destination / WorldPosition /
                // CameraDetachedByPlayer here.
                _localVisibleTakeoverActive = false;
                ResetLocalVisibleAutoTravelTracking();
                return;
            }

            // Rising edge: ExecutionMode entered LocalVisible (1st / 2nd / 3rd Close behave
            // identically). Every takeover re-arms the CURRENT leg unconditionally, regardless of
            // a previously issued Order, a different SegmentIndex, a stale paused flag or an
            // identical last target.
            if (!_localVisibleTakeoverActive)
            {
                _localVisibleTakeoverActive = true;
                ReArmCurrentLocalLeg(motion);
            }

            // Crossed into the next hex DURING this LocalVisible session: the new Wilderness
            // LocalMap is loading. Wait for the edge presentation to finish, then re-arm the
            // CURRENT (new) leg and continue driving automatically (no one-map pause).
            var gate = motion.SurfaceEdgeGate;
            if (_autoTravelLegSegmentIndex >= 0 &&
                motion.SegmentIndex != _autoTravelLegSegmentIndex)
            {
                if (gate != null && gate.TransitionInProgress)
                    return; // new LocalMap still finalizing; retry next frame
                _autoTravelLegSegmentIndex = motion.SegmentIndex;
                _lastAutoTravelTarget = default; // force re-issue for the new leg
            }

            // Failed drive attempts back off briefly instead of re-path spamming every frame.
            if (Time.time < _autoTravelRetryCooldownUntil)
                return;

            var active = party.ActiveCharacterId;
            if (active.IsNone || _spawner == null)
                return;
            if (!_spawner.Registry.TryGet(active, out var activeView) || activeView == null)
                return;

            if (!PlayerPartyLocalVisibleAutoTravelService.TryResolveActiveLeg(
                    motion, out var currentHex, out var nextHex, out var directionIndex))
            {
                // No remaining leg: either the path ended at the final Wilderness destination hex
                // (walk to its LocalMap center, then FinishArrival) or the plan is exhausted.
                LastTransitionStatus = "FinalLeg";
                TryDriveFinalWildernessArrival(world, motion);
                return;
            }

            // Phase 5R-B7A：WorldSite footprint 与普通 Surface 共用 passability。
            // 目标 Site 仍在 ingress 时完成 Travel；非目标 Site 由 transition service 保留
            // 同一 HexPath/Destination，进入 Site LocalMap 后自动形成正式 departure 并继续。

            if (!TryResolveWildernessBounds(out var bounds))
                return;

            if (!PlayerPartyLocalVisibleAutoTravelService.TryResolveWildernessExitConnection(
                    world, bounds, currentHex, nextHex, directionIndex, out var connection))
            {
                LastTransitionStatus = "NoExit";
                _autoTravelRetryCooldownUntil = Time.time + 0.5f; // No Exit => back off.
                return;
            }

            LastExitSourceHex = connection.SourceHex.ToString();
            LastExitDestinationHex = connection.DestinationHex.ToString();
            LastExitSlotRect =
                "(" + connection.SlotRect.MinX.ToString("0.##") + "," + connection.SlotRect.MinY.ToString("0.##") +
                ")-(" + connection.SlotRect.MaxX.ToString("0.##") + "," + connection.SlotRect.MaxY.ToString("0.##") + ")";

            var activePos = activeView.transform.position;
            var depth = SurfaceExitZoneCalculator.ResolveDepthFromSession(world, bounds);

            // 到达正式 Exit = 角色位置进入该 connection 的 SlotRect（唯一权威判定，
            // 与真实 Trigger / 半透明 Debug 方块同一真源）。不再使用 ExitCenter 半径 fallback。
            var arrivedAtExit = SurfaceExitZoneCalculator.PointBelongsToConnection(
                activePos.x, activePos.y, connection, depth);
            LastActiveInsideExitSlot = arrivedAtExit;

            if (arrivedAtExit)
            {
                // SurfaceEdgeGate 未 armed：不向外推（LocalMap bounds 不允许真正离图）。
                // 先让 Active 走向地图中心（Safe Interior），TickRearm 途中重新 armed，
                // 下一轮到达 Exit 即可触发 —— 绝不永久顶在边缘（不重写 Gate）。
                if (gate != null && !gate.CanAttemptEdgeTransition)
                {
                    LastTransitionStatus = "GateDisarmed";
                    LastTransitionFailureReason = string.Empty;
                    var centerTarget =
                        new Vector3(bounds.CenterX, bounds.CenterY, HostPresentationSpace.EntityZ);
                    var centerMoving = _move != null && _move.IsMoving(active);
                    var sameCenter = Vector3.Distance(centerTarget, _lastAutoTravelTarget) < 0.05f;
                    if (!(centerMoving && sameCenter) &&
                        (_move == null ||
                         !_move.OrderEntityToWorldPoint(active, centerTarget, null, issueStop: false)))
                    {
                        _autoTravelRetryCooldownUntil = Time.time + 0.5f;
                        return;
                    }

                    _lastAutoTravelTarget = centerTarget;
                    SyncLocalVisibleProgress(world, motion);
                    return;
                }

                // 已到达正式 Exit 且 Gate 允许：直接汇入与手动离图相同的正式 Transition
                // Authority。服务内部再次校验 connection 对应 CurrentHex→NextHex / 可通行 /
                // WorldSite 拦截，并完成 World Hex 切换、SetSegment 推进、EnterWildernessLocalMap
                // （新图加载 + Entry projection）、Gate 状态 —— 不自行 Snap / 不 Reload。
                var cross = PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                    world, party, connection);
                if (cross.IsSuccess)
                {
                    LastTransitionStatus = "Crossed->" + nextHex;
                    LastTransitionFailureReason = string.Empty;
                    bootstrap.ExpandLocalMapForCurrentPartyWorld(closeWorldMap: false);
                    EnsureEdgeGateCompletedAfterExpand(world);
                    return;
                }

                LastTransitionStatus = "Rejected";
                LastTransitionFailureReason = cross.Error.ToString();
                _autoTravelRetryCooldownUntil = Time.time + 0.5f; // 触发被拒（gate 竞争等）→ 退避。
                return;
            }

            // 未到达 Exit：Local A* 正常走向该 connection 的 approach 点（Exit Zone 内侧，
            // 不要求目标位于 bounds 外）。
            PlayerPartyLocalVisibleAutoTravelService.GetExitApproachLocalPoint(
                connection, bounds, out var localX, out var localY);
            // 权威校验：请求目标必须 ∈ 正式 SlotRect ∩ playable bounds。几何修正后 approach
            // 恒在其中；此处仅作 clamp 防御（非 magic offset），禁止 Pathfinder 把非法 Exit
            // target 静默解析到不属于 SlotRect 的墙角。
            if (!SurfaceExitZoneCalculator.PointBelongsToConnection(
                    localX, localY, connection, depth))
            {
                var slot = connection.SlotRect;
                localX = Mathf.Clamp(
                    localX,
                    Mathf.Max(slot.MinX, bounds.MinX),
                    Mathf.Min(slot.MaxX, bounds.MaxX));
                localY = Mathf.Clamp(
                    localY,
                    Mathf.Max(slot.MinY, bounds.MinY),
                    Mathf.Min(slot.MaxY, bounds.MaxY));
            }

            var target = new Vector3(localX, localY, HostPresentationSpace.EntityZ);

            var alreadyMoving = _move != null && _move.IsMoving(active);
            var sameTarget = Vector3.Distance(target, _lastAutoTravelTarget) < 0.05f;
            if (alreadyMoving && sameTarget)
            {
                SyncLocalVisibleProgress(world, motion);
                return;
            }

            if (_move == null ||
                !_move.OrderEntityToWorldPoint(active, target, null, issueStop: false))
            {
                LastTransitionStatus = "PathBlocked";
                _autoTravelRetryCooldownUntil = Time.time + 0.5f; // A* failed => back off.
                return;
            }

            _lastAutoTravelTarget = target;
            SyncLocalVisibleProgress(world, motion);
        }

        /// <summary>
        /// Phase 5R-B6：WorldSite LocalVisible → 正式出口 approach 驱动。
        /// 角色在 Site LocalMap 内自动走向 DeparturePlan 的正式 <see cref="SurfaceExitConnection"/>
        /// （由 TryBuildPathLeavingSite 的 first outside hex 决定，不按 Anchor/Presence/最近边猜测）：
        ///  1. departure phase Planned → Approaching（LocalVisible owns，B4 继续 Local→Canonical）；
        ///  2. 解析正式 connection（真实 MapLayout bounds → SlotRect 与 presenter 视觉方块同源）
        ///     → BoundaryContactWorld → V2 WorldToLocal；
        ///  3. Local A* 驱动 → 到达正式 SlotRect 触发带（approach 目标权威 clamp 进触发带内，
        ///     不再停在带外）→ TransitionCommit（B4 停）→
        ///     TryCrossWorldSiteEdgePreservingLocalVisibleAutoTravel 正式 egress
        ///     （AtWorldSite → AtWorldPosition + EnterWildernessLocalMap），原 route 继续。
        /// 任何失败：不 teleport、不 fallback，保留 AtWorldSite + 当前 Canonical，throttled 诊断。
        /// cross 失败回退 Approaching（恢复 B4），不残留 TransitionCommit 卡死。
        /// </summary>
        void TickWorldSiteDepartureApproach(
            XianXia.Core.Simulation.SimulationWorld world,
            PlayerPartyWorldMotion motion,
            PlayerPartyRuntime party)
        {
            if (motion.DeparturePhase == PlayerPartyDeparturePhase.Planned)
                motion.SetDeparturePhase(PlayerPartyDeparturePhase.Approaching);

            var active = party != null ? party.ActiveCharacterId : EntityId.None;
            if (active.IsNone || _spawner == null ||
                !_spawner.Registry.TryGet(active, out var activeView) ||
                activeView == null)
                return;

            var siteId = motion.SiteId ?? string.Empty;
            WorldSite site = null;
            if (string.IsNullOrEmpty(siteId) ||
                world.Strategic?.Sites == null ||
                !world.Strategic.Sites.TryGet(siteId, out site) ||
                site == null)
            {
                LastTransitionStatus = "DepartureNoSite";
                return;
            }

            var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                ? world.HexWorld.HexSize
                : 1f;

            // 真实 Site LocalMap playable bounds（与 HostSurfaceExitZonePresenter 同源）→
            // connection.SlotRect 与视觉方块一致。
            if (!TryResolveSiteSyncGeometry(world, motion, out var bounds, out var geometry))
            {
                LastTransitionStatus = "DepartureNoGeometry";
                return;
            }

            var wildBounds = WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(
                bounds.OriginX, bounds.OriginY, bounds.CellSize, bounds.Width, bounds.Height);

            // 正式出口连接：DeparturePlan 的 first outside hex（TryBuildPathLeavingSite 已选）。
            if (!WorldSiteFootprintExitConnectionResolver.TryResolveFormalExitConnection(
                    world,
                    site,
                    motion.SiteDepartureFootprintHex,
                    motion.SiteDepartureExitHex,
                    hexSize,
                    wildBounds,
                    out var connection))
            {
                LastTransitionStatus = "DepartureNoConnection";
                _autoTravelRetryCooldownUntil = Time.time + 0.5f;
                return;
            }

            var activePos = activeView.transform.position;
            var authoredDepth = world?.LocalMap != null ? world.LocalMap.ExitTriggerDepth : 0f;
            var depth = authoredDepth > 0.0001f
                ? authoredDepth
                : SurfaceExitZoneCalculator.DefaultExitTriggerDepth;

            // 到达判定：角色进入该 connection 的 SlotRect（与 presenter 视觉方块同一真源：
            // 同一 connection → 同一真实 bounds 派生 SlotRect）。
            var arrivedAtExit = SurfaceExitZoneCalculator.PointBelongsToConnection(
                activePos.x, activePos.y, connection, depth);

            if (arrivedAtExit)
            {
                // 正式 egress：先置 TransitionCommit（B4 停止），随后 egress 保留原 route。
                motion.SetDeparturePhase(PlayerPartyDeparturePhase.TransitionCommit);
                var cross = PlayerPartyLocalVisibleAutoTravelService
                    .TryCrossWorldSiteEdgePreservingLocalVisibleAutoTravel(world, party, connection);
                if (cross.IsSuccess)
                {
                    LastTransitionStatus = "SiteExit->" + connection.DestinationHex;
                    LastTransitionFailureReason = string.Empty;
                    // Phase 5R-B6.1：请求 egress 后 materialize 完成的 one-shot recenter
                    // （OnLocalMapMaterialized 消费，final transform 确定后对准主控）。
                    // B6.2：记录 egress 时 LocalMapId，消费要求换图，防泄漏误 recenter。
                    _pendingEgressRecenter = true;
                    _pendingEgressRecenterMapId =
                        world.LocalMap?.ActiveMapLayoutId ?? string.Empty;
                    bootstrap.ExpandLocalMapForCurrentPartyWorld(closeWorldMap: false);
                    return;
                }

                // Phase 5R-B6.2：cross 失败 → 回退 Approaching（恢复 B4 sync），不残留
                // TransitionCommit 卡死；保留当前位置可重试，不 teleport。
                motion.SetDeparturePhase(PlayerPartyDeparturePhase.Approaching);
                LastTransitionStatus = "SiteExitRejected";
                LastTransitionFailureReason = cross.Error.ToString();
                _autoTravelRetryCooldownUntil = Time.time + 0.5f;
                return;
            }

            // 未到达：正式 approach 点（起点 = connection.ExitCenterLocal（真实 bounds 周界，
            // presenter 同源）→ 沿 inward 退正式 inset → 权威 clamp 进 SlotRect 触发带内；
            // 保证 A* 终点进入触发区，到达即 crossing，不再停在带外）。
            PlayerPartyLocalVisibleAutoTravelService.ResolveWorldSiteExitApproachLocalPoint(
                connection, bounds, depth, out var ax, out var ay);
            var target = new Vector3(ax, ay, HostPresentationSpace.EntityZ);

            var alreadyMoving = _move != null && _move.IsMoving(active);
            var sameTarget = Vector3.Distance(target, _lastAutoTravelTarget) < 0.05f;
            if (alreadyMoving && sameTarget)
            {
                SyncLocalVisibleProgress(world, motion);
                return;
            }

            if (_move == null ||
                !_move.OrderEntityToWorldPoint(active, target, null, issueStop: false))
            {
                LastTransitionStatus = "DeparturePathBlocked";
                _autoTravelRetryCooldownUntil = Time.time + 0.5f;
                return;
            }

            _lastAutoTravelTarget = target;
            SyncLocalVisibleProgress(world, motion);
        }

        void SyncLocalVisibleProgress(
            XianXia.Core.Simulation.SimulationWorld world,
            PlayerPartyWorldMotion motion)
        {
            var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                ? world.HexWorld.HexSize
                : 1f;
            PlayerPartyLocalVisibleAutoTravelService.SyncSegmentProgressFromWorldPosition(motion, hexSize);
        }

        /// <summary>
        /// Phase 5C-W2: Final Wilderness Arrival（专用完成路径）。跨入目标 Wilderness Hex 后保留
        /// AutoTravel / LocalVisible，驱动 Active 用既有 Local A* 真实走向该 LocalMap 中心；
        /// 到达中心附近后才走 CompleteWildernessFinalArrival —— 只结束 AutoTravel，不 Snap /
        /// 不 ClearPartyWorldPresentationCache / 不重新 Materialize，位置保持一致。
        /// 走向中心的过程会让 SurfaceEdgeGate 自然离开边缘并 re-arm。
        /// </summary>
        void TryDriveFinalWildernessArrival(
            XianXia.Core.Simulation.SimulationWorld world,
            PlayerPartyWorldMotion motion)
        {
            if (!PlayerPartyLocalVisibleAutoTravelService.IsActiveLocalVisibleAutoTravel(motion))
                return;
            if (motion.LocationKind != PlayerPartyLocationKind.AtWorldPosition)
                return; // WorldSite：不在此范围。
            if (!string.IsNullOrEmpty(motion.DestinationSiteId))
                return; // WorldSite 目标：保留 TravelPlan，不做 Site Arrival。
            if (!motion.CurrentHex.Equals(motion.DestinationHex))
                return; // 尚未跨入目标 Hex。

            var active = Party != null ? Party.ActiveCharacterId : EntityId.None;
            if (active.IsNone || _spawner == null ||
                !_spawner.Registry.TryGet(active, out var activeView) ||
                activeView == null)
                return;
            if (!TryResolveWildernessBounds(out var bounds))
                return;

            var pos = activeView.transform.position;

            // 到达判定：真正接近 LocalMap 中心（由 bounds 推导，非 magic offset），而非 Safe
            // Interior（50x50 图离边约 2 格即满足，不是中心）。角色用 Local A* 真实走向中心。
            var centerRadius = Mathf.Max(0.5f, Mathf.Min(bounds.HalfWidth, bounds.HalfHeight) * 0.25f);
            var dxCenter = pos.x - bounds.CenterX;
            var dyCenter = pos.y - bounds.CenterY;
            var arrived = dxCenter * dxCenter + dyCenter * dyCenter <= centerRadius * centerRadius;
            if (arrived)
            {
                // 最后一次 LocalPosition → WorldPosition sync（保持连续位置一致，不 Snap）。
                PlayerPartyWildernessTransitionService.TrySyncLocalMovementToWorldPosition(
                    world, pos.x, pos.y, bounds);
                var finish = PlayerPartyHexTravelService.CompleteWildernessFinalArrival(world);
                if (finish.IsFailure)
                {
                    LastTransitionStatus = "FinalArrivalRejected";
                    LastTransitionFailureReason = finish.Error.ToString();
                }
                else
                {
                    LastTransitionStatus = "Arrived";
                    LastTransitionFailureReason = string.Empty;
                }

                _finalArrivalApproachInProgress = false;
                // Arrival 完成后角色已在 Wilderness 中心区域：让 Gate 通过现有 re-arm 逻辑
                // 恢复为可再次 Transition 的状态（不强制、不绕过）。
                TryRearmEdgeGateIfInSafeInterior(motion);
                return;
            }

            _finalArrivalApproachInProgress = true;
            var center = new Vector3(bounds.CenterX, bounds.CenterY, HostPresentationSpace.EntityZ);
            var alreadyMoving = _move != null && _move.IsMoving(active);
            var sameTarget = Vector3.Distance(center, _lastAutoTravelTarget) < 0.05f;
            if (alreadyMoving && sameTarget)
            {
                SyncLocalVisibleProgress(world, motion);
                return;
            }

            if (_move == null ||
                !_move.OrderEntityToWorldPoint(active, center, null, issueStop: false))
                return; // A* 失败：保持现状，下帧重试。

            _lastAutoTravelTarget = center;
            SyncLocalVisibleProgress(world, motion);
        }

        void CancelLocalVisibleAutoTravelForPlayerInterrupt()
        {
            var world = bootstrap?.Session?.World;
            if (world == null)
                return;
            var motion = world.PlayerPartyTravel;
            if (motion == null ||
                !PlayerPartyLocalVisibleAutoTravelService.IsActiveLocalVisibleAutoTravel(motion))
                return;

            PlayerPartyHexTravelService.CancelTravel(world);
            _localVisibleTakeoverActive = false;
            ResetLocalVisibleAutoTravelTracking();
        }

        void ResetLocalVisibleAutoTravelTracking()
        {
            _autoTravelLegSegmentIndex = -1;
            _lastAutoTravelTarget = default;
            _autoTravelRetryCooldownUntil = 0f;
            _finalArrivalApproachInProgress = false;
        }

        /// <summary>
        /// Fresh LocalVisible takeover: unconditionally re-arm the current leg.
        /// - clears the paused flag
        /// - anchors the leg to the CURRENT SegmentIndex
        /// - clears last target / any issued Local Move so the A* re-issues for the current Exit
        /// </summary>
        void ReArmCurrentLocalLeg(PlayerPartyWorldMotion motion)
        {
            _autoTravelLegSegmentIndex = motion != null ? motion.SegmentIndex : -1;
            _lastAutoTravelTarget = default;
            _autoTravelRetryCooldownUntil = 0f;

            var active = Party != null ? Party.ActiveCharacterId : EntityId.None;
            if (!active.IsNone && _move != null)
                _move.CancelPresentationMovementPublic(active);

            // 新一趟 AutoTravel 开始时：若角色当前已处于正式 Safe Interior，立即通过现有
            // Gate re-arm 逻辑恢复（TickRearm 本身要求 Safe Interior 且不强制）。
            // 若不在 Safe Interior，保持现有规则，不绕过 Gate。
            TryRearmEdgeGateIfInSafeInterior(motion);
        }

        /// <summary>
        /// 仅当角色位置属于正式 Safe Interior 时，通过现有 PlayerPartySurfaceEdgeGate.TickRearm
        /// 恢复 Gate。不强制 armed、不绕过 Gate、不新增状态机。
        /// </summary>
        void TryRearmEdgeGateIfInSafeInterior(PlayerPartyWorldMotion motion)
        {
            if (motion?.SurfaceEdgeGate == null)
                return;
            if (motion.SurfaceEdgeGate.CanAttemptEdgeTransition)
                return; // 已 armed：无需处理。

            var active = Party != null ? Party.ActiveCharacterId : EntityId.None;
            if (active.IsNone || _spawner == null ||
                !_spawner.Registry.TryGet(active, out var view) ||
                view == null)
                return;
            if (!TryResolveWildernessBounds(out var bounds))
                return;

            var pos = view.transform.position;
            if (!WildernessLocalWorldProjection.IsInSafeInterior(pos.x, pos.y, bounds))
                return; // 不在 Safe Interior：保持现有规则，不绕过。

            motion.SurfaceEdgeGate.TickRearm(pos.x, pos.y, bounds);
        }

        static Vector2 ReadWasdDirection()
        {
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
                return Vector2.zero;

            var dir = Vector2.zero;
            if (Input.GetKey(KeyCode.W)) dir.y += 1f;
            if (Input.GetKey(KeyCode.S)) dir.y -= 1f;
            if (Input.GetKey(KeyCode.A)) dir.x -= 1f;
            if (Input.GetKey(KeyCode.D)) dir.x += 1f;
            if (dir.sqrMagnitude > 1f)
                dir.Normalize();
            return dir;
        }

        void TickFollowers()
        {
            var active = Party.ActiveCharacterId;
            if (active.IsNone || _move == null || _spawner == null)
                return;

            if (!_spawner.Registry.TryGet(active, out var activeView) || activeView == null)
                return;

            var activePos = activeView.transform.position;
            var followerIndex = 0;
            for (var i = 0; i < Party.Members.Count; i++)
            {
                var id = Party.Members[i];
                if (Party.IsActive(id))
                    continue;

                if (_melee != null && _melee.IsAttacker(id))
                    continue;
                if (_chop != null && _chop.IsAttacker(id))
                    continue;
                if (_farm != null && _farm.IsFarming(id))
                    continue;
                if (_move.IsMoving(id))
                    continue;

                if (!ShouldRepathFollower(id))
                    continue;

                if (!_spawner.Registry.TryGet(id, out var view) || view == null)
                    continue;

                var offset = FollowerOffset(followerIndex++);
                var goal = activePos + offset;
                goal.z = HostPresentationSpace.EntityZ;
                var dist = Vector2.Distance(
                    new Vector2(view.transform.position.x, view.transform.position.y),
                    new Vector2(activePos.x + offset.x, activePos.y + offset.y));
                if (dist <= followStopDistance)
                    continue;

                _move.OrderEntityToWorldPoint(id, goal, null, issueStop: false);
                _nextFollowRepath[id.Value] = Time.unscaledTime + followRepathInterval;
            }
        }

        bool ShouldRepathFollower(EntityId id)
        {
            if (!_nextFollowRepath.TryGetValue(id.Value, out var next))
                return true;
            return Time.unscaledTime >= next;
        }

        Vector3 FollowerOffset(int index)
        {
            if (index <= 0)
                return new Vector3(-followerSpreadRadius, 0f, 0f);
            var angle = index * 137.5f * Mathf.Deg2Rad;
            var r = followerSpreadRadius * (1f + 0.15f * (index % 3));
            return new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f);
        }

        void OrderFollowerTowardActive(EntityId follower)
        {
            var active = Party.ActiveCharacterId;
            if (follower.IsNone || active.IsNone || _move == null || _spawner == null)
                return;

            if (!_spawner.Registry.TryGet(active, out var activeView) || activeView == null)
                return;

            var goal = activeView.transform.position;
            goal.z = HostPresentationSpace.EntityZ;
            _move.OrderEntityToWorldPoint(follower, goal, null, issueStop: true);
            _nextFollowRepath[follower.Value] = Time.unscaledTime + followRepathInterval;
        }

        void TickCombatFollow()
        {
            if (_melee == null || !_melee.IsFighting)
                return;

            var active = Party.ActiveCharacterId;
            if (!_melee.IsAttacker(active))
                return;

            var defender = _melee.DefenderId;
            for (var i = 0; i < Party.Members.Count; i++)
            {
                var id = Party.Members[i];
                if (Party.IsActive(id) || _melee.IsAttacker(id))
                    continue;
                if (!bootstrap.Session.World.Entities.TryGet(id, out var ent) ||
                    !CombatLifeStateService.CanFight(ent))
                    continue;

                _melee.Begin(id, defender);
            }
        }

        void TickPartyDerivedGroupActivity()
        {
            var active = Party.ActiveCharacterId;
            if (active.IsNone)
                return;

            var current = ResolveActiveSharedActivity(active);
            if (current != _lastActiveSharedActivity)
            {
                for (var i = 0; i < Party.Members.Count; i++)
                {
                    var id = Party.Members[i];
                    if (Party.IsActive(id))
                        continue;
                    if (_followerSharedActivity.TryGetValue(id.Value, out var assigned) &&
                        assigned == current)
                        continue;
                    StopFollowerPartyDerivedWork(id);
                }

                _lastActiveSharedActivity = current;
            }

            if (!current.IsShareable)
            {
                ClearFollowerPartyDerivedWork();
                return;
            }

            for (var i = 0; i < Party.Members.Count; i++)
            {
                var id = Party.Members[i];
                if (Party.IsActive(id))
                    continue;
                if (_followerSharedActivity.TryGetValue(id.Value, out var assigned) &&
                    assigned == current)
                    continue;

                if (TryAssignFollowerSharedActivity(id, current, active))
                    _followerSharedActivity[id.Value] = current;
            }
        }

        HostPartySharedActivity ResolveActiveSharedActivity(EntityId active)
        {
            if (_melee != null && _melee.IsAttacker(active))
                return HostPartySharedActivity.Combat;

            if (IsActivePlayerDrivenMoving(active))
                return HostPartySharedActivity.Movement;

            if (_farm != null && _farm.IsFarming(active))
            {
                var loc = ResolveFarmLocation(active);
                if (!string.IsNullOrEmpty(loc))
                    return HostPartySharedActivity.Farming(loc);
            }

            if (_chop != null && _chop.IsAttacker(active))
            {
                var target = _chop.GetTargetForAttacker(active);
                var instanceId = target != null ? target.GetInstanceID() : 0;
                return HostPartySharedActivity.Woodcutting(instanceId);
            }

            if (_workLoop != null && _workLoop.IsLooping(active) &&
                _workLoop.TryGetLoopKind(active, out var loopKind))
            {
                var loc = ResolveEntityLocation(active);
                if (!string.IsNullOrEmpty(loc))
                    return HostPartySharedActivity.Gathering(loc, loopKind);
            }

            return HostPartySharedActivity.FollowIdle;
        }

        bool TryAssignFollowerSharedActivity(
            EntityId follower,
            HostPartySharedActivity activity,
            EntityId active)
        {
            StopFollowerPartyDerivedWork(follower);

            switch (activity.Kind)
            {
                case HostPartySharedActivityKind.Farming:
                    return _farm != null &&
                           _farm.Begin(follower, activity.LocationId, fromPartyFollow: true);
                case HostPartySharedActivityKind.Woodcutting:
                    return TryBeginFollowerWoodcut(follower, active);
                case HostPartySharedActivityKind.Gathering:
                    if (_workLoop == null)
                        return false;
                    _workLoop.StartPartyDerivedLoop(follower, activity.LoopKind);
                    return _workLoop.IsPartyDerivedLooping(follower);
                default:
                    return false;
            }
        }

        bool TryBeginFollowerWoodcut(EntityId follower, EntityId active)
        {
            if (_chop == null || _spawner == null)
                return false;

            if (!_spawner.Registry.TryGet(follower, out var followerView) || followerView == null)
                return false;

            var from = followerView.transform.position;
            HostMapDestructible activeTarget = null;
            if (_chop.IsAttacker(active))
                activeTarget = _chop.GetTargetForAttacker(active);

            if (!HostMapObjectRegistry.TryFindNearestDestructible(
                    from,
                    followerChopSearchRadius,
                    out var tree,
                    treesOnly: true,
                    exclude: activeTarget) &&
                activeTarget != null)
                tree = activeTarget;

            if (tree == null)
                return false;

            _chop.Begin(follower, tree, fromPartyFollow: true);
            return _chop.IsAttacker(follower);
        }

        void StopFollowerPartyDerivedWork(EntityId id)
        {
            _farm?.StopPartyDerived(id);
            _chop?.StopPartyDerived(id);
            _workLoop?.StopPartyDerived(id);
            _followerSharedActivity.Remove(id.Value);
        }

        void ClearFollowerPartyDerivedWork()
        {
            for (var i = 0; i < Party.Members.Count; i++)
            {
                var id = Party.Members[i];
                if (Party.IsActive(id))
                    continue;
                if (_followerSharedActivity.ContainsKey(id.Value) ||
                    (_farm != null && _farm.IsPartyDerivedFarming(id)) ||
                    (_chop != null && _chop.IsPartyDerivedAttacker(id)) ||
                    (_workLoop != null && _workLoop.IsPartyDerivedLooping(id)))
                    StopFollowerPartyDerivedWork(id);
            }
        }

        string ResolveFarmLocation(EntityId id)
        {
            if (_farm != null && _farm.TryGetFarmLocation(id, out var loc))
                return loc;
            return ResolveEntityLocation(id);
        }

        string ResolveEntityLocation(EntityId id)
        {
            if (bootstrap?.Session?.World == null ||
                !bootstrap.Session.World.Entities.TryGet(id, out var ent) ||
                !ent.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var loc) ||
                !loc.HasLocation)
                return null;
            return loc.LocationId;
        }

        void TickCameraFollow()
        {
            if (!enableCameraFollow || _cameraRig == null || _spawner == null)
                return;
            if (HostInputGate.BlockWorldCamera)
                return;

            var active = Party.ActiveCharacterId;
            if (active.IsNone ||
                !_spawner.Registry.TryGet(active, out var view) ||
                view == null)
                return;

            // Phase 5C-W1: LocalVisible AutoTravel follows Active; middle-mouse pan DETACHES the
            // camera until a NEW AutoTravel session starts (restored only on fresh session; not on
            // Open/Close WorldMap within the same travel).
            var world = bootstrap?.Session?.World;
            if (world?.PlayerPartyTravel != null)
            {
                var sessionMotion = world.PlayerPartyTravel;
                // Travel ended / cancelled: next LocalVisible takeover is a NEW session.
                if (!sessionMotion.IsMoving)
                    _hasAutoTravelSession = false;
            }

            if (world?.PlayerPartyTravel != null &&
                PlayerPartyLocalVisibleAutoTravelService.IsActiveLocalVisibleAutoTravel(world.PlayerPartyTravel) &&
                (world.PlayerPartyTravel.LocationKind == PlayerPartyLocationKind.AtWorldPosition ||
                 // Phase 5R-B6.2：WorldSite DepartureApproach（AtWorldSite + departure pending + LocalVisible
                 // AutoTravel）也是 LocalVisible execution —— Camera 跟随 Active Character。
                 (world.PlayerPartyTravel.LocationKind == PlayerPartyLocationKind.AtWorldSite &&
                  world.PlayerPartyTravel.IsSiteDeparturePending)))
            {
                if (!_hasAutoTravelSession)
                {
                    _hasAutoTravelSession = true;
                    _cameraDetachedByPlayer = false; // new AutoTravel session: follow by default
                }

                if (_cameraRig.ConsumeUserMiddlePanThisFrame())
                    _cameraDetachedByPlayer = true; // player took the camera: stay detached
                if (!_cameraDetachedByPlayer)
                    _cameraRig.SoftFollow(view.transform.position, localVisibleAutoTravelFollowLerp);
                return;
            }

            var wasd = HasActiveWasdDirectInput();
            _cameraMode = ResolveCameraFollowMode(wasd);

            if (_cameraMode == HostActiveCameraFollowMode.WasdHardFollow)
                _cameraRig.HardFollow(view.transform.position);
        }

        void EnterWasdHardFollow(EntityId active)
        {
            _cameraMode = HostActiveCameraFollowMode.WasdHardFollow;
            SnapCameraTo(active);
        }

        /// <summary>
        /// Camera Hard Follow source: valid player WASD Direct Movement only.
        /// Not Character.IsMoving / RTS path.
        /// </summary>
        public bool HasActiveWasdDirectInput()
        {
            if (HostInputGate.BlockWorldInteraction)
                return false;
            if (Party == null || Party.ActiveCharacterId.IsNone)
                return false;
            return ReadWasdDirection().sqrMagnitude > 0.01f;
        }

        public HostActiveCameraFollowMode CameraFollowMode => _cameraMode;

        /// <summary>
        /// Final camera policy: only WASD Direct Input engages Hard Follow.
        /// RTS / click path never changes Camera mode.
        /// </summary>
        public static HostActiveCameraFollowMode ResolveCameraFollowMode(bool wasdDirectActive) =>
            wasdDirectActive
                ? HostActiveCameraFollowMode.WasdHardFollow
                : HostActiveCameraFollowMode.Free;

        /// <summary>Compatibility overload; previous mode is ignored (RTS never drives Camera).</summary>
        public static HostActiveCameraFollowMode ResolveCameraFollowMode(
            HostActiveCameraFollowMode _,
            bool wasdDirectActive) =>
            ResolveCameraFollowMode(wasdDirectActive);

        /// <summary>Active 正在 WASD 或点击寻路移动（玩家驱动）——供 Party 共享活动，非 Camera 策略。</summary>
        public bool IsActivePlayerDrivenMoving(EntityId active)
        {
            if (active.IsNone || Party == null || !Party.IsActive(active))
                return false;

            if (HasActiveWasdDirectInput())
                return true;

            return _move != null && _move.IsPlayerPartyPathMoving(active);
        }

        void FrameCameraOn(EntityId id) => SnapCameraTo(id);

        void SnapCameraTo(EntityId id)
        {
            if (!enableCameraFollow || _cameraRig == null || _spawner == null || id.IsNone)
                return;
            if (_spawner.Registry.TryGet(id, out var view) && view != null)
                _cameraRig.FrameWorldPoint(view.transform.position);
        }

        void StopFollowerDirectControl(EntityId id)
        {
            _move?.CancelPresentationMovementPublic(id);
            StopFollowerPartyDerivedWork(id);
            _melee?.DisengageIfAttacker(id);
        }
    }
}
