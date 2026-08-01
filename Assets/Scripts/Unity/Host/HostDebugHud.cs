using UnityEngine;
using XianXia.Core.Domain.Ids;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// VS0.4 Phase E: IMGUI debug HUD. Read-only; no Core mutation buttons.
    /// </summary>
    public sealed class HostDebugHud : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] bool visible = true;
        [SerializeField] KeyCode toggleVisibleKey = KeyCode.F1;

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
            if (multiplier == 1 || multiplier == 2 || multiplier == 5)
                _speedMultiplier = multiplier;
        }

        public void CycleSpeed()
        {
            _speedMultiplier = _speedMultiplier == 1 ? 2 : _speedMultiplier == 2 ? 5 : 1;
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

        void Update()
        {
            if (Input.GetKeyDown(toggleVisibleKey))
                visible = !visible;
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
            GUI.Box(rect, "PlayableHost HUD (F1)");
            GUI.Label(new Rect(rect.x + 8f, rect.y + 22f, rect.width - 16f, rect.height - 28f), text);

            const float tipH = 72f;
            var tip = new Rect(pad, rect.y - tipH - 6f, width, tipH);
            GUI.Box(tip, "Demo 0.1 路径");
            GUI.Label(
                new Rect(tip.x + 8f, tip.y + 20f, tip.width - 16f, tip.height - 24f),
                "Y旅行→洞口  T探索学机缘  4修炼突破  8/9/0分工  过日产资源  5帮助/7招募");
        }
    }
}
