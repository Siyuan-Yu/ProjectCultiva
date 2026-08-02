using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Exploration;
using XianXia.Core.Input;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// RTS：右键地面／区域＝只移动（打断当前活）；不会因走到工区／灵地就自动劳动或入定。
    /// 显式点选劳动／入定（W 或底栏）时，才在抵达后开工。
    /// </summary>
    public sealed class HostMoveController : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] EntityViewSpawner viewSpawner;
        [SerializeField] HostCommandBridge commandBridge;
        [SerializeField] Camera worldCamera;
        [SerializeField] float moveSpeed = 6f;
        [SerializeField] float arriveEpsilon = 0.2f;
        [SerializeField] float formationSpacing = 1.25f;

        readonly Dictionary<EntityView, Vector3> _targets = new Dictionary<EntityView, Vector3>();
        readonly Dictionary<ulong, PlayerCommandKind> _pendingOnArrive = new Dictionary<ulong, PlayerCommandKind>();
        readonly HashSet<ulong> _movingIds = new HashSet<ulong>();

        public bool IsMoving(EntityId id) => !id.IsNone && _movingIds.Contains(id.Value);

        public void Bind(
            PlayableHostBootstrap host,
            HostSelectionController selection,
            EntityViewSpawner spawner,
            HostCommandBridge bridge = null)
        {
            bootstrap = host;
            selectionController = selection;
            viewSpawner = spawner;
            commandBridge = bridge;
            if (worldCamera == null)
                worldCamera = Camera.main;
        }

        void Update()
        {
            if (bootstrap == null || bootstrap.Session == null || !bootstrap.Session.IsInitialized)
                return;
            if (bootstrap.Session.World.ContentEvents.HasActive)
                return;
            if (bootstrap.ContentInterrupt != null && bootstrap.ContentInterrupt.HasBlockingInterrupt)
                return;
            if (worldCamera == null)
                worldCamera = Camera.main;
            if (worldCamera == null || selectionController == null || viewSpawner == null)
                return;

            var workMode = bootstrap.WorkTargetMode;
            if (workMode != null && workMode.IsActive)
                return;

            if (Input.GetMouseButtonDown(1) && !Input.GetKey(KeyCode.LeftAlt))
            {
                if (HostUiHitTest.ContainsScreenPoint(Input.mousePosition))
                    return;
                IssueMoveToMouse();
            }

            TickMoves();
        }

        void IssueMoveToMouse()
        {
            if (!HostPresentationSpace.TryRaycastPlane(worldCamera, Input.mousePosition, out var point))
                return;

            // 右键只移动到鼠标点，绝不因「到了农田／树林／灵泉」自动开工。
            OrderPartyToPoint(point, null);
        }

        /// <summary>选中己方走到地点中心，抵达后下达 arriveCommand（可空＝只移动）。</summary>
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
            if (focus.IsNone || bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return false;
            if (!HostZoneQuery.TryGetLocationCenter(bootstrap.Session.World, locationId, out var center))
                return false;
            if (viewSpawner == null || !viewSpawner.Registry.TryGet(focus, out var view) || view == null)
                return false;

            ResumeTime();
            StopOne(focus);
            ClearPending(focus);
            HoldPlayerWait(focus, Vector3.Distance(
                view.transform.position, center));
            _targets[view] = center;
            _movingIds.Add(focus.Value);
            view.SetActivityText("移动中");
            if (arriveCommand.HasValue)
                _pendingOnArrive[focus.Value] = arriveCommand.Value;
            return true;
        }

        bool OrderPartyToPoint(Vector3 point, PlayerCommandKind? arriveCommand)
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

            for (var i = 0; i < count; i++)
            {
                var id = selectionController.State.SelectedIds[i];
                if (!selectionController.IsPartyUnit(id))
                    continue;
                if (!viewSpawner.Registry.TryGet(id, out var view) || view == null)
                    continue;

                ClearPending(id);
                var offset = FormationOffset(moveIndex++, moveCount);
                var target = point + offset;
                HoldPlayerWait(id, Vector3.Distance(view.transform.position, target));
                _targets[view] = target;
                _movingIds.Add(id.Value);
                view.SetActivityText("移动中");
                if (arriveCommand.HasValue)
                    _pendingOnArrive[id.Value] = arriveCommand.Value;
            }

            return true;
        }

        /// <summary>
        /// 表现移动期间塞 Player Wait，挡住 Schedule 立刻把角色拉回休息／劳动（否则像粘住）。
        /// </summary>
        void HoldPlayerWait(EntityId id, float worldDistance)
        {
            var session = bootstrap?.Session;
            if (session?.Loop == null || id.IsNone)
                return;
            var seconds = worldDistance / Mathf.Max(0.5f, moveSpeed);
            var tickSeconds = bootstrap != null ? 3f : 3f;
            // Bootstrap 默认约 3s/tick；移动按现实秒估算，再加缓冲。
            var ticks = (ulong)Mathf.Clamp(Mathf.CeilToInt(seconds / tickSeconds) + 4, 6, 48);
            var wait = session.Loop.CreateWaitOrder(id, ticks, XianXia.Core.Orders.OrderSource.Player);
            session.Loop.EnqueueOrder(wait);
        }

        void TickMoves()
        {
            if (_targets.Count == 0)
                return;

            var done = new List<EntityView>();
            foreach (var kv in _targets)
            {
                var view = kv.Key;
                if (view == null)
                {
                    done.Add(view);
                    continue;
                }

                var target = kv.Value;
                var pos = view.transform.position;
                var next = Vector3.MoveTowards(pos, target, moveSpeed * Time.unscaledDeltaTime);
                next.z = HostPresentationSpace.EntityZ;
                view.transform.position = next;
                if ((next - target).sqrMagnitude <= arriveEpsilon * arriveEpsilon)
                    done.Add(view);
            }

            for (var i = 0; i < done.Count; i++)
            {
                var view = done[i];
                _targets.Remove(view);
                if (view == null)
                    continue;
                view.SetActivityText(string.Empty);
                _movingIds.Remove(view.EntityId.Value);
                SyncLocation(view);
                if (_pendingOnArrive.ContainsKey(view.EntityId.Value))
                    ApplyPendingArrive(view.EntityId);
                else
                    HoldStandby(view.EntityId);
            }
        }

        void ApplyPendingArrive(EntityId id)
        {
            if (!_pendingOnArrive.TryGetValue(id.Value, out var kind))
                return;
            _pendingOnArrive.Remove(id.Value);

            // 先停掉移动用的 Wait，再下劳动／入定（仅显式点选工区时才会走到这里）
            StopOne(id);

            if (commandBridge != null)
            {
                var dur = kind == PlayerCommandKind.Stop || kind == PlayerCommandKind.UseConcealGrass
                    ? 0UL
                    : HostCommandBridge.DefaultDurationTicks;
                commandBridge.IssueOne(id, kind, dur);
            }
            else if (bootstrap.Session?.Port != null)
            {
                bootstrap.Session.Port.Submit(
                    new PlayerCommandRequest(id, kind, HostCommandBridge.DefaultDurationTicks));
            }
        }

        /// <summary>抵达后待命：挡住日程立刻塞劳动／休息，直到玩家再下令。</summary>
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
            {
                ApplyPresentationArrival(session, view.EntityId, best, bootstrap);
            }
            else
            {
                loc.LocationId = best;
            }
        }

        /// <summary>
        /// 表现层换区：NotifyArrived + 首次入区自动勘察（RTS 右键进区即可推进探索类任务／事件）。
        /// </summary>
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
            if (!id.IsNone)
                _pendingOnArrive.Remove(id.Value);
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
