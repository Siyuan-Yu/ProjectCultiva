using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Exploration;
using XianXia.Core.Input;
using XianXia.Core.Navigation;
using XianXia.Core.World;

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
        [SerializeField] float separationRadius = 1.2f;
        [SerializeField] float separationStrength = 3.2f;
        [Tooltip("站定／工作中也轻轻推开，避免多人叠在同一点；仍可穿过彼此。")]
        [SerializeField] float idleSeparationStrength = 4.5f;
        [SerializeField] float hardOverlapRadius = 0.4f;
        [SerializeField] float maxSeparationSpeed = 5.5f;

        readonly Dictionary<EntityView, Vector3> _targets = new Dictionary<EntityView, Vector3>();
        readonly Dictionary<ulong, List<Vector3>> _paths = new Dictionary<ulong, List<Vector3>>();
        readonly Dictionary<ulong, int> _pathIndex = new Dictionary<ulong, int>();
        readonly Dictionary<ulong, PlayerCommandKind> _pendingOnArrive = new Dictionary<ulong, PlayerCommandKind>();
        readonly Dictionary<ulong, string> _pendingArriveLocation = new Dictionary<ulong, string>();
        readonly Dictionary<ulong, System.Action> _pendingArriveActions = new Dictionary<ulong, System.Action>();
        readonly Dictionary<ulong, HostNpcArriveIntent> _pendingNpcIntent = new Dictionary<ulong, HostNpcArriveIntent>();
        readonly HashSet<ulong> _interactionHeldNpcs = new HashSet<ulong>();
        readonly HashSet<ulong> _movingIds = new HashSet<ulong>();
        readonly List<float> _pathScratch = new List<float>(64);
        readonly List<Vector3> _wpScratch = new List<Vector3>(32);
        readonly List<EntityView> _crowdScratch = new List<EntityView>(64);

        WalkGrid _walkGrid;

        public bool IsMoving(EntityId id) => !id.IsNone && _movingIds.Contains(id.Value);

        public WalkGrid WalkGrid => _walkGrid;

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

        /// <summary>Build an A* world polyline without issuing a move order.</summary>
        public bool TryBuildPathPreview(Vector3 from, Vector3 to, List<Vector3> into) =>
            TryBuildWorldPath(from, to, into);

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
                TickIdleCrowdSpacing();
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
            TickIdleCrowdSpacing();
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

        /// <summary>单人寻路；抵达后调用 onArrive（用于入洞等）。</summary>
        public bool OrderEntityToWorldPointPublic(EntityId id, Vector3 point, System.Action onArrive)
        {
            if (!OrderEntityToWorldPoint(id, point, null, issueStop: true))
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

            var target = npcView.transform.position;
            if (viewSpawner.Registry.TryGet(actor, out var actorView) && actorView != null)
            {
                var delta = actorView.transform.position - target;
                delta.z = 0f;
                // 停在交战距离内侧；开斗气纱衣时用远程半径，避免白走贴脸
                var engage = HostNpcInteraction.DefaultMeleeEngageRange;
                if (action == HostNpcArriveAction.Attack &&
                    bootstrap?.Session?.World != null &&
                    bootstrap.Session.World.Entities.TryGet(actor, out var actorEnt))
                {
                    engage = new SpiritVeilService().ResolveEngageRange(actorEnt);
                }

                var stopShort = engage * 0.55f;
                if (delta.sqrMagnitude > 0.01f)
                    target += delta.normalized * stopShort;
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

            var mover = bootstrap != null
                ? bootstrap.GetComponent<HostNpcScheduleMover>()
                : GetComponent<HostNpcScheduleMover>();
            mover?.NotifyNpcReleased(npc);
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

        /// <summary>进出洞府等瞬切前：清掉表现层走位，避免 Location 被错误吸附。</summary>
        public void CancelPresentationMovementPublic(EntityId id) => CancelPresentationMovement(id);

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

            // 弥留／死亡／已移除：LocalMap 禁止移动（大地图宏观令另有门禁）
            if (bootstrap?.Session?.World != null &&
                bootstrap.Session.World.Entities.TryGet(id, out var lifeEnt) &&
                !CombatLifeStateService.CanFight(lifeEnt))
            {
                CancelPresentationMovement(id);
                return false;
            }

            SnapOntoWalkableIfNeeded(view);
            if (!TryBuildWorldPath(view.transform.position, point, _wpScratch))
                return false;

            bootstrap?.BreakthroughRitual?.NotifyMoveOrdered(id);
            bootstrap?.SkillStudyRitual?.NotifyMoveOrdered(id);

            ResumeTime();
            if (issueStop)
                StopOne(id);

            ClearPath(id);
            ClearPending(id);
            var path = new List<Vector3>(_wpScratch.Count);
            path.AddRange(_wpScratch);
            _paths[id.Value] = path;
            _pathIndex[id.Value] = 0;
            SnapOntoWalkableIfNeeded(view);
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
            var world = bootstrap?.Session?.World;
            for (var i = 0; i < count; i++)
            {
                var sid = selectionController.State.SelectedIds[i];
                if (!selectionController.IsPartyUnit(sid))
                    continue;
                if (world != null &&
                    world.Entities.TryGet(sid, out var ent) &&
                    !CombatLifeStateService.CanFight(ent))
                    continue;
                moveCount++;
                // Drop any in-flight Host path so an old waypoint cannot fight the new order.
                ClearHostMove(sid);
            }

            if (moveCount == 0)
                return false;

            var any = false;
            for (var i = 0; i < count; i++)
            {
                var id = selectionController.State.SelectedIds[i];
                if (!selectionController.IsPartyUnit(id))
                    continue;
                if (world != null &&
                    world.Entities.TryGet(id, out var ent) &&
                    !CombatLifeStateService.CanFight(ent))
                    continue;
                var offset = FormationOffset(moveIndex++, moveCount);
                var goal = ResolveFormationGoal(point, offset);
                if (OrderEntityToWorldPoint(id, goal, arriveCommand, issueStop: false, arriveLocationId))
                    any = true;
                else if (OrderEntityToWorldPoint(id, point, arriveCommand, issueStop: false, arriveLocationId))
                    any = true;
            }

            if (any && arriveCommand == null)
            {
                NotifyMeleeDisengageForPartyMove();
                NotifyDestructibleDisengageForPartyMove();
                NotifyFarmLaborStopForPartyMove();
            }

            return any;
        }

        void NotifyMeleeDisengageForPartyMove()
        {
            var melee = bootstrap != null
                ? bootstrap.GetComponent<HostNpcMeleeAssault>()
                : GetComponent<HostNpcMeleeAssault>();
            if (melee == null || selectionController == null)
                return;
            for (var i = 0; i < selectionController.State.Count; i++)
            {
                var id = selectionController.State.SelectedIds[i];
                if (selectionController.IsPartyUnit(id))
                    melee.DisengageIfAttacker(id);
            }
        }

        void NotifyDestructibleDisengageForPartyMove()
        {
            var chop = bootstrap != null
                ? bootstrap.GetComponent<HostDestructibleAssault>()
                : GetComponent<HostDestructibleAssault>();
            if (chop == null || selectionController == null)
                return;
            for (var i = 0; i < selectionController.State.Count; i++)
            {
                var id = selectionController.State.SelectedIds[i];
                if (selectionController.IsPartyUnit(id))
                    chop.DisengageIfAttacker(id);
            }
        }

        void NotifyFarmLaborStopForPartyMove()
        {
            var farm = bootstrap != null
                ? bootstrap.GetComponent<HostFarmFieldLabor>()
                : GetComponent<HostFarmFieldLabor>();
            if (farm == null || selectionController == null)
                return;
            for (var i = 0; i < selectionController.State.Count; i++)
            {
                var id = selectionController.State.SelectedIds[i];
                if (selectionController.IsPartyUnit(id))
                    farm.Stop(id);
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

        bool TryBuildWorldPath(Vector3 from, Vector3 to, List<Vector3> waypoints)
        {
            waypoints.Clear();
            if (_walkGrid == null)
            {
                Debug.LogWarning(
                    "[HostMove] WalkGrid missing — straight-line move (will ignore blocksMovement). " +
                    "Ensure PlayableHostBootstrap finished Initialize.",
                    this);
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

                // 途中倒下：立刻停步
                if (bootstrap?.Session?.World != null &&
                    bootstrap.Session.World.Entities.TryGet(view.EntityId, out var movingEnt) &&
                    !CombatLifeStateService.CanFight(movingEnt))
                {
                    done.Add(view);
                    ClearPending(view.EntityId);
                    continue;
                }

                var pos = view.transform.position;
                var sep = ComputeSeparation(view, pos);
                var desired = Vector3.MoveTowards(pos, target, moveSpeed * dt);
                var next = desired + ClampSeparationDelta(sep * (separationStrength * dt), dt);
                next.z = HostPresentationSpace.EntityZ;
                next = ClampToWalkable(pos, next);
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
                else if (_pendingArriveActions.ContainsKey(id.Value))
                    ApplyPendingArriveAction(id);
                else if (IsActiveMeleeAttacker(id))
                {
                    // 追击到位后不要 HoldStandby→Stop，否则会 Disengage 整场交战
                    view.SetActivityText("交战中");
                }
                else if (IsFarmingUnit(id))
                {
                    // 田区走格：到位后不要 HoldStandby→Stop，否则会掐断农作
                }
                else if (selectionController != null && selectionController.IsPartyUnit(id))
                    HoldStandby(id);
            }
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
            _pendingNpcIntent.Remove(id.Value);
            StopOne(id);
            if (npcContextMenu == null)
                return;
            if (intent.Action == HostNpcArriveAction.Talk)
                npcContextMenu.OnNpcArriveTalk(id, intent.NpcId);
            else
                npcContextMenu.OnNpcArriveAttack(id, intent.NpcId);
        }

        void SnapOntoWalkableIfNeeded(EntityView view)
        {
            if (_walkGrid == null || view == null)
                return;
            var pos = view.transform.position;
            if (!_walkGrid.TryWorldToCell(pos.x, pos.y, out var cx, out var cy))
                return;
            if (_walkGrid.IsWalkable(cx, cy))
                return;
            if (!_walkGrid.TryFindNearestWalkable(cx, cy, 12, out var nx, out var ny))
                return;
            _walkGrid.CellToWorldCenter(nx, ny, out var wx, out var wy);
            view.transform.position = new Vector3(wx, wy, HostPresentationSpace.EntityZ);
        }

        Vector3 ClampToWalkable(Vector3 from, Vector3 proposed)
        {
            if (_walkGrid == null)
                return proposed;

            if (_walkGrid.TryWorldToCell(proposed.x, proposed.y, out var nx, out var ny) &&
                _walkGrid.IsWalkable(nx, ny))
                return proposed;

            // Prefer axis slides so soft-separation cannot shove units into buildings.
            var slideX = new Vector3(proposed.x, from.y, proposed.z);
            if (_walkGrid.TryWorldToCell(slideX.x, slideX.y, out nx, out ny) &&
                _walkGrid.IsWalkable(nx, ny))
                return slideX;

            var slideY = new Vector3(from.x, proposed.y, proposed.z);
            if (_walkGrid.TryWorldToCell(slideY.x, slideY.y, out nx, out ny) &&
                _walkGrid.IsWalkable(nx, ny))
                return slideY;

            return from;
        }

        /// <summary>
        /// 站定单位软斥力：不挡路、可穿过，只避免完全叠在同一坐标。
        /// 正在寻路移动的单位已在 <see cref="TickMoves"/> 里推过，这里跳过以免加倍。
        /// </summary>
        void TickIdleCrowdSpacing()
        {
            if (viewSpawner == null)
                return;
            var dt = bootstrap != null ? bootstrap.PresentationDeltaTime : Time.unscaledDeltaTime;
            if (dt <= 0f)
                return;

            _crowdScratch.Clear();
            foreach (var view in viewSpawner.Registry.All)
            {
                if (view == null || !view.IsBound)
                    continue;
                if (_movingIds.Contains(view.EntityId.Value))
                    continue;
                _crowdScratch.Add(view);
            }

            for (var i = 0; i < _crowdScratch.Count; i++)
            {
                var view = _crowdScratch[i];
                if (view == null)
                    continue;
                var pos = view.transform.position;
                var sep = ComputeSeparation(view, pos);
                if (sep.sqrMagnitude < 1e-8f)
                    continue;

                var next = pos + ClampSeparationDelta(sep * (idleSeparationStrength * dt), dt);
                next.z = HostPresentationSpace.EntityZ;
                next = ClampToWalkable(pos, next);
                view.transform.position = next;
            }
        }

        Vector3 ClampSeparationDelta(Vector3 delta, float dt)
        {
            var max = Mathf.Max(0.1f, maxSeparationSpeed) * Mathf.Max(dt, 1e-4f);
            if (delta.sqrMagnitude <= max * max)
                return delta;
            return delta.normalized * max;
        }

        Vector3 ComputeSeparation(EntityView self, Vector3 pos)
        {
            if (viewSpawner == null)
                return Vector3.zero;
            var push = Vector3.zero;
            var r = Mathf.Max(0.2f, separationRadius);
            var r2 = r * r;
            var hard = Mathf.Clamp(hardOverlapRadius, 0.05f, r);
            var selfId = self != null ? self.EntityId.Value : 0UL;
            foreach (var other in viewSpawner.Registry.All)
            {
                if (other == null || other == self || !other.IsBound)
                    continue;
                var d = pos - other.transform.position;
                d.z = 0f;
                var sq = d.sqrMagnitude;
                if (sq > r2)
                    continue;

                // 完全重合时旧逻辑会跳过 → 永远叠在一起；用双方 id 生成稳定侧向推力。
                if (sq < 1e-6f)
                {
                    var otherId = other.EntityId.Value;
                    var h = unchecked((int)(selfId * 73856093UL ^ otherId * 19349663UL));
                    var ang = (h & 1023) * (Mathf.PI * 2f / 1024f);
                    d = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
                    push += d * 1.35f;
                    continue;
                }

                var dist = Mathf.Sqrt(sq);
                var w = 1f - dist / r;
                if (dist < hard)
                    w *= 1f + (hard - dist) / hard * 2.2f;
                push += d / dist * w;
            }

            return push;
        }

        void ApplyPendingArriveAction(EntityId id)
        {
            if (!_pendingArriveActions.TryGetValue(id.Value, out var action))
                return;
            _pendingArriveActions.Remove(id.Value);
            // 先到站回调，再 Stop（路上 Traveling 的人跳过 Stop，避免打断宏观移动）
            action?.Invoke();
            if (bootstrap?.Session != null &&
                bootstrap.Session.World.WorldPresence.TryGet(id, out var p) &&
                p != null &&
                p.Mode == PartyWorldPresenceMode.Traveling)
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

            var previous = loc.LocationId;
            var p = HostPresentationSpace.ToPresentation(view.transform.position);
            string best = null;
            var bestDist = HostZoneQuery.DefaultCenterRadius;
            foreach (var kv in session.World.WorldRegion.Locations)
            {
                // 洞内／地表切换后禁止吸附到另一张图的地点，否则离开时带不走人。
                if (!LocalMapVisibility.IsLocationOnActiveMap(session.World, kv.Value))
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
                // 记下当前表现坐标，进出图 Rebuild 时不再弹回地点中心
                loc.SetPresentationOverride(p.x, p.y);
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
                session.World.WorldRegion.TryGet(locationId, out var place) &&
                !OpportunityEntranceRules.IsHiddenEntrance(place))
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
            ClearHostMove(id);
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
            if (viewSpawner != null &&
                viewSpawner.Registry.TryGet(id, out var view) &&
                view != null)
            {
                _targets.Remove(view);
                if (view.ActivityText == "移动中")
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
