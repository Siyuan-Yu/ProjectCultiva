using UnityEngine;
using XianXia.Core.Simulation;

namespace XianXia.Unity.Host
{
    public sealed partial class HostFormalHud
    {
        const string ControlsPause = "Host.ControlsSettings";
        bool _controlsOpen;
        PlayableHostSession _controlsSession;
        void OpenControlSettings(PlayableHostSession session)
        {
            _controlsSession = session; _controlsOpen = true;
            session.AcquireModalPause(ControlsPause);
            HostInputGate.AcquireInteraction(ControlsPause);
        }
        void CloseControlSettings()
        {
            _controlsOpen = false;
            _controlsSession?.ReleaseModalPause(ControlsPause); _controlsSession = null;
            HostInputGate.ReleaseInteraction(ControlsPause);
            bootstrap?.GetComponent<HostDirectionalRangedAttack>()?.RequireFreshPress();
        }
        bool DrawControlSettings(PlayableHostSession session)
        {
            if (!_controlsOpen) return false;
            if (!ReferenceEquals(session, _controlsSession)) { CloseControlSettings(); return false; }
            var width = Mathf.Min(520f, Screen.width - 16);
            var box = new Rect((Screen.width - width) / 2, (Screen.height - 238) / 2, width, 238);
            HostUiHitTest.Block(new Rect(0, 0, Screen.width, Screen.height));
            Fill(box, Parchment); DrawFrame(box, ParchmentDark);
            GUI.Label(new Rect(box.x + 16, box.y + 10, width - 32, 28), "操作设置（本机保存）", _parchmentTitle);
            var scene = GUI.Toggle(new Rect(box.x + 16, box.y + 48, width - 32, 28), HostRpgControls.SceneRts, "启用场景 RTS 操作");
            HostRpgControls.SetSceneRts(bootstrap, scene);
            var map = GUI.Toggle(new Rect(box.x + 16, box.y + 82, width - 32, 28), HostRpgControls.MapClickTravel, "启用大地图点击赶路");
            HostRpgControls.SetMapClickTravel(bootstrap, map);
            GUI.Label(new Rect(box.x + 16, box.y + 120, width - 32, 68),
                "关闭场景RTS：WASD移动，战斗左键/长按普攻，F近处交互。\n地图赶路独立保留，按WASD接管。\n非战斗左键查看，右键近处菜单保留。", _parchmentBody);
            if (GUI.Button(new Rect(box.x + 16, box.yMax - 42, width - 32, 30), "关闭") ||
                Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                CloseControlSettings();
            return true;
        }
        void OnDisable() => CloseControlSettings();
    }
}
