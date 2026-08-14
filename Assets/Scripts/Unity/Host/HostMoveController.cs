using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Exploration;
using XianXia.Core.Input;
using XianXia.Core.Navigation;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// RTS 移动：沿 WalkGrid A* 航点行进；交互／修炼抵达后下令。支持 NPC 无 Stop 订单的走位。
    /// </summary>
    public sealed class HostMoveController : MonoBehaviour
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
        [SerializeField] float separationRadius = 0.95f;
        [SerializeField] float separationStrength = 2.2f;

        readonly Dictionary<EntityView, Vector3> _targets = new Dictionary<EntityView, Vector3>();
        readonly Dictionary<ulong, List<Vector3>> _paths = new Dictionary<ulong, List<Vector3>>();
        readonly Dictionary<ulong, int> _pathIndex = new Dictionary<ulong, int>();
        readonly Dictionary<ulong, PlayerCommandKind> _pendingOnArrive = new Dictionary<ulong, PlayerCommandKind>();
        readonly Dictionary<ulong, string> _pendingArriveLocation = new Dictionary<ulong, string>();
        readonly Dictionary<ulong, HostNpcArriveIntent> _pendingNpcIntent = new Dictionary<ulong, HostNpcArriveIntent>();
        readonly HashSet<ulong> _interactionHeldNpcs = new HashSet<ulong>();
        readonly HashSet<ulong> _movingIds = new HashSet<ulong>();
        readonly List<float> _pathScratch = new List<float>(64);
        readonly List<Vector3> _wpScratch = new List<Vector3>(32);

        WalkGrid _walkGrid;

        public bool IsMoving(EntityId id) => !id.IsNone && _movingIds.Contains(id.Value);

        public WalkGrid WalkGrid => _walkGrid;

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

        public void SetWalkGrid(WalkGrid grid) => _walkGrid = grid;

        void Update()
        {
            if (bootstrap == null || bootstrap.Session == null || !bootstrap.Session.IsInitialized)
                return;
            if (HostInputGate.BlockWorldInteraction)
                return;
            if (bootstrap.Session.World.ContentEvents.HasActive)
                return;
            if (bootstrap.ContentInterrupt != null && bootstrap.ContentInterrupt.HasBlockingInterrupt)
                return;
            if (npcContextMenu != null && npcContextMenu.IsOpen)
                return;
            if (worldCamera == null)
                worldCamera = Camera.main;
            if (worldCamera == null || selectionController == null || viewSpawner == null)
                return;

            var workMode = bootstrap.WorkTargetMode;
            if (workMode != null && workMode.IsActive)
            {
                TickMoves();
                return;
            }

            if (Input.GetMouseButtonDown(1) && !Input.GetKey(KeyCode.LeftAlt))
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

        public bool OrderActorToNpc(EntityId actor, EntityId npc, HostNpcArriveAction action)
        {
            if (actor.IsNone || npc.IsNone || viewSpawner == null ||
                !viewSpawner.Registry.TryGet(npc, out var npcView) || npcView == null)
                return false;

            var target = npcView.transform.position;
            if (viewSpawner.Registry.TryGet(actor, out var actorView) && actorView != null)
            {
                var delta = actorView.transform.position - target;
                delta.z = 0f;
                if (delta.sqrMagnitude > 0.01f)
                    target += delta.normalized * 1.1f;
            }

            target.z = HostPresentationSpace.EntityZ;
            HoldNpcForInteraction(npc);
            if (!OrderEntityToWorldPoint(actor, target, null, issueStop: true))
            {
                ReleaseNpcForInteraction(npc);
                return false;
            }

            return RegisterPendingNpcIntent(actor, npc, action);
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
                view.SetActivityText("稍候");
        }

        public void ReleaseNpcForInteraction(EntityId npc)
        {
            if (npc.IsNone || !_interactionHeldNpcs.Remove(npc.Value))
                return;
            if (viewSpawner != null &&
                viewSpawner.Registry.TryGet(npc, out var view) &&
                view != null)
                view.SetActivityText(string.Empty);
        }

        void CancelPresentationMovement(EntityId id)
        {
            if (id.IsNone)
                return;
            ClearPath(id);
            _movingIds.Remove(id.Value);
            if (viewSpawner == null)
                return;
            if (!viewSpawner.Registry.TryGet(id, out var view) || view == null)
                return;
            _targets.Remove(view);
        }

        bool RegisterPendingNpcIntent(EntityId actor, EntityId npc, HostNpcArriveAction action)
        {
            if (actor.IsNone)
                return false;
            _pendingNpcIntent[actor.Value] = new HostNpcArriveIntent(npc, action);
            return true;
        }

        public bool OrderPartyToPointThen(Vector3 point, PlayerCommandKind arriveCommand) =>
            OrderPartyToPoint(point, arriveCommand, null);

        public bool OrderPartyToPointThen(Vector3 point, PlayerCommandKind arriveCommand, string arriveLocationId) =>
            OrderPartyToPoint(point, arriveCommand, arriveLocationId);

        /// <summary>任意单位寻路移动（NPC 日程用 issueStop=false，避免冲掉其 Schedule 订单）。</summary>
        public bool OrderEntityToWorldPoint(
            EntityId id,
            Vector3 point,
            PlayerCommandKind? arriveCommand,
            bool issueStop,
            string arriveLocationId = null)
        {
            if (id.IsNone || viewSpawner == null ||
                !viewSpawner.Registry.TryGet(id, out var view) || view == null)
                return false;

            if (!TryBuildWorldPath(view.transform.position, point, _wpScratch))
                return false;

            ResumeTime();
            if (issueStop)
                StopOne(id);

            ClearPath(id);
            ClearPending(id);
            var path = new List<Vector3>(_wpScratch.Count);
            path.AddRange(_wpScratch);
            _paths[id.Value] = path;
            _pathIndex[id.Value] = 0;
            _targets[view] = path[0];
            _movingIds.Add(id.Value);
            view.SetActivityText("移动中");
            if (arriveCommand.HasValue)
                _pendingOnArrive[id.Value] = arriveCommand.Value;
            if (!string.IsNullOrEmpty(arriveLocationId))
                _pendingArriveLocation[id.Value] = arriveLocationId;

            var pathLen = EstimatePathLength(path);
            if (issueStop)
                HoldPlayerWait(id, pathLen);
            return true;
        }

        bool OrderPartyToPoint(Vector3 point, PlayerCommandKind? arriveCommand, string arriveLocationId = null)
        {
            if (selectionController == null || selectionController.State.Count == 0)
                return false;

            ResumeTime();
            if (commandBridge != null)
                commandBridge.IssueSelected(PlayerCommandKind.Stop, 0);
            else
                StopSelectedViaPort();

            var count = selectionController.State.Count;
            var moveIndex = 0;
            var moveCount = 0;
            for (var i = 0; i < count; i++)
            {
                if (selectionController.IsPartyUnit(selectionController.State.SelectedIds[i]))
                    moveCount++;
            }

            if (moveCount == 0)
                return false;

            var any = false;
            for (var i = 0; i < count; i++)
            {
                var id = selectionController.State.SelectedIds[i];
                if (!selectionController.IsPartyUnit(id))
                    continue;
                var offset = FormationOffset(moveIndex++, moveCount);
                if (OrderEntityToWorldPoint(id, point + offset, arriveCommand, issueStop: false, arriveLocationId))
                    any = true;
            }

            return any;
        }

        bool TryBuildWorldPath(Vector3 from, Vector3 to, List<Vector3> waypoints)
        {
            waypoints.Clear();
            if (_walkGrid == null)
            {
                waypoints.Add(new Vector3(to.x, to.y, HostPresentationSpace.EntityZ));
                return true;
            }

            _pathScratch.Clear();
            if (!GridPathfinder.TryFindWorldPath(
                    _walkGrid, from.x, from.y, to.x, to.y, _pathScratch))
                return false;

            for (var i = 0; i + 1 < _pathScratch.Count; i += 2)
            {
                waypoints.Add(new Vector3(
                    _pathScratch[i],
                    _pathScratch[i + 1],
                    HostPresentationSpace.EntityZ));
            }

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
            var tickSeconds = 3f;
            var ticks = (ulong)Mathf.Clamp(Mathf.CeilToInt(seconds / tickSeconds) + 4, 6, 96);
            var wait = session.Loop.CreateWaitOrder(id, ticks, XianXia.Core.Orders.OrderSource.Player);
            session.Loop.EnqueueOrder(wait);
        }

        void TickMoves()
        {
            if (_targets.Count == 0)
                return;

            // 不能在 foreach Dictionary 时改 _targets（切下一航点会写入）
            var views = new List<EntityView>(_targets.Keys);
            var done = new List<EntityView>();
            for (var vi = 0; vi < views.Count; vi++)
            {
                var view = views[vi];
                if (view == null || !_targets.TryGetValue(view, out var target))
                {
                    if (view != null)
                        done.Add(view);
                    continue;
                }

                var pos = view.transform.position;
                var sep = ComputeSeparation(view, pos);
                var desired = Vector3.MoveTowards(pos, target, moveSpeed * Time.unscaledDeltaTime);
                var next = desired + sep * (separationStrength * Time.unscaledDeltaTime);
                next.z = HostPresentationSpace.EntityZ;
                view.transform.position = next;

                if ((next - target).sqrMagnitude > arriveEpsilon * arriveEpsilon)
                    continue;

                var idv = view.EntityId.Value;
                if (_paths.TryGetValue(idv, out var path) &&
                    _pathIndex.TryGetValue(idv, out var idx) &&
                    idx + 1 < path.Count)
                {
                    _pathIndex[idv] = idx + 1;
                    _targets[view] = path[idx + 1];
                    continue;
                }

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
                ClearPath(id);
                SyncLocation(view);
                if (_pendingNpcIntent.ContainsKey(id.Value))
                    ApplyPendingNpcIntent(id);
                else if (_pendingOnArrive.ContainsKey(id.Value))
                    ApplyPendingArrive(id);
                else if (selectionController != null && selectionController.IsPartyUnit(id))
                    HoldStandby(id);
            }
        }

        void ApplyPendingNpcIntent(EntityId id)
        {
            if (!_pendingNpcIntent.TryGetValue(id.Value, out var intent))
                return;
            _pendingNpcIntent.Remove(id.Value);
            StopOne(id);
            if (npcContextMenu == null)
                return;
            if (intent.Action == HostNpcArriveAction.Talk)
                npcContextMenu.OnNpcArriveTalk(id, intent.NpcId);
            else
                npcContextMenu.OnNpcArriveAttack(id, intent.NpcId);
        }

        Vector3 ComputeSeparation(EntityView self, Vector3 pos)
        {
            if (viewSpawner == null)
                return Vector3.zero;
            var push = Vector3.zero;
            var r2 = separationRadius * separationRadius;
            foreach (var other in viewSpawner.Registry.All)
            {
                if (other == null || other == self || !other.IsBound)
                    continue;
                var d = pos - other.transform.position;
                d.z = 0f;
                var sq = d.sqrMagnitude;
                if (sq < 1e-6f || sq > r2)
                    continue;
                var dist = Mathf.Sqrt(sq);
                push += d / dist * (1f - dist / separationRadius);
            }

            return push;
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

            var previous = loc.LocationId;
            var p = HostPresentationSpace.ToPresentation(view.transform.position);
            string best = null;
            var bestDist = HostZoneQuery.DefaultCenterRadius;
            foreach (var kv in session.World.WorldRegion.Locations)
            {
                var dx = kv.Value.PresentationX - p.x;
                var dy = kv.Value.PresentationZ - p.y;
                var d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = kv.Key;
                }
            }

            if (string.IsNullOrEmpty(best))
                return;

            if (!string.Equals(previous, best, System.StringComparison.Ordinal))
                ApplyPresentationArrival(session, view.EntityId, best, bootstrap);
            else
                loc.LocationId = best;
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
            if (!session.World.Flags.Has(exploredFlag))
                exploration.ExploreHere(session.World, subject);

            if (host != null)
                host.DispatchDrainedEvents();
        }

        void ResumeTime()
        {
            if (bootstrap?.Session != null &&
                !bootstrap.Session.World.ContentEvents.HasActive)
                bootstrap.Session.IsPaused = false;
        }

        void StopOne(EntityId id)
        {
            if (commandBridge != null)
                commandBridge.IssueOne(id, PlayerCommandKind.Stop, 0);
            else if (bootstrap.Session?.Port != null)
                bootstrap.Session.Port.Submit(new PlayerCommandRequest(id, PlayerCommandKind.Stop, 0));
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
        }

        void ClearPath(EntityId id)
        {
            if (id.IsNone)
                return;
            _paths.Remove(id.Value);
            _pathIndex.Remove(id.Value);
        }

        void StopSelectedViaPort()
        {
            var session = bootstrap.Session;
            if (session?.Port == null)
                return;
            for (var i = 0; i < selectionController.State.Count; i++)
            {
                var id = selectionController.State.SelectedIds[i];
                if (selectionController.IsPartyUnit(id))
                    session.Port.Submit(new PlayerCommandRequest(id, PlayerCommandKind.Stop, 0));
            }
        }

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
