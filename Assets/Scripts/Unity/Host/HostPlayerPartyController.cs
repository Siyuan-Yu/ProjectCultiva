using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Actions;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Input;
using XianXia.Core.World;
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

        readonly Dictionary<ulong, float> _nextFollowRepath = new Dictionary<ulong, float>();
        readonly Dictionary<ulong, HostPartySharedActivity> _followerSharedActivity =
            new Dictionary<ulong, HostPartySharedActivity>();
        HostPartySharedActivity _lastActiveSharedActivity = HostPartySharedActivity.FollowIdle;
        bool _wasdHeldLastFrame;
        HostActiveCameraFollowMode _cameraMode = HostActiveCameraFollowMode.Free;

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

            TickWasdForActive();
            TickPartyDerivedGroupActivity();
            TickFollowers();
            TickCombatFollow();
            TickWildernessWorldSyncAndEdge();
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
            if (!WildernessLocalWorldProjection.TryResolveExitTriggerIntent(
                    pos.x, pos.y, proposedX, proposedY, bounds, depth, out var edgeDir))
            {
                // WalkGrid 出界但尚无正式 intent：用上一帧 Local（若有）再判一次。
                var grid = _move != null ? _move.WalkGrid : null;
                var leavesWalkGrid = grid != null &&
                                     !grid.TryWorldToCell(proposedX, proposedY, out _, out _);
                if (!leavesWalkGrid)
                    return false;
                if (!WildernessLocalWorldProjection.TryResolveExitTriggerIntent(
                        pos.x, pos.y, proposedX, proposedY, bounds, depth, out edgeDir))
                    return false;
            }

            var cross = PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                world, party, edgeDir);
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
            if (!motion.HasPosition || motion.IsMoving)
                return;
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
                    WildernessLocalWorldProjection.TryResolveExitTriggerIntent(
                        prevX, prevY, localX, localY, bounds, depth, out var siteDir))
                {
                    var exit = PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                        world, party, siteDir);
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

            if (hasPrev &&
                WildernessLocalWorldProjection.TryResolveExitTriggerIntent(
                    prevX, prevY, localX, localY, bounds, depth, out var dir))
            {
                var cross = PlayerPartyWildernessTransitionService.TryAttemptSurfaceEdgeTransition(
                    world, party, dir);
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

            TickCameraFollow();
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
