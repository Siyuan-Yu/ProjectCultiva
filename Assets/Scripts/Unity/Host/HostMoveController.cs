using UnityEngine;
using XianXia.Core.Exploration;
using XianXia.Core.Input;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Demo-aligned：右键地面移动（XY）；下令前 Stop 当前 Core Action。
    /// </summary>
    public sealed class HostMoveController : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] EntityViewSpawner viewSpawner;
        [SerializeField] HostCommandBridge commandBridge;
        [SerializeField] Camera worldCamera;
        [SerializeField] float moveSpeed = 6f;
        [SerializeField] float arriveLocationRadius = 1.6f;
        [SerializeField] float formationSpacing = 1.25f;

        readonly System.Collections.Generic.Dictionary<EntityView, Vector3> _targets =
            new System.Collections.Generic.Dictionary<EntityView, Vector3>();

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
            if (worldCamera == null)
                worldCamera = Camera.main;
            if (worldCamera == null || selectionController == null || viewSpawner == null)
                return;

            if (Input.GetMouseButtonDown(1) && !Input.GetKey(KeyCode.LeftAlt))
                IssueMoveToMouse();

            TickMoves();
        }

        void IssueMoveToMouse()
        {
            if (!HostPresentationSpace.TryRaycastPlane(worldCamera, Input.mousePosition, out var point))
                return;

            // Cancel active Core actions before presentation move ([49] interrupt).
            if (commandBridge != null)
                commandBridge.IssueSelected(PlayerCommandKind.Stop, 0);
            else
                StopSelectedViaPort();

            var workHere = FindWorkLocationNear(point);
            var cultivateHere = FindCultivateLocationNear(point);

            var count = selectionController.State.Count;
            var moveIndex = 0;
            var moveCount = 0;
            for (var i = 0; i < count; i++)
            {
                if (selectionController.IsPartyUnit(selectionController.State.SelectedIds[i]))
                    moveCount++;
            }

            for (var i = 0; i < count; i++)
            {
                var id = selectionController.State.SelectedIds[i];
                // Demo: only the three controllable party units accept move orders.
                if (!selectionController.IsPartyUnit(id))
                    continue;
                if (!viewSpawner.Registry.TryGet(id, out var view) || view == null)
                    continue;
                var offset = FormationOffset(moveIndex++, moveCount);
                _targets[view] = point + offset;
                view.SetActivityText("移动中");
            }

            // Demo: right-click work／spirit zone → sync location then order (approach continues in view).
            if (!string.IsNullOrEmpty(workHere))
            {
                SnapSelectionLocation(workHere);
                if (commandBridge != null)
                    commandBridge.IssueSelected(PlayerCommandKind.Labor);
            }
            else if (!string.IsNullOrEmpty(cultivateHere))
            {
                SnapSelectionLocation(cultivateHere);
                if (commandBridge != null)
                    commandBridge.IssueSelected(PlayerCommandKind.Cultivate);
            }
        }

        void SnapSelectionLocation(string locationId)
        {
            for (var i = 0; i < selectionController.State.Count; i++)
            {
                var id = selectionController.State.SelectedIds[i];
                if (!selectionController.IsPartyUnit(id))
                    continue;
                if (!bootstrap.Session.World.Entities.TryGet(id, out var entity))
                    continue;
                if (!entity.TryGet<EntityLocationComponent>(out var loc))
                    continue;
                loc.LocationId = locationId;
            }
        }

        string FindWorkLocationNear(Vector3 worldPoint)
        {
            var p = HostPresentationSpace.ToPresentation(worldPoint);
            foreach (var kv in bootstrap.Session.World.WorldRegion.Locations)
            {
                var loc = kv.Value;
                if (string.IsNullOrEmpty(loc.ResourceOnExploreId) || loc.ResourceOnExploreAmount <= 0)
                    continue;
                var dx = loc.PresentationX - p.x;
                var dy = loc.PresentationZ - p.y;
                if (dx * dx + dy * dy <= arriveLocationRadius * arriveLocationRadius)
                    return loc.Id;
            }

            return null;
        }

        string FindCultivateLocationNear(Vector3 worldPoint)
        {
            var p = HostPresentationSpace.ToPresentation(worldPoint);
            foreach (var kv in bootstrap.Session.World.WorldRegion.Locations)
            {
                var loc = kv.Value;
                if (loc.Kind != LocationKind.Opportunity)
                    continue;
                var dx = loc.PresentationX - p.x;
                var dy = loc.PresentationZ - p.y;
                if (dx * dx + dy * dy <= arriveLocationRadius * arriveLocationRadius)
                    return loc.Id;
            }

            return null;
        }

        void StopSelectedViaPort()
        {
            var session = bootstrap.Session;
            if (session?.Port == null)
                return;
            for (var i = 0; i < selectionController.State.Count; i++)
            {
                var id = selectionController.State.SelectedIds[i];
                session.Port.Submit(new PlayerCommandRequest(id, PlayerCommandKind.Stop, 0));
            }
        }

        static Vector3 FormationOffset(int index, int count)
        {
            if (count <= 1)
                return Vector3.zero;
            var col = index % 3;
            var row = index / 3;
            return new Vector3((col - 1) * 1.25f, -row * 1.25f, 0f);
        }

        void TickMoves()
        {
            if (_targets.Count == 0)
                return;

            var done = new System.Collections.Generic.List<EntityView>();
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
                if ((next - target).sqrMagnitude < 0.04f)
                {
                    done.Add(view);
                    view.SetActivityText(string.Empty);
                    SyncLocation(view);
                }
            }

            for (var i = 0; i < done.Count; i++)
                _targets.Remove(done[i]);
        }

        void SyncLocation(EntityView view)
        {
            var session = bootstrap.Session;
            if (session == null || !session.World.Entities.TryGet(view.EntityId, out var entity))
                return;
            if (!entity.TryGet<EntityLocationComponent>(out var loc))
                return;

            var p = HostPresentationSpace.ToPresentation(view.transform.position);
            string best = null;
            var bestDist = arriveLocationRadius;
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

            if (!string.IsNullOrEmpty(best))
                loc.LocationId = best;
        }
    }
}
