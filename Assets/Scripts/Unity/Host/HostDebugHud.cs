using UnityEngine;
using XianXia.Core.Domain.Ids;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Simulation speed 状态真源 + 可选只读 Runtime 诊断（LevelTester）。
    /// </summary>
    public sealed class HostDebugHud : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] bool visible;

        int _speedMultiplier = 1;
        HostHudSnapshot _last = new HostHudSnapshot();

        public int SpeedMultiplier => _speedMultiplier;

        public HostHudSnapshot LastSnapshot => _last;

        public void Bind(PlayableHostBootstrap hostBootstrap, HostSelectionController selection)
        {
            bootstrap = hostBootstrap;
            selectionController = selection;
        }

        public void SetSpeedMultiplier(int multiplier)
        {
            if (multiplier != 1 && multiplier != 2 && multiplier != 5 && multiplier != 20)
                return;
            _speedMultiplier = multiplier;
            bootstrap?.ResetAutoTickAccumulator();
        }

        public void CycleSpeed()
        {
            _speedMultiplier = _speedMultiplier == 1 ? 2
                : _speedMultiplier == 2 ? 5
                : _speedMultiplier == 5 ? 20
                : 1;
            bootstrap?.ResetAutoTickAccumulator();
        }

        public HostHudSnapshot Refresh()
        {
            var focus = EntityId.None;
            var peer = EntityId.None;
            if (selectionController != null && selectionController.State.Count > 0)
            {
                focus = selectionController.State.SelectedIds[0];
                if (selectionController.State.Count > 1)
                    peer = selectionController.State.SelectedIds[1];
            }

            var session = bootstrap != null ? bootstrap.Session : null;
            _last = HostHudSnapshot.Capture(session, focus, _speedMultiplier, peer);
            return _last;
        }

        void OnGUI()
        {
            if (!visible)
                return;

            Refresh();
            var text = _last.ToDebugText();
            const float pad = 8f;
            var width = 460f;
            var height = 250f;
            var rect = new Rect(pad, Screen.height - height - pad, width, height);
            GUI.Box(rect, "Runtime Diagnostics");
            GUI.Label(new Rect(rect.x + 8f, rect.y + 22f, rect.width - 16f, rect.height - 28f), text);
        }
    }
}
