using UnityEngine;
using XianXia.Core.Exploration;
using XianXia.Core.Input;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Demo [49] W work-target mode: yellow hint, LMB confirm work zone, RMB/Esc cancel.
    /// </summary>
    public sealed class HostWorkTargetMode : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] HostCommandBridge commandBridge;
        [SerializeField] HostMoveController moveController;
        [SerializeField] Camera worldCamera;
        [SerializeField] bool active;

        public bool IsActive => active;

        public void Bind(
            PlayableHostBootstrap host,
            HostSelectionController selection,
            HostCommandBridge bridge)
        {
            bootstrap = host;
            selectionController = selection;
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

            if (Input.GetKeyDown(KeyCode.W) && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt))
                active = true;

            if (!active)
                return;

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                active = false;
                return;
            }

            if (!Input.GetMouseButtonDown(0))
                return;

            if (!HostPresentationSpace.TryRaycastPlane(worldCamera, Input.mousePosition, out var point))
                return;

            var locId = FindWorkLocationNear(point);
            if (string.IsNullOrEmpty(locId))
                return;

            for (var i = 0; i < selectionController.State.Count; i++)
            {
                var id = selectionController.State.SelectedIds[i];
                if (!bootstrap.Session.World.Entities.TryGet(id, out var entity))
                    continue;
                if (entity.TryGet<EntityLocationComponent>(out var loc))
                    loc.LocationId = locId;
            }

            if (commandBridge != null)
                commandBridge.IssueSelected(PlayerCommandKind.Labor);
            active = false;
        }

        void OnGUI()
        {
            if (!active)
                return;
            GUI.Box(new Rect(12f, Screen.height - 48f, 360f, 32f), "工作指令：左键选择工区…（右键/Esc 取消）");
        }

        string FindWorkLocationNear(Vector3 worldPoint)
        {
            var p = HostPresentationSpace.ToPresentation(worldPoint);
            const float r = 2.2f;
            foreach (var kv in bootstrap.Session.World.WorldRegion.Locations)
            {
                var loc = kv.Value;
                if (string.IsNullOrEmpty(loc.ResourceOnExploreId) || loc.ResourceOnExploreAmount <= 0)
                    continue;
                var dx = loc.PresentationX - p.x;
                var dy = loc.PresentationZ - p.y;
                if (dx * dx + dy * dy <= r * r)
                    return loc.Id;
            }

            return null;
        }
    }
}
