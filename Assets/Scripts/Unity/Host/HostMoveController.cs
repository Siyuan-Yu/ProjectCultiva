using UnityEngine;
using XianXia.Core.Exploration;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Reference RTS：右键地面移动选中单位；靠近地点圆心时同步 EntityLocation。
    /// </summary>
    public sealed class HostMoveController : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] EntityViewSpawner viewSpawner;
        [SerializeField] Camera worldCamera;
        [SerializeField] float moveSpeed = 6f;
        [SerializeField] float arriveLocationRadius = 1.6f;

        readonly System.Collections.Generic.Dictionary<EntityView, Vector3> _targets =
            new System.Collections.Generic.Dictionary<EntityView, Vector3>();

        public void Bind(
            PlayableHostBootstrap host,
            HostSelectionController selection,
            EntityViewSpawner spawner)
        {
            bootstrap = host;
            selectionController = selection;
            viewSpawner = spawner;
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
            var ray = worldCamera.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out var enter))
                return;
            var hitPoint = ray.GetPoint(enter);
            var point = new Vector3(hitPoint.x, 0.5f, hitPoint.z);
            for (var i = 0; i < selectionController.State.Count; i++)
            {
                var id = selectionController.State.SelectedIds[i];
                if (!viewSpawner.Registry.TryGet(id, out var view) || view == null)
                    continue;
                _targets[view] = point;
            }
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
                next.y = 0.5f;
                view.transform.position = next;
                if ((next - target).sqrMagnitude < 0.04f)
                {
                    done.Add(view);
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

            var p = view.transform.position;
            string best = null;
            var bestDist = arriveLocationRadius;
            foreach (var kv in session.World.WorldRegion.Locations)
            {
                var dx = kv.Value.PresentationX - p.x;
                var dz = kv.Value.PresentationZ - p.z;
                var d = Mathf.Sqrt(dx * dx + dz * dz);
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
