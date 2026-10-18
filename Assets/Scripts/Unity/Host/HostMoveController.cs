using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Input;
using XianXia.Core.Navigation;
using XianXia.Core.Npc;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    public enum HostMoveCompletionPolicy
    {
        HoldStandby = 0,
        PreserveCurrentCommand = 1
    }

    /// <summary>
    /// RTS 移动：沿 WalkGrid A* 航点行进；交互／修炼抵达后下令。支NPC Stop 订单的走位
    /// </summary>
    public sealed partial class HostMoveController : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] EntityViewSpawner viewSpawner;
        [SerializeField] HostCommandBridge commandBridge;
        [SerializeField] HostNpcContextMenu npcContextMenu;
        [SerializeField] Camera worldCamera;
        [SerializeField] float moveSpeed = 6f;
        [SerializeField] float arriveEpsilon = 0.2f;
        [SerializeField] float formationSpacing = 1.25f;

        readonly Dictionary<EntityView, Vector3> _targets = new Dictionary<EntityView, Vector3>();
        readonly Dictionary<ulong, List<Vector3>> _paths = new Dictionary<ulong, List<Vector3>>();
        readonly Dictionary<ulong, int> _pathIndex = new Dictionary<ulong, int>();
        readonly Dictionary<ulong, PlayerCommandKind> _pendingOnArrive = new Dictionary<ulong, PlayerCommandKind>();
        readonly Dictionary<ulong, string> _pendingArriveLocation = new Dictionary<ulong, string>();
        readonly Dictionary<ulong, System.Action> _pendingArriveActions = new Dictionary<ulong, System.Action>();
        readonly Dictionary<ulong, HostNpcArriveIntent> _pendingNpcIntent = new Dictionary<ulong, HostNpcArriveIntent>();
        readonly List<ulong> _pendingNpcIntentKeys = new List<ulong>(8);
        readonly HashSet<ulong> _interactionHeldNpcs = new HashSet<ulong>();
        readonly HashSet<ulong> _movingIds = new HashSet<ulong>();
        readonly HashSet<ulong> _playerPartyPathMoveIds = new HashSet<ulong>();
        readonly Dictionary<ulong, string> _pathLocalMapIds = new Dictionary<ulong, string>();
        readonly Dictionary<ulong, float> _moveSpeedMultiplierByEntity = new Dictionary<ulong, float>();
        readonly Dictionary<ulong, HostMoveCompletionPolicy> _completionPolicies =
            new Dictionary<ulong, HostMoveCompletionPolicy>();
        // Presentation-only ownership serial.  Independent Host executors use this to cancel
        // only the path they issued, without erasing a newer schedule / needs path.
        readonly Dictionary<ulong, uint> _movementRevisionByEntity = new Dictionary<ulong, uint>();
        readonly List<float> _pathScratch = new List<float>(64);
        readonly List<Vector3> _wpScratch = new List<Vector3>(32);
        // TickMoves 每帧不再 new List；改用 field 级 scratch。
        readonly List<EntityView> _tickViews = new List<EntityView>(64);
        readonly List<EntityView> _tickDone = new List<EntityView>(32);
        readonly Dictionary<ulong, ulong> _carriedToCarrier = new Dictionary<ulong, ulong>();
        int _transportStateFrame = -1;

        const float PursuitRepathDistance = .75f;
        const float PursuitRepathCooldown = .55f;

        WalkGrid _walkGrid;
        int _walkGridGeneration;
        string _boundLocalMapId = string.Empty;

        public WalkGrid WalkGrid => _walkGrid;
        public int WalkGridGeneration => _walkGridGeneration;

        /// <summary>LocalMap layout id that current paths / WalkGrid belong to.</summary>
        public string BoundLocalMapId => _boundLocalMapId;

        public bool IsMoving(EntityId id)
        {
            if (id.IsNone || !_movingIds.Contains(id.Value))
                return false;

            if (!HasActiveMoveTarget(id))
            {
                ClearStaleMoveState(id);
                return false;
            }

            if (_pathLocalMapIds.TryGetValue(id.Value, out var pathMap) &&
                !string.IsNullOrEmpty(_boundLocalMapId) &&
                !string.Equals(pathMap, _boundLocalMapId, StringComparison.Ordinal))
            {
                CancelPresentationMovement(id);
                return false;
            }

            return true;
        }

        public bool HasMovementPath(EntityId id) =>
            !id.IsNone && _paths.ContainsKey(id.Value);

        /// <summary>Read-only animation recovery gate for an active, current-map move path.</summary>
        public bool HasActiveLocomotionTarget(EntityId id) =>
            !id.IsNone && _movingIds.Contains(id.Value) && HasActiveMoveTarget(id) &&
            (!_pathLocalMapIds.TryGetValue(id.Value, out var pathMap) ||
             string.IsNullOrEmpty(_boundLocalMapId) ||
             string.Equals(pathMap, _boundLocalMapId, StringComparison.Ordinal));

        public uint GetPresentationMovementRevision(EntityId id) =>
            id.IsNone || !_movementRevisionByEntity.TryGetValue(id.Value, out var revision)
                ? 0u : revision;

        public bool CancelPresentationMovementIfRevision(EntityId id, uint expectedRevision)
        {
            if (id.IsNone || expectedRevision == 0u ||
                GetPresentationMovementRevision(id) != expectedRevision)
                return false;
            CancelPresentationMovement(id);
            return true;
        }

        public bool TryGetPathLocalMapId(EntityId id, out string localMapId) =>
            _pathLocalMapIds.TryGetValue(id.Value, out localMapId);

        /// <summary>Bind presentation movement to the loaded LocalMap layout.</summary>
        public void BindLocalMapContext(string localMapId) =>
            _boundLocalMapId = localMapId?.Trim() ?? string.Empty;

        /// <summary>
        /// LocalMap materialize / transition: drop party-local paths that cannot cross maps.
        /// </summary>
        public void InvalidatePartyLocalMovement(IReadOnlyList<EntityId> partyMembers)
        {
            if (partyMembers == null)
                return;

            for (var i = 0; i < partyMembers.Count; i++)
            {
                var id = partyMembers[i];
                if (!id.IsNone)
                    CancelPresentationMovement(id);
            }

            PurgeOrphanedMoveTargets();
        }

        /// <summary>Snapshot Rebuild：丢弃旧 EntityView 上的全部 presentation movement 状态。</summary>
        public void ResetPresentationMovementState()
        {
            _targets.Clear();
            _paths.Clear();
            _pathIndex.Clear();
            _pendingOnArrive.Clear();
            _pendingArriveLocation.Clear();
            _pendingArriveActions.Clear();
            _pendingNpcIntent.Clear();
            _interactionHeldNpcs.Clear();
            _movingIds.Clear();
            _playerPartyPathMoveIds.Clear();
            _pathLocalMapIds.Clear();
            _moveSpeedMultiplierByEntity.Clear();
            _completionPolicies.Clear();
            _carriedToCarrier.Clear();
            ResetCharacterSpacing();
            _transportStateFrame = -1;
            _playerPartyPathMoveSerial = 0;
            PurgeOrphanedMoveTargets();
        }

        /// <summary>Active 经由 OrderPartyToPoint* 下发的玩家点击寻路（非 Follower／Melee／Schedule）。</summary>
        public bool IsPlayerPartyPathMoving(EntityId id) =>
            !id.IsNone && _playerPartyPathMoveIds.Contains(id.Value);

        /// <summary>
        /// Increments each time Active receives a new player RTS/click path order.
        /// Not used by Camera (RTS path is camera-decoupled).
        /// </summary>
        public int PlayerPartyPathMoveSerial => _playerPartyPathMoveSerial;

        int _playerPartyPathMoveSerial;

        /// <summary>Active A* polyline for a moving unit: current pos + remaining waypoints.</summary>
        public bool TryGetRemainingPath(EntityId id, List<Vector3> into)
        {
            if (into == null)
                return false;
            into.Clear();
            if (id.IsNone || viewSpawner == null ||
                !viewSpawner.Registry.TryGet(id, out var view) || view == null)
                return false;
            if (!_paths.TryGetValue(id.Value, out var path) || path == null || path.Count == 0)
                return false;

            _pathIndex.TryGetValue(id.Value, out var idx);
            if (idx < 0)
                idx = 0;
            if (idx >= path.Count)
                return false;

            var start = view.transform.position;
            start.z = HostPresentationSpace.EntityZ;
            into.Add(start);
            for (var i = idx; i < path.Count; i++)
            {
                var p = path[i];
                p.z = HostPresentationSpace.EntityZ;
                into.Add(p);
            }

            return into.Count >= 2;
        }

        public bool TryGetPathProgress(EntityId id, out int index, out int count)
        {
            index = 0;
            count = 0;
            if (id.IsNone || !_paths.TryGetValue(id.Value, out var path) || path == null)
                return false;
            _pathIndex.TryGetValue(id.Value, out index);
            count = path.Count;
            return index >= 0 && index < count;
        }

        /// <summary>Checks the issued remainder against the current composite grid without rebuilding it.</summary>
        public bool IsRemainingPathValid(EntityId id)
        {
            if (_walkGrid == null || id.IsNone || viewSpawner == null ||
                !viewSpawner.Registry.TryGet(id, out var view) || view == null ||
                !_paths.TryGetValue(id.Value, out var path) || path == null || path.Count == 0 ||
                !_pathIndex.TryGetValue(id.Value, out var index) || index < 0 || index >= path.Count)
                return false;

            var from = view.transform.position;
            for (var i = index; i < path.Count; i++)
            {
                var to = path[i];
                if (!GridPathfinder.IsWorldSegmentWalkable(_walkGrid, from.x, from.y, to.x, to.y))
                    return false;
                from = to;
            }
            return true;
        }

        /// <summary>Build an A* world polyline without issuing a move order.</summary>
        public bool TryBuildPathPreview(
            Vector3 from, Vector3 to, List<Vector3> into, bool exactGoal = false) =>
            TryBuildWorldPath(from, to, into, exactGoal);

        public Vector3 PreviewFormationGoal(Vector3 click, int moveIndex, int moveCount) =>
            ResolveFormationGoal(click, FormationOffset(moveIndex, moveCount));

        public void Bind(
            PlayableHostBootstrap host,
            HostSelectionController selection,
            EntityViewSpawner spawner,
            HostCommandBridge bridge = null,
            HostNpcContextMenu npcMenu = null)
        {
            bootstrap = host;
            selectionController = selection;
            viewSpawner = spawner;
            commandBridge = bridge;
            npcContextMenu = npcMenu;
            if (worldCamera == null)
                worldCamera = Camera.main;
        }

        public void SetWalkGrid(WalkGrid grid)
        {
            if (!ReferenceEquals(_walkGrid, grid))
                _walkGridGeneration++;
            _walkGrid = grid;
        }

        bool HasActiveMoveTarget(EntityId id)
        {
            if (viewSpawner == null ||
                !viewSpawner.Registry.TryGet(id, out var view) ||
                view == null)
                return false;
            return _targets.ContainsKey(view);
        }

        void ClearStaleMoveState(EntityId id)
        {
            if (id.IsNone)
                return;
            _movingIds.Remove(id.Value);
            _playerPartyPathMoveIds.Remove(id.Value);
            ClearPath(id);
            if (viewSpawner != null &&
                viewSpawner.Registry.TryGet(id, out var view) &&
                view != null &&
                view.ActivityText == "\u79fb\u52a8\u4e2d")
                view.SetActivityText(string.Empty);
        }

        void PurgeOrphanedMoveTargets()
        {
            if (_targets.Count == 0)
                return;

            var staleViews = new List<EntityView>();
            foreach (var kv in _targets)
            {
                if (kv.Key == null || !kv.Key.IsBound)
                    staleViews.Add(kv.Key);
            }

            for (var i = 0; i < staleViews.Count; i++)
                _targets.Remove(staleViews[i]);
        }

        void Update()
        {
            if (bootstrap == null || bootstrap.Session == null || !bootstrap.Session.IsInitialized)
                return;
            // Modal pause owns all input. Manual pause still listens for one legal player world
            // command; the successful command clears ManualPaused before movement advances.
            if (bootstrap.Session.ModalHardPaused)
                return;
            var characterEncounter = bootstrap.Session.World.Strategic.CharacterEncounter;
            if (characterEncounter != null &&
                characterEncounter.Phase != CharacterEncounterPhase.Active &&
                characterEncounter.Phase != CharacterEncounterPhase.ReadyToEnd)
                return;
            if (bootstrap.Session.World.ContentEvents.HasActive)
                return;
            if (bootstrap.ContentInterrupt != null && bootstrap.ContentInterrupt.HasBlockingInterrupt)
                return;
            var blockPlayerInput = HostInputGate.BlockWorldInteraction ||
                                   npcContextMenu != null && npcContextMenu.IsOpen;
            if (worldCamera == null)
                worldCamera = Camera.main;
            if (viewSpawner == null)
                return;

            var workMode = bootstrap.WorkTargetMode;
            if (workMode != null && workMode.IsActive)
            {
                if (bootstrap.Session.IsPaused)
                    return;
                TickMoves();
                return;
            }

            if (!blockPlayerInput && worldCamera != null && selectionController != null &&
                Input.GetMouseButtonDown(1) && !Input.GetKey(KeyCode.LeftAlt))
            {
                if (HostUiHitTest.ContainsScreenPoint(Input.mousePosition))
                    return;
                if (npcContextMenu != null && npcContextMenu.TryOpenAtMouse())
                {
                    // handled
                }
                else if (workMode != null && workMode.TryHandleContextRightClick())
                {
                    // handled
                }
                else
                    IssueMoveToMouse();
            }

            if (bootstrap.Session.IsPaused)
                return;
            TickPendingNpcApproaches();
            TickMoves();
        }

        void IssueMoveToMouse()
        {
            if (!HostPresentationSpace.TryRaycastPlane(worldCamera, Input.mousePosition, out var point))
                return;
            OrderPartyToPoint(point, null);
        }

        public bool OrderPartyToLocation(string locationId, PlayerCommandKind? arriveCommand)
        {
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return false;
            if (!HostZoneQuery.TryGetLocationCenter(bootstrap.Session.World, locationId, out var center))
                return false;
            return OrderPartyToPoint(center, arriveCommand);
        }

        public bool OrderFocusToLocation(EntityId focus, string locationId, PlayerCommandKind? arriveCommand)
        {
            if (!HostZoneQuery.TryGetLocationCenter(bootstrap.Session.World, locationId, out var center))
                return false;
            return OrderEntityToWorldPoint(focus, center, arriveCommand, issueStop: true);
        }

        public bool OrderPartyToPointPublic(Vector3 point) => OrderPartyToPoint(point, null);

        /// <summary>单人寻路；抵达后调用 onArrive（用于入洞等）/summary>
        public bool OrderEntityToWorldPointPublic(EntityId id, Vector3 point, System.Action onArrive,
            bool exactGoal = false)
        {
            if (!OrderEntityToWorldPoint(id, point, null, issueStop: true, exactGoal: exactGoal))
                return false;
            if (onArrive != null)
                _pendingArriveActions[id.Value] = onArrive;
            return true;
        }

        public bool OrderActorToNpc(EntityId actor, EntityId npc, HostNpcArriveAction action)
        {
            if (actor.IsNone || npc.IsNone || viewSpawner == null ||
                !viewSpawner.Registry.TryGet(npc, out var npcView) || npcView == null)
                return false;

            npcContextMenu?.CancelRecruitInteractionForActor(actor);
            var policy = action == HostNpcArriveAction.Attack || action == HostNpcArriveAction.Capture
                ? HostNpcApproachPolicy.Pursuit : HostNpcApproachPolicy.Cooperative;
            var requiredRange = ResolveNpcRequiredRange(actor, action);
            if (policy == HostNpcApproachPolicy.Cooperative)
                HoldNpcForInteraction(npc);
            if (!TryOrderCharacterApproach(actor, npc, requiredRange, issueStop: true))
            {
                if (policy == HostNpcApproachPolicy.Cooperative) ReleaseNpcForInteraction(npc);
                return false;
            }

            return RegisterPendingNpcIntent(actor, npc, action, policy, requiredRange, npcView.transform.position);
        }

        public bool IsNpcHeldForInteraction(EntityId npc) =>
            !npc.IsNone && _interactionHeldNpcs.Contains(npc.Value);

        public bool IsApproachingNpc(EntityId npc)
        {
            if (npc.IsNone)
                return false;
            foreach (var kv in _pendingNpcIntent)
            {
                if (kv.Value.NpcId == npc)
                    return true;
            }

            return false;
        }

        public void HoldNpcForInteraction(EntityId npc)
        {
            if (npc.IsNone)
                return;
            CancelPresentationMovement(npc);
            _interactionHeldNpcs.Add(npc.Value);
            if (viewSpawner != null &&
                viewSpawner.Registry.TryGet(npc, out var view) &&
                view != null)
                view.SetActivityText("\u7a0d\u5019");
        }

        public void ReleaseNpcForInteraction(EntityId npc)
        {
            if (npc.IsNone || !_interactionHeldNpcs.Remove(npc.Value))
                return;
            if (viewSpawner != null &&
                viewSpawner.Registry.TryGet(npc, out var view) &&
                view != null)
                view.SetActivityText(string.Empty);

            var mover = bootstrap != null
                ? bootstrap.GetComponent<HostNpcScheduleMover>()
                : GetComponent<HostNpcScheduleMover>();
            mover?.NotifyNpcReleased(npc);
        }

        void CancelPresentationMovement(EntityId id)
        {
            if (id.IsNone)
                return;
            AdvancePresentationMovementRevision(id);
            ClearPath(id);
            _movingIds.Remove(id.Value);
            _playerPartyPathMoveIds.Remove(id.Value);
            if (viewSpawner == null)
                return;
            if (!viewSpawner.Registry.TryGet(id, out var view) || view == null)
                return;
            _targets.Remove(view);
        }

        /// <summary>进出洞府等瞬切前：清掉表现层走位，避Location 被错误吸附/summary>
        public void CancelPresentationMovementPublic(EntityId id) => CancelPresentationMovement(id);

        bool RegisterPendingNpcIntent(
            EntityId actor, EntityId npc, HostNpcArriveAction action,
            HostNpcApproachPolicy policy, float requiredRange, Vector3 targetPosition)
        {
            if (actor.IsNone)
                return false;
            _pendingNpcIntent[actor.Value] = new HostNpcArriveIntent(
                actor, npc, action, policy, requiredRange, targetPosition);
            return true;
        }

        float ResolveNpcRequiredRange(EntityId actor, HostNpcArriveAction action)
        {
            if (action == HostNpcArriveAction.Attack && bootstrap?.Session?.World != null &&
                bootstrap.Session.World.Entities.TryGet(actor, out var actorEntity))
                return Mathf.Max(.8f, new SpiritVeilService().ResolveEngageRange(actorEntity));
            return HostNpcInteraction.DefaultMeleeEngageRange;
        }

        void TickPendingNpcApproaches()
        {
            if (_pendingNpcIntent.Count == 0) return;
            _pendingNpcIntentKeys.Clear();
            foreach (var pair in _pendingNpcIntent) _pendingNpcIntentKeys.Add(pair.Key);
            for (var i = 0; i < _pendingNpcIntentKeys.Count; i++)
            {
                var actorId = new EntityId(_pendingNpcIntentKeys[i]);
                if (!_pendingNpcIntent.TryGetValue(actorId.Value, out var intent)) continue;
                if (!TryResolveApproachViews(intent, out var actorView, out var targetView))
                { CancelPendingNpcIntent(actorId); continue; }
                var distance = Vector2.Distance(actorView.transform.position, targetView.transform.position);
                if (distance <= intent.RequiredRange && IsCharacterStandClear(actorId))
                {
                    CancelPresentationMovement(actorId);
                    ApplyPendingNpcIntent(actorId);
                    continue;
                }
                var targetPosition = targetView.transform.position;
                targetPosition.z = HostPresentationSpace.EntityZ;
                var moved = Vector2.Distance(targetPosition, intent.LastTargetPosition);
                var needsRetry = intent.RepathState == "RepathFailed" ||
                                 intent.RepathState == "AwaitingMovingTargetRepath";
                if ((!needsRetry && moved < PursuitRepathDistance && IsMoving(actorId)) ||
                    Time.unscaledTime < intent.LastRepathTime + PursuitRepathCooldown)
                    continue;
                intent.LastTargetPosition = targetView.transform.position;
                intent.LastRepathTime = Time.unscaledTime;
                if (TryOrderCharacterApproach(actorId, intent.NpcId, intent.RequiredRange,
                        issueStop: false, preserveNpcIntent: true))
                    intent.RepathState = "RepathAccepted";
                else
                    intent.RepathState = "RepathFailed";
            }
        }

        bool TryResolveApproachViews(
            HostNpcArriveIntent intent, out EntityView actorView, out EntityView targetView)
        {
            actorView = targetView = null;
            var world = bootstrap?.Session?.World;
            if (intent == null || world == null || viewSpawner == null ||
                !world.Entities.TryGet(intent.ActorId, out var actor) || !CombatLifeStateService.CanFight(actor) ||
                !world.Entities.TryGet(intent.NpcId, out var target) ||
                !target.TryGet<LifecycleComponent>(out var targetLife) || targetLife.IsDead || targetLife.IsRemoved ||
                !viewSpawner.Registry.TryGet(intent.ActorId, out actorView) || actorView == null ||
                !viewSpawner.Registry.TryGet(intent.NpcId, out targetView) || targetView == null)
                return false;
            if (CharacterEncounterService.OwnsParticipantSpatialState(world, intent.ActorId) ||
                CharacterEncounterService.OwnsParticipantSpatialState(world, intent.NpcId))
                return false;
            var actorInSpace = SeparateSpaceTransitionService.IsOwnedByActiveSeparateSpace(world, intent.ActorId);
            var targetInSpace = SeparateSpaceTransitionService.IsOwnedByActiveSeparateSpace(world, intent.NpcId);
            return actorInSpace == targetInSpace;
        }

        void CancelPendingNpcIntent(EntityId actor)
        {
            if (actor.IsNone || !_pendingNpcIntent.TryGetValue(actor.Value, out var intent)) return;
            _pendingNpcIntent.Remove(actor.Value);
            CancelPresentationMovement(actor);
            if (intent.Policy == HostNpcApproachPolicy.Cooperative)
                ReleaseNpcForInteraction(intent.NpcId);
        }

        public string DescribePendingPersonAction()
        {
            foreach (var pair in _pendingNpcIntent)
            {
                var intent = pair.Value;
                var distance = -1f;
                if (viewSpawner != null && viewSpawner.Registry.TryGet(intent.ActorId, out var actor) && actor != null &&
                    viewSpawner.Registry.TryGet(intent.NpcId, out var target) && target != null)
                    distance = Vector2.Distance(actor.transform.position, target.transform.position);
                return "Actor=" + intent.ActorId + " Target=" + intent.NpcId +
                       " Action=" + intent.Action + " Policy=" + intent.Policy +
                       " Distance=" + distance.ToString("0.###") +
                       " RequiredRange=" + intent.RequiredRange.ToString("0.###") +
                       " Repath=" + intent.RepathState;
            }
            return "None";
        }

        public bool OrderPartyToPointThen(Vector3 point, PlayerCommandKind arriveCommand) =>
            OrderPartyToPoint(point, arriveCommand, null);

        public bool OrderPartyToPointThen(Vector3 point, PlayerCommandKind arriveCommand, string arriveLocationId) =>
            OrderPartyToPoint(point, arriveCommand, arriveLocationId);

        /// <summary>任意单位寻路移动（NPC 日程issueStop=false，避免冲掉其 Schedule 订单）/summary>
        public bool OrderEntityToWorldPoint(
            EntityId id,
            Vector3 point,
            PlayerCommandKind? arriveCommand,
            bool issueStop,
            string arriveLocationId = null,
            HostMoveCompletionPolicy completionPolicy = HostMoveCompletionPolicy.HoldStandby,
            bool exactGoal = false,
            bool preserveNpcIntent = false,
            float speedMultiplier = 1f,
            float freeStandRadius = 0f,
            bool stopBeforeCommit = false)
        {
            if (id.IsNone || viewSpawner == null ||
                !viewSpawner.Registry.TryGet(id, out var view) || view == null)
                return false;

            // 弥留／死亡／已移除：LocalMap 禁止移动（大地图宏观令另有门禁）
            if (bootstrap?.Session?.World != null &&
                bootstrap.Session.World.Entities.TryGet(id, out var lifeEnt) &&
                !CombatLifeStateService.CanFight(lifeEnt))
            {
                CancelPresentationMovement(id);
                return false;
            }

            if (stopBeforeCommit)
            {
                // A player repath must be planned from the visible foot point. Never move the
                // character to a grid centre as a side effect of a right click.
                var foot = view.transform.position;
                if (_walkGrid == null || !_walkGrid.TryWorldToCell(foot.x, foot.y, out var cx, out var cy) ||
                    !_walkGrid.IsWalkable(cx, cy)) return false;
            }
            else SnapOntoWalkableIfNeeded(view);
            var requestedCenter = point;
            freeStandRadius = float.IsNaN(freeStandRadius) || float.IsInfinity(freeStandRadius)
                ? 0f : Mathf.Max(0f, freeStandRadius);
            if (freeStandRadius > 0f &&
                !TryResolveFreeStandPoint(id, requestedCenter, freeStandRadius, out point))
                return false;
            if (!TryBuildWorldPath(view.transform.position, point, _wpScratch, exactGoal))
                return false;

            // Commit the player's Stop only after a replacement path exists. A blocked click
            // leaves the current route running rather than producing a visible stop/restart.
            if (stopBeforeCommit)
            {
                CancelSurfaceAutoTravelIfActive();
                StopOne(id);
            }
            bootstrap?.BreakthroughRitual?.NotifyMoveOrdered(id);
            bootstrap?.SkillStudyRitual?.NotifyMoveOrdered(id);

            ResumeTime();
            if (issueStop)
                StopOne(id);

            ClearPath(id, replacingPath: true);
            if (preserveNpcIntent)
            {
                _pendingOnArrive.Remove(id.Value);
                _pendingArriveLocation.Remove(id.Value);
                _pendingArriveActions.Remove(id.Value);
            }
            else ClearPending(id);
            var path = new List<Vector3>(_wpScratch.Count);
            path.AddRange(_wpScratch);
            _paths[id.Value] = path;
            ReserveCharacterDestination(id, path[path.Count - 1], requestedCenter, freeStandRadius);
            _pathIndex[id.Value] = 0;
            _moveSpeedMultiplierByEntity[id.Value] = SanitizeSpeedMultiplier(speedMultiplier);
            _completionPolicies[id.Value] = completionPolicy;
            if (!string.IsNullOrEmpty(_boundLocalMapId))
                _pathLocalMapIds[id.Value] = _boundLocalMapId;
            else
                _pathLocalMapIds.Remove(id.Value);
            _targets[view] = path[0];
            _movingIds.Add(id.Value);
            AdvancePresentationMovementRevision(id);
            view.SetActivityText("\u79fb\u52a8\u4e2d");
            if (arriveCommand.HasValue)
                _pendingOnArrive[id.Value] = arriveCommand.Value;
            if (!string.IsNullOrEmpty(arriveLocationId))
                _pendingArriveLocation[id.Value] = arriveLocationId;

            var pathLen = EstimatePathLength(path);
            if (issueStop)
                HoldPlayerWait(id, pathLen);
            return true;
        }

        void AdvancePresentationMovementRevision(EntityId id)
        {
            if (id.IsNone)
                return;
            _movementRevisionByEntity.TryGetValue(id.Value, out var revision);
            revision++;
            if (revision == 0u)
                revision = 1u;
            _movementRevisionByEntity[id.Value] = revision;
        }

        bool OrderPartyToPoint(Vector3 point, PlayerCommandKind? arriveCommand, string arriveLocationId = null)
        {
            var active = ResolveActiveCharacter();
            if (active.IsNone)
                return false;

            npcContextMenu?.CancelRecruitInteractionForActor(active);
            if (OrderEntityToWorldPoint(active, point, arriveCommand, issueStop: false, arriveLocationId,
                    freeStandRadius: arriveCommand.HasValue ? 0f : 1.25f,
                    stopBeforeCommit: true))
            {
                _playerPartyPathMoveIds.Add(active.Value);
                _playerPartyPathMoveSerial++;
                if (arriveCommand == null)
                {
                    NotifyMeleeDisengageForMove(active);
                    NotifyDestructibleDisengageForMove(active);
                    NotifyFarmLaborStopForMove(active);
                }

                return true;
            }

            return false;
        }

        EntityId ResolveActiveCharacter() =>
            HostPlayerMoveCommandGate.ResolveActiveForWorldMove(
                selectionController,
                bootstrap?.Session?.PlayerParty);

        /// <summary>
        /// Direct movement cancels Surface travel while preserving the current exact position.
        /// </summary>
        void CancelSurfaceAutoTravelIfActive()
        {
            var world = bootstrap?.Session?.World;
            var motion = world?.PlayerPartyTravel;
            if (!PlayerPartySurfaceTravelService.IsActiveSurfaceTravel(motion))
                return;
            PlayerPartyTravelRuntimeService.CancelTravel(world);
        }
        void NotifyMeleeDisengageForMove(EntityId id)
        {
            var melee = bootstrap != null
                ? bootstrap.GetComponent<HostNpcMeleeAssault>()
                : GetComponent<HostNpcMeleeAssault>();
            melee?.DisengageIfAttacker(id);
        }

        void NotifyDestructibleDisengageForMove(EntityId id)
        {
            if (bootstrap?.Session?.PlayerParty?.ActiveCharacterId == id)
            {
                bootstrap.GetComponent<HostControlCoreAssault>()?.Clear();
                bootstrap.GetComponent<HostFactionFlagAssault>()?.Clear();
            }
            var chop = bootstrap != null
                ? bootstrap.GetComponent<HostDestructibleAssault>()
                : GetComponent<HostDestructibleAssault>();
            chop?.DisengageIfAttacker(id);
        }

        void NotifyFarmLaborStopForMove(EntityId id)
        {
            var farm = bootstrap != null
                ? bootstrap.GetComponent<HostFarmFieldLabor>()
                : GetComponent<HostFarmFieldLabor>();
            farm?.Stop(id);
        }

        /// <summary>Direct WASD locomotion for Active only; cancels click-move path.</summary>
        public void TickDirectWasdMove(EntityId id, Vector2 dir, float speed)
        {
            if (id.IsNone || dir.sqrMagnitude < 0.0001f || viewSpawner == null ||
                !viewSpawner.Registry.TryGet(id, out var view) || view == null)
                return;

            if (bootstrap?.Session?.World != null &&
                bootstrap.Session.World.Entities.TryGet(id, out var ent) &&
                !CombatLifeStateService.CanFight(ent))
                return;

            if (_paths.ContainsKey(id.Value) || _targets.ContainsKey(view) ||
                _pendingNpcIntent.ContainsKey(id.Value) || _pendingOnArrive.ContainsKey(id.Value) ||
                _pendingArriveActions.ContainsKey(id.Value) || _pendingArriveLocation.ContainsKey(id.Value))
                ClearHostMove(id);
            ResumeTime();

            var dt = Time.deltaTime;
            var pos = view.transform.position;
            var startPos = pos;
            var dx = dir.x * speed * dt;
            var dy = dir.y * speed * dt;

            if (_walkGrid != null)
            {
                TryAxisStep(view, ref pos, dx, 0f);
                TryAxisStep(view, ref pos, 0f, dy);

                // 卡边缘保底：仅当 Active 当前 cell 本身不可走/出界（出生进了 blocked 格或透明
                // exit / grid 外）时才用 nearest-safe-walkable resolver 拉回最近合法 cell。
                // 正常撞墙（站在 walkable 格上被阻挡）不触发，避免按向障碍物时瞬移。
                if (!_walkGrid.TryWorldToCell(pos.x, pos.y, out var gx2, out var gy2))
                    SnapOntoWalkableIfNeeded(view);
                else if (!_walkGrid.IsWalkable(gx2, gy2))
                    SnapOntoWalkableIfNeeded(view);
            }
            else
            {
                pos.x += dx;
                pos.y += dy;
            }

            pos.z = HostPresentationSpace.EntityZ;
            pos = FilterCharacterStep(view, startPos, pos, pos,
                Mathf.Max(0f, dt), settling: false, requireWaypointConnection: false);
            view.transform.position = pos;
            view.RecordLocomotion(pos - startPos);
            CaptureIndependentTacticalPosition(view);
        }

        void TryAxisStep(EntityView view, ref Vector3 pos, float dx, float dy)
        {
            if (Mathf.Abs(dx) < 0.00001f && Mathf.Abs(dy) < 0.00001f)
                return;

            var next = pos;
            next.x += dx;
            next.y += dy;
            next.z = HostPresentationSpace.EntityZ;
            if (GridPathfinder.IsWorldSegmentWalkable(_walkGrid, pos.x, pos.y, next.x, next.y))
            {
                pos = next;
                return;
            }

            if (Mathf.Abs(dx) > 0.00001f)
            {
                next = pos;
                next.x += dx;
                if (GridPathfinder.IsWorldSegmentWalkable(_walkGrid, pos.x, pos.y, next.x, next.y))
                    pos = next;
            }

            if (Mathf.Abs(dy) > 0.00001f)
            {
                next = pos;
                next.y += dy;
                if (GridPathfinder.IsWorldSegmentWalkable(_walkGrid, pos.x, pos.y, next.x, next.y))
                    pos = next;
            }
        }

        /// <summary>
        /// Formation slots that land in blocked cells used to snap up to 8 cells away
        /// (looks like random detours / ignoring the click). Keep goals near the click.
        /// </summary>
        Vector3 ResolveFormationGoal(Vector3 click, Vector3 offset)
        {
            var goal = click + offset;
            if (_walkGrid == null)
                return goal;

            if (_walkGrid.TryWorldToCell(goal.x, goal.y, out var gx, out var gy) &&
                _walkGrid.IsWalkable(gx, gy))
                return goal;

            if (_walkGrid.TryWorldToCell(goal.x, goal.y, out gx, out gy) &&
                _walkGrid.TryFindNearestWalkable(gx, gy, 3, out var nx, out var ny))
            {
                _walkGrid.CellToWorldCenter(nx, ny, out var wx, out var wy);
                return new Vector3(wx, wy, goal.z);
            }

            if (_walkGrid.TryWorldToCell(click.x, click.y, out var cx, out var cy) &&
                _walkGrid.IsWalkable(cx, cy))
                return click;

            if (_walkGrid.TryWorldToCell(click.x, click.y, out cx, out cy) &&
                _walkGrid.TryFindNearestWalkable(cx, cy, 4, out nx, out ny))
            {
                _walkGrid.CellToWorldCenter(nx, ny, out var wx, out var wy);
                return new Vector3(wx, wy, click.z);
            }

            return click;
        }

        bool TryBuildWorldPath(Vector3 from, Vector3 to, List<Vector3> waypoints, bool exactGoal = false)
        {
            waypoints.Clear();
            if (_walkGrid == null)
            {
                Debug.LogWarning("[HostMove] WalkGrid missing; refusing an unvalidated straight-line move.", this);
                return false;
            }

            _pathScratch.Clear();
            if (!GridPathfinder.TryFindWorldPath(
                    _walkGrid, from.x, from.y, to.x, to.y, _pathScratch, 8, exactGoal ? 0 : 4))
                return false;

            for (var i = 0; i + 1 < _pathScratch.Count; i += 2)
            {
                waypoints.Add(new Vector3(
                    _pathScratch[i],
                    _pathScratch[i + 1],
                    HostPresentationSpace.EntityZ));
            }

            // A* emits the start cell centre as waypoint zero. When the exact current foot
            // point can see waypoint one, routing via that centre creates a short sideways
            // hook on every right-click retarget, especially at increased presentation speed.
            if (waypoints.Count > 1 && GridPathfinder.IsWorldSegmentWalkable(
                    _walkGrid, from.x, from.y, waypoints[1].x, waypoints[1].y))
                waypoints.RemoveAt(0);

            return waypoints.Count > 0;
        }

        static float EstimatePathLength(List<Vector3> path)
        {
            if (path == null || path.Count == 0)
                return 1f;
            var len = 0f;
            for (var i = 1; i < path.Count; i++)
                len += Vector3.Distance(path[i - 1], path[i]);
            return Mathf.Max(1f, len);
        }

        void HoldPlayerWait(EntityId id, float worldDistance)
        {
            var session = bootstrap?.Session;
            if (session?.Loop == null || id.IsNone)
                return;
            var seconds = worldDistance / Mathf.Max(0.5f, moveSpeed);
            var tickSeconds = bootstrap != null
                ? Mathf.Max(0.01f, bootstrap.SecondsPerAutoTickAt1x)
                : SimulationTickPacing.SecondsPerTickAt1x;
            var ticks = (ulong)Mathf.Clamp(Mathf.CeilToInt(seconds / tickSeconds) + 4, 6, 96);
            var wait = session.Loop.CreateWaitOrder(id, ticks, XianXia.Core.Orders.OrderSource.Player);
            session.Loop.EnqueueOrder(wait);
        }

        void TickMoves()
        {
            if (_targets.Count == 0)
                return;

            var dt = bootstrap != null ? bootstrap.PresentationDeltaTime : Time.unscaledDeltaTime;
            if (dt <= 0f)
                return;

            // 不能foreach Dictionary 时改 _targets（切下一航点会写入）
            _tickViews.Clear();
            foreach (var key in _targets.Keys)
                _tickViews.Add(key);
            _tickViews.Sort(CompareCharacterMoveViews);
            _tickDone.Clear();
            var views = _tickViews;
            var done = _tickDone;
            for (var vi = 0; vi < views.Count; vi++)
            {
                var view = views[vi];
                if (view == null || !_targets.TryGetValue(view, out var target))
                {
                    if (view != null)
                        done.Add(view);
                    continue;
                }

                // 途中倒下：立刻停
                if (bootstrap?.Session?.World != null &&
                    bootstrap.Session.World.Entities.TryGet(view.EntityId, out var movingEnt) &&
                    !CombatLifeStateService.CanFight(movingEnt))
                {
                    done.Add(view);
                    ClearPending(view.EntityId);
                    continue;
                }
                if (IsTransportAttached(view.EntityId, out _))
                {
                    done.Add(view);
                    ClearPending(view.EntityId);
                    continue;
                }
                if (IsCharacterSleeping(view.EntityId))
                {
                    done.Add(view);
                    ClearPending(view.EntityId);
                    continue;
                }

                RefreshFreeStandDestination(view, dt);
                if (!_targets.TryGetValue(view, out target)) continue;

                var pos = view.transform.position;
                if (_walkGrid == null || !GridPathfinder.IsWorldSegmentWalkable(
                        _walkGrid, pos.x, pos.y, target.x, target.y))
                {
                    Debug.LogWarning("[HostMoveBlocked] Current path leg became invalid. EntityId=" +
                                     view.EntityId.Value + " Position=" + pos.x.ToString("0.###") +
                                     "," + pos.y.ToString("0.###") + " Target=" +
                                     target.x.ToString("0.###") + "," + target.y.ToString("0.###") +
                                     " NavigationGeneration=" + _walkGridGeneration, this);
                    done.Add(view);
                    ClearPending(view.EntityId);
                    continue;
                }
                var multiplier = _moveSpeedMultiplierByEntity.TryGetValue(view.EntityId.Value, out var configured)
                    ? configured : 1f;
                var next = AdvanceAlongPath(view, pos, moveSpeed * multiplier * dt, dt);
                view.transform.position = next;
                view.RecordLocomotion(next - pos);
                CaptureIndependentTacticalPosition(view);
                if (!_targets.TryGetValue(view, out target))
                    continue;
                if ((next - target).sqrMagnitude > arriveEpsilon * arriveEpsilon)
                    continue;

                var idv = view.EntityId.Value;
                if (_paths.TryGetValue(idv, out var path) &&
                    _pathIndex.TryGetValue(idv, out var idx) &&
                    idx + 1 < path.Count)
                {
                    if ((next - target).sqrMagnitude > .0001f) continue;
                    _pathIndex[idv] = idx + 1;
                    _targets[view] = path[idx + 1];
                    continue;
                }

                if ((next - target).sqrMagnitude > .0001f || !IsCharacterStandClear(view.EntityId))
                    continue;

                done.Add(view);
            }

            for (var i = 0; i < done.Count; i++)
            {
                var view = done[i];
                _targets.Remove(view);
                if (view == null)
                    continue;
                view.SetActivityText(string.Empty);
                var id = view.EntityId;
                _movingIds.Remove(id.Value);
                _playerPartyPathMoveIds.Remove(id.Value);
                ClearPath(id);
                var completionPolicy = _completionPolicies.TryGetValue(id.Value, out var policy)
                    ? policy
                    : HostMoveCompletionPolicy.HoldStandby;
                _completionPolicies.Remove(id.Value);
                SyncLocation(view);
                if (_pendingNpcIntent.ContainsKey(id.Value))
                    ApplyPendingNpcIntent(id);
                else if (_pendingOnArrive.ContainsKey(id.Value))
                    ApplyPendingArrive(id);
                else if (_pendingArriveActions.ContainsKey(id.Value))
                    ApplyPendingArriveAction(id);
                else if (IsActiveMeleeAttacker(id))
                {
                    // 追击到位后不HoldStandby→Stop，否则会 Disengage 整场交战
                    view.SetActivityText("\u4ea4\u6218\u4e2d");
                }
                else if (IsFarmingUnit(id))
                {
                    // 田区走格：到位后不要 HoldStandby→Stop，否则会掐断农作
                }
                else if (completionPolicy == HostMoveCompletionPolicy.HoldStandby &&
                         selectionController != null && selectionController.IsPartyUnit(id))
                    HoldStandby(id);
            }
        }

        Vector3 AdvanceAlongPath(EntityView view, Vector3 start, float distanceBudget, float deltaTime)
        {
            var pos = start;
            var idv = view.EntityId.Value;
            var guard = 0;
            while (distanceBudget > .0001f && guard++ < 64 &&
                   _targets.TryGetValue(view, out var target))
            {
                var delta = target - pos;
                delta.z = 0f;
                var distance = delta.magnitude;
                var finalLeg = !_paths.TryGetValue(idv, out var currentPath) ||
                    !_pathIndex.TryGetValue(idv, out var currentIndex) || currentIndex + 1 >= currentPath.Count;
                if (distance <= arriveEpsilon)
                {
                    var nearTarget = Vector3.MoveTowards(pos, target, distanceBudget);
                    var beforeSnap = pos;
                    pos = FilterCharacterStep(view, pos, ClampToWalkable(pos, nearTarget), target,
                        deltaTime, settling: finalLeg && distance <= distanceBudget);
                    distanceBudget -= Vector3.Distance(beforeSnap, pos);
                    if ((pos - target).sqrMagnitude > .0001f)
                        break;
                    if (finalLeg && !IsCharacterStandClearAt(view, pos)) break;
                    if (_paths.TryGetValue(idv, out var path) &&
                        _pathIndex.TryGetValue(idv, out var index) && index + 1 < path.Count)
                    {
                        _pathIndex[idv] = index + 1;
                        _targets[view] = path[index + 1];
                        continue;
                    }
                    break;
                }

                var routeStep = Mathf.Min(distanceBudget, distance);
                var proposed = pos + delta / distance * routeStep;
                proposed.z = HostPresentationSpace.EntityZ;
                var moved = FilterCharacterStep(view, pos, ClampToWalkable(pos, proposed), target,
                    deltaTime, settling: finalLeg && distance <= routeStep + .0001f);
                var consumed = Vector3.Distance(pos, moved);
                pos = moved;
                // A slowed/blocked candidate spends this step's budget; never repeat tiny steps
                // in the same frame until the original full speed has been recovered.
                distanceBudget -= routeStep;
                if (consumed < .0001f)
                    break;
            }
            pos.z = HostPresentationSpace.EntityZ;
            return pos;
        }

        bool IsFarmingUnit(EntityId id)
        {
            if (id.IsNone)
                return false;
            var farm = bootstrap != null
                ? bootstrap.GetComponent<HostFarmFieldLabor>()
                : GetComponent<HostFarmFieldLabor>();
            return farm != null && farm.IsFarming(id);
        }

        bool IsActiveMeleeAttacker(EntityId id)
        {
            if (id.IsNone)
                return false;
            var melee = bootstrap != null
                ? bootstrap.GetComponent<HostNpcMeleeAssault>()
                : GetComponent<HostNpcMeleeAssault>();
            return melee != null && melee.IsFighting && melee.IsAttacker(id);
        }

        void ApplyPendingNpcIntent(EntityId id)
        {
            if (!_pendingNpcIntent.TryGetValue(id.Value, out var intent))
                return;
            if (!TryResolveApproachViews(intent, out var actorView, out var targetView))
            { CancelPendingNpcIntent(id); return; }
            if (Vector2.Distance(actorView.transform.position, targetView.transform.position) > intent.RequiredRange ||
                !IsCharacterStandClear(id))
            {
                intent.RepathState = "AwaitingMovingTargetRepath";
                intent.LastRepathTime = 0f;
                return;
            }
            _pendingNpcIntent.Remove(id.Value);
            StopOne(id);
            if (npcContextMenu == null)
                return;
            if (intent.Action == HostNpcArriveAction.Talk)
                npcContextMenu.OnNpcArriveTalk(id, intent.NpcId);
            else if (intent.Action == HostNpcArriveAction.Trade)
                npcContextMenu.OnNpcArriveTrade(id, intent.NpcId);
            else if (intent.Action == HostNpcArriveAction.Auction)
                npcContextMenu.OnNpcArriveAuction(id, intent.NpcId);
            else if (intent.Action == HostNpcArriveAction.Attack)
                npcContextMenu.OnNpcArriveAttack(id, intent.NpcId);
            else if (intent.Action == HostNpcArriveAction.Capture)
                npcContextMenu.OnNpcArriveCapture(id, intent.NpcId);
            else if (intent.Action == HostNpcArriveAction.Recruit)
                npcContextMenu.OnNpcArriveRecruit(id, intent.NpcId);
            else if (intent.Action == HostNpcArriveAction.Release)
                npcContextMenu.OnNpcArriveRelease(id, intent.NpcId);
            else if (intent.Action == HostNpcArriveAction.Execute)
                npcContextMenu.OnNpcArriveExecute(id, intent.NpcId);
        }

        void SnapOntoWalkableIfNeeded(EntityView view)
        {
            if (_walkGrid == null || view == null)
                return;
            var pos = view.transform.position;
            if (!_walkGrid.TryWorldToCell(pos.x, pos.y, out var cx, out var cy))
            {
                // Outside-grid 修复：角色出生在 WalkGrid 外（透明 exit / 边界外）时 TryWorldToCell
                // 返回 false，旧实现直接 return 导致永远没有 recovery。现在按 raw world index clamp
                // 到 grid 再搜最近 walkable。
                if (!TryResolveNearestWalkableWorldPoint(pos.x, pos.y, 12, out var recovered))
                    return;
                view.transform.position = new Vector3(recovered.x, recovered.y, HostPresentationSpace.EntityZ);
                CaptureIndependentTacticalPosition(view);
                return;
            }

            if (_walkGrid.IsWalkable(cx, cy))
                return;
            if (!_walkGrid.TryFindNearestWalkable(cx, cy, 12, out var nx, out var ny))
                return;
            _walkGrid.CellToWorldCenter(nx, ny, out var wx, out var wy);
            view.transform.position = new Vector3(wx, wy, HostPresentationSpace.EntityZ);
            CaptureIndependentTacticalPosition(view);
        }

        /// <summary>
        /// 通用 nearest-safe-walkable resolver：即使 world point 在 WalkGrid 外（raw index 超 bounds）
        /// 也能恢复 —— 先按 raw cell index clamp 到 grid 作为搜索起点，再 ring/BFS nearest walkable；
        /// 命中返回 cell center world 坐标。SafeInterior / exit-slot 过滤由调用方（Host repair）用
        /// 返回值再做最终判定。
        /// </summary>
        public bool TryResolveNearestWalkableWorldPoint(
            float worldX,
            float worldY,
            int maxRadius,
            out Vector3 worldPoint)
        {
            worldPoint = default;
            if (_walkGrid == null)
                return false;

            var rawX = (int)Math.Floor((worldX - _walkGrid.OriginX) / _walkGrid.CellSize);
            var rawY = (int)Math.Floor((worldY - _walkGrid.OriginY) / _walkGrid.CellSize);
            var cx = Mathf.Clamp(rawX, 0, _walkGrid.Width - 1);
            var cy = Mathf.Clamp(rawY, 0, _walkGrid.Height - 1);

            if (_walkGrid.IsWalkable(cx, cy))
            {
                _walkGrid.CellToWorldCenter(cx, cy, out var wx, out var wy);
                worldPoint = new Vector3(wx, wy, HostPresentationSpace.EntityZ);
                return true;
            }

            if (!_walkGrid.TryFindNearestWalkable(cx, cy, maxRadius, out var nx, out var ny))
                return false;
            _walkGrid.CellToWorldCenter(nx, ny, out var fx, out var fy);
            worldPoint = new Vector3(fx, fy, HostPresentationSpace.EntityZ);
            return true;
        }

        Vector3 ClampToWalkable(Vector3 from, Vector3 proposed)
        {
            if (_walkGrid == null)
                return proposed;

            if (GridPathfinder.IsWorldSegmentWalkable(
                    _walkGrid, from.x, from.y, proposed.x, proposed.y))
                return proposed;

            // Keep partial axis movement on the existing walkable geometry.
            var slideX = new Vector3(proposed.x, from.y, proposed.z);
            if (GridPathfinder.IsWorldSegmentWalkable(
                    _walkGrid, from.x, from.y, slideX.x, slideX.y))
                return slideX;

            var slideY = new Vector3(from.x, proposed.y, proposed.z);
            if (GridPathfinder.IsWorldSegmentWalkable(
                    _walkGrid, from.x, from.y, slideY.x, slideY.y))
                return slideY;

            return from;
        }

        void CaptureIndependentTacticalPosition(EntityView view)
        {
            if (view == null || bootstrap?.ContinuousOutdoorSurfaceRuntime == null)
                return;
            var world = bootstrap.Session?.World;
            if (world != null && world.Civilians.TryGet(view.EntityId, out var civilian) &&
                MortalCivilianMovementAuthority.CanCivilianOwnLocalMovement(world, view.EntityId, civilian))
            {
                bootstrap.ContinuousOutdoorSurfaceRuntime.CommitContinuousNpcPosition(
                    view.EntityId, view.transform.position);
                return;
            }
            bootstrap.ContinuousOutdoorSurfaceRuntime.TryCaptureIndependentParticipantPosition(
                view.EntityId, view.transform.position);
        }

        bool IsTransportAttached(EntityId id, out EntityId carrier)
        {
            carrier = EntityId.None;
            if (id.IsNone) return false;
            RefreshTransportState();
            if (!_carriedToCarrier.TryGetValue(id.Value, out var carrierValue)) return false;
            carrier = new EntityId(carrierValue);
            return true;
        }

        void RefreshTransportState()
        {
            var frame = Time.frameCount;
            if (_transportStateFrame == frame) return;
            _transportStateFrame = frame;
            _carriedToCarrier.Clear();
            var world = bootstrap?.Session?.World;
            if (world == null) return;
            foreach (var pair in world.Civilians.All)
            {
                var state = pair.Value;
                if (state == null) continue;
                if (state.RescueCarrying && !state.RescueTargetId.IsNone)
                    _carriedToCarrier[state.RescueTargetId.Value] = pair.Key.Value;
                if (state.Disposition == CivilianDisposition.CapturedEscorted && !state.CarrierId.IsNone)
                    _carriedToCarrier[pair.Key.Value] = state.CarrierId.Value;
            }
        }

        void ApplyPendingArriveAction(EntityId id)
        {
            if (!_pendingArriveActions.TryGetValue(id.Value, out var action))
                return;
            _pendingArriveActions.Remove(id.Value);
            // 先到站回调，Stop（路Traveling 的人跳过 Stop，避免打断宏观移动）
            action?.Invoke();
            if (bootstrap?.Session != null &&
                bootstrap.Session.World.WorldPresence.TryGet(id, out var p) &&
                p != null &&
                p.Mode == PartyWorldPresenceMode.AtSite)
                return;
            StopOne(id);
        }

        void ApplyPendingArrive(EntityId id)
        {
            if (!_pendingOnArrive.TryGetValue(id.Value, out var kind))
                return;
            _pendingOnArrive.Remove(id.Value);
            if (_pendingArriveLocation.TryGetValue(id.Value, out var forcedLoc))
            {
                _pendingArriveLocation.Remove(id.Value);
                if (!string.IsNullOrEmpty(forcedLoc))
                    ApplyPresentationArrival(bootstrap.Session, id, forcedLoc, bootstrap);
            }

            StopOne(id);

            if (kind == PlayerCommandKind.Labor)
            {
                var loop = bootstrap != null ? bootstrap.GetComponent<HostWorkLoop>() : null;
                loop?.StartLoop(id);
            }

            if (commandBridge != null)
            {
                var dur = kind == PlayerCommandKind.Stop || kind == PlayerCommandKind.UseConcealGrass
                    ? 0UL
                    : kind == PlayerCommandKind.Labor
                        ? commandBridge.GatherDurationTicks()
                        : HostCommandBridge.DefaultDurationTicks;
                commandBridge.IssueOne(id, kind, dur);
            }
            else if (bootstrap.Session?.Port != null)
            {
                bootstrap.Session.Port.Submit(
                    new PlayerCommandRequest(id, kind, HostCommandBridge.DefaultDurationTicks));
            }
        }

        void HoldStandby(EntityId id)
        {
            if (IsActiveMeleeAttacker(id))
                return;
            var session = bootstrap?.Session;
            if (session?.Loop == null || id.IsNone)
                return;
            StopOne(id);
            var wait = session.Loop.CreateWaitOrder(id, 96UL, XianXia.Core.Orders.OrderSource.Player);
            session.Loop.EnqueueOrder(wait);
            if (viewSpawner != null && viewSpawner.Registry.TryGet(id, out var view) && view != null)
                view.SetActivityText("待命");
        }

        void SyncLocation(EntityView view)
        {
            var session = bootstrap.Session;
            if (session == null || !session.World.Entities.TryGet(view.EntityId, out var entity))
                return;
            if (!entity.TryGet<EntityLocationComponent>(out var loc))
                return;

            if (CharacterEncounterService.BlocksOrdinaryContinuousSurface(session.World))
            {
                var tactical = HostPresentationSpace.ToPresentation(view.transform.position);
                loc.SetPresentationOverride(tactical.x, tactical.y);
                CaptureIndependentTacticalPosition(view);
                return;
            }
            var previous = loc.LocationId;
            var p = HostPresentationSpace.ToPresentation(view.transform.position);
            string best = null;
            var bestDist = HostZoneQuery.DefaultCenterRadius;
            var continuous = bootstrap.ContinuousOutdoorSurfaceRuntime != null &&
                             bootstrap.ContinuousOutdoorSurfaceRuntime.IsActive;
            IReadOnlyDictionary<string, WorldLocationState> locations = continuous
                ? session.World.ContinuousOutdoorMaterialization.PlacesByLocationId
                : session.World.LocalPlaces.Locations;
            foreach (var kv in locations)
            {
                // 洞内／地表切换后禁止吸附到另一张图的地点，否则离开时带不走人
                if (!continuous && !LocalMapVisibility.IsLocationOnActiveMap(session.World, kv.Value))
                    continue;
                var dx = kv.Value.PresentationX - p.x;
                var dy = kv.Value.PresentationZ - p.y;
                var d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = kv.Key;
                }
            }

            if (!string.IsNullOrEmpty(best))
            {
                if (!string.Equals(previous, best, System.StringComparison.Ordinal))
                    ApplyPresentationArrival(session, view.EntityId, best, bootstrap);
                else
                    loc.LocationId = best;
                // 记下当前表现坐标，进出图 Rebuild 时不再弹回地点中
                loc.SetPresentationOverride(p.x, p.y);
            }
            else if (session.World.LocalMap != null && session.World.LocalMap.IsInInterior)
            {
                // Separate Space：远离地点中心时仍写 local placement，供 Save capture。
                loc.SetPresentationOverride(p.x, p.y);
            }
            if (continuous)
            {
                loc.SetPresentationOverride(p.x, p.y);
                bootstrap.ContinuousOutdoorSurfaceRuntime.TryCaptureAtSiteAnchor(
                    view.EntityId, view.transform.position);
            }
        }

        public static void ApplyPresentationArrival(
            PlayableHostSession session,
            EntityId subject,
            string locationId,
            PlayableHostBootstrap host = null)
        {
            if (session == null || !session.IsInitialized || subject.IsNone ||
                string.IsNullOrEmpty(locationId))
                return;
            if (!session.World.Entities.TryGet(subject, out var entity) ||
                !entity.TryGet<EntityLocationComponent>(out var loc))
                return;

            var exploration = new ExplorationService();
            var arrived = exploration.NotifyArrived(session.World, subject, locationId, setLocation: true);
            if (arrived.IsFailure)
                loc.LocationId = locationId;

            var exploredFlag = ContentConditionEvaluator.ExploredFlag(locationId);
            if (!session.World.Flags.Has(exploredFlag) &&
                session.World.LocalPlaces.TryGet(locationId, out var place) &&
                !OpportunityEntranceRules.IsHiddenEntrance(place))
                exploration.ExploreHere(session.World, subject);

            if (host != null)
                host.DispatchDrainedEvents();
        }

        void ResumeTime()
        {
            if (bootstrap?.Session != null &&
                !bootstrap.Session.ModalHardPaused &&
                !bootstrap.Session.World.ContentEvents.HasActive)
                bootstrap.Session.ManualPaused = false;
        }

        void StopOne(EntityId id)
        {
            ClearHostMove(id);
            if (bootstrap.Session?.World?.QuestCompanions.TryGet(id,out _) == true)
            { bootstrap.Session.Loop.StopSubject(id); return; } // Internal follower cleanup, not a player command.
            if (commandBridge != null)
                commandBridge.IssueOne(id, PlayerCommandKind.Stop, 0);
            else if (bootstrap.Session?.Port != null)
                bootstrap.Session.Port.Submit(new PlayerCommandRequest(id, PlayerCommandKind.Stop, 0));
        }

        void ClearHostMove(EntityId id)
        {
            if (id.IsNone)
                return;
            ClearPending(id);
            ClearPath(id);
            _movingIds.Remove(id.Value);
            _playerPartyPathMoveIds.Remove(id.Value);
            if (viewSpawner != null &&
                viewSpawner.Registry.TryGet(id, out var view) &&
                view != null)
            {
                _targets.Remove(view);
                if (view.ActivityText == "\u79fb\u52a8\u4e2d")
                    view.SetActivityText(string.Empty);
            }
        }

        void ClearPending(EntityId id)
        {
            if (id.IsNone)
                return;
            if (_pendingNpcIntent.TryGetValue(id.Value, out var intent))
            {
                ReleaseNpcForInteraction(intent.NpcId);
                _pendingNpcIntent.Remove(id.Value);
            }

            _pendingOnArrive.Remove(id.Value);
            _pendingArriveLocation.Remove(id.Value);
            _pendingArriveActions.Remove(id.Value);
        }

        void ClearPath(EntityId id, bool replacingPath = false)
        {
            if (id.IsNone)
                return;
            _paths.Remove(id.Value);
            _pathIndex.Remove(id.Value);
            _pathLocalMapIds.Remove(id.Value);
            _moveSpeedMultiplierByEntity.Remove(id.Value);
            ReleaseCharacterDestination(id, preserveCrowdWait: replacingPath);
        }

        public float GetMoveSpeedMultiplier(EntityId id) =>
            !id.IsNone && _moveSpeedMultiplierByEntity.TryGetValue(id.Value, out var multiplier)
                ? multiplier : 1f;

        public float GetEffectiveMoveSpeed(EntityId id) => moveSpeed * GetMoveSpeedMultiplier(id);

        /// <summary>Presentation-only time step for gait filtering; never drives Core motion.</summary>
        public float GaitPresentationDeltaTime => bootstrap != null
            ? bootstrap.PresentationDeltaTime : Time.unscaledDeltaTime;

        static float SanitizeSpeedMultiplier(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) || value <= 0f ? 1f : value;

        Vector3 FormationOffset(int index, int count)
        {
            if (count <= 1)
                return Vector3.zero;
            var col = index % 3;
            var row = index / 3;
            return new Vector3((col - 1) * formationSpacing, -row * formationSpacing, 0f);
        }
    }
}
