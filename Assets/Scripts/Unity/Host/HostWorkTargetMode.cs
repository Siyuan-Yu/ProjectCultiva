using UnityEngine;
using XianXia.Core.Exploration;
using XianXia.Core.Input;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// RTS 点选工区／灵地：W 或底栏「劳役／入定」进入；左键确认目标区。
    /// </summary>
    public sealed class HostWorkTargetMode : MonoBehaviour
    {
        public enum ArmKind
        {
            None = 0,
            Labor = 1,
            Cultivate = 2
        }

        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] HostCommandBridge commandBridge;
        [SerializeField] HostMoveController moveController;
        [SerializeField] Camera worldCamera;
        [SerializeField] ArmKind armed;

        public bool IsActive => armed != ArmKind.None;

        public void Bind(
            PlayableHostBootstrap host,
            HostSelectionController selection,
            HostCommandBridge bridge)
        {
            bootstrap = host;
            selectionController = selection;
            commandBridge = bridge;
            moveController = host != null ? host.GetComponent<HostMoveController>() : moveController;
            if (worldCamera == null)
                worldCamera = Camera.main;
        }

        public void ArmLabor() => armed = ArmKind.Labor;

        public void ArmCultivate() => armed = ArmKind.Cultivate;

        public void Cancel() => armed = ArmKind.None;

        void Update()
        {
            if (bootstrap == null || bootstrap.Session == null || !bootstrap.Session.IsInitialized)
                return;
            if (bootstrap.Session.World.ContentEvents.HasActive)
            {
                armed = ArmKind.None;
                return;
            }

            if (worldCamera == null)
                worldCamera = Camera.main;
            if (moveController == null && bootstrap != null)
                moveController = bootstrap.GetComponent<HostMoveController>();

            if (Input.GetKeyDown(KeyCode.W) && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt))
                armed = ArmKind.Labor;

            if (armed == ArmKind.None)
                return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                armed = ArmKind.None;
                return;
            }

            // 右键在点选模式下＝取消（避免与移动抢）。
            if (Input.GetMouseButtonDown(1))
            {
                armed = ArmKind.None;
                return;
            }

            if (!Input.GetMouseButtonDown(0))
                return;

            // 点在 UI 上时不吃世界点选（粗略：屏幕下缘角色板）。
            if (Input.mousePosition.y < 220f && Screen.width > 0)
            {
                var mx = Input.mousePosition.x;
                if (mx > Screen.width * 0.2f && mx < Screen.width * 0.8f)
                    return;
            }

            if (!HostPresentationSpace.TryRaycastPlane(worldCamera, Input.mousePosition, out var point))
                return;

            var world = bootstrap.Session.World;
            if (armed == ArmKind.Labor)
            {
                var locId = HostZoneQuery.FindWorkLocation(world, point);
                if (string.IsNullOrEmpty(locId))
                    return;
                if (moveController != null)
                    moveController.OrderPartyToLocation(locId, PlayerCommandKind.Labor);
                else
                    FallbackSnapAndIssue(locId, PlayerCommandKind.Labor);
                armed = ArmKind.None;
                return;
            }

            if (armed == ArmKind.Cultivate)
            {
                var locId = HostZoneQuery.FindCultivateLocation(world, point);
                if (string.IsNullOrEmpty(locId))
                    return;
                if (moveController != null)
                    moveController.OrderPartyToLocation(locId, PlayerCommandKind.Cultivate);
                else
                    FallbackSnapAndIssue(locId, PlayerCommandKind.Cultivate);
                armed = ArmKind.None;
            }
        }

        void FallbackSnapAndIssue(string locId, PlayerCommandKind kind)
        {
            for (var i = 0; i < selectionController.State.Count; i++)
            {
                var id = selectionController.State.SelectedIds[i];
                if (!selectionController.IsPartyUnit(id))
                    continue;
                if (!bootstrap.Session.World.Entities.TryGet(id, out var entity))
                    continue;
                if (entity.TryGet<EntityLocationComponent>(out var loc))
                    loc.LocationId = locId;
            }

            bootstrap.Session.IsPaused = false;
            if (commandBridge != null)
                commandBridge.IssueSelected(kind);
        }

        void OnGUI()
        {
            if (armed == ArmKind.None)
                return;
            var msg = armed == ArmKind.Labor
                ? "劳役点选：左键农田／树林／药田…（右键/Esc 取消）"
                : "修炼点选：左键灵泉／洞府…（右键/Esc 取消）";
            GUI.Box(new Rect(12f, Screen.height - 48f, 420f, 32f), msg);
        }
    }
}
