using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 斗气纱衣：由 FormalHud 底栏 F2／按钮切换；本组件负责空灵力自动卸与 Toast。
    /// </summary>
    public sealed class HostSpiritVeilController : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] EntityViewSpawner viewSpawner;

        readonly SpiritVeilService _veil = new SpiritVeilService();

        public void Bind(PlayableHostBootstrap host)
        {
            bootstrap = host;
            if (host == null)
                return;
            selectionController = host.GetComponent<HostSelectionController>();
            viewSpawner = host.ViewSpawner;
        }

        public SpiritVeilService Service => _veil;

        public bool TryToggleSelected(out string message)
        {
            message = null;
            if (selectionController == null || selectionController.State.Count == 0)
            {
                message = "未选中单位";
                return false;
            }

            var id = selectionController.State.SelectedIds[0];
            return TryToggle(id, out message);
        }

        public bool TryToggle(EntityId id, out string message)
        {
            message = null;
            var world = bootstrap?.Session?.World;
            if (world == null || id.IsNone)
            {
                message = "无效";
                return false;
            }

            if (!world.Entities.TryGet(id, out var entity) ||
                (entity.Tags & EntityTag.Npc) != 0)
            {
                message = "仅己方可召唤";
                return false;
            }

            if (SpiritVeilService.IsActive(entity))
            {
                var off = _veil.TryDeactivate(world, id, "manual");
                message = off.IsSuccess ? "收起斗气纱衣" : off.Error.Message;
                if (off.IsSuccess)
                    RefreshActivity(id, false);
                return off.IsSuccess;
            }

            var on = _veil.TryActivate(world, id);
            message = on.IsSuccess
                ? "展开斗气纱衣（普攻远程）"
                : on.Error.Message;
            if (on.IsSuccess)
            {
                RefreshActivity(id, true);
                bootstrap?.DispatchDrainedEvents();
            }

            return on.IsSuccess;
        }

        void Update()
        {
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;
            if (bootstrap.Session.IsPaused)
                return;

            TickSpiritEmptyDismiss();
        }

        void TickSpiritEmptyDismiss()
        {
            var world = bootstrap.Session.World;
            foreach (var e in world.Entities.All)
            {
                if (e == null || !SpiritVeilService.IsActive(e))
                    continue;
                if (!_veil.DeactivateIfSpiritEmpty(e))
                    continue;
                RefreshActivity(e.Id, false);
                Toast(e.Id, "灵力耗尽，纱衣散去", new Color(0.75f, 0.85f, 1f));
            }
        }

        void RefreshActivity(EntityId id, bool active)
        {
            if (viewSpawner == null || id.IsNone)
                return;
            if (!viewSpawner.Registry.TryGet(id, out var view) || view == null)
                return;
            if (active)
                view.SetActivityText(SpiritVeilRules.DisplayName);
            else if (view.ActivityText == SpiritVeilRules.DisplayName)
                view.SetActivityText(string.Empty);
        }

        public void ToastToggleResult(EntityId id, bool ok, string message)
        {
            if (string.IsNullOrEmpty(message) || id.IsNone)
                return;
            Toast(id, message, ok
                ? new Color(0.55f, 0.9f, 1f)
                : new Color(1f, 0.55f, 0.4f));
        }

        void Toast(EntityId id, string text, Color color)
        {
            var overlay = bootstrap != null ? bootstrap.GetComponent<HostFeedbackOverlay>() : null;
            if (overlay == null || viewSpawner == null || id.IsNone)
                return;
            overlay.SpawnAtEntity(viewSpawner, id, text, color);
        }
    }
}
