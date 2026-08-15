using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Input;
using XianXia.Core.Settlement;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Reference：选中角色后的行动菜单（V 键或右上按钮区）。
    /// </summary>
    public sealed class HostActionMenu : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] HostCommandBridge commandBridge;
        [SerializeField] bool visible;
        [SerializeField] KeyCode toggleKey = KeyCode.V;

        string _status = "行动菜单 (V)";

        public void Bind(
            PlayableHostBootstrap host,
            HostSelectionController selection,
            HostCommandBridge bridge)
        {
            bootstrap = host;
            selectionController = selection;
            commandBridge = bridge;
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                visible = !visible;
        }

        void OnGUI()
        {
            if (!visible)
                return;
            if (bootstrap == null || bootstrap.Session == null || !bootstrap.Session.IsInitialized)
                return;

            var rect = new Rect(Screen.width - 210f, 120f, 200f, 400f);
            GUI.Box(rect, "角色指令 (V)");
            var y = rect.y + 28f;
            var x = rect.x + 10f;
            var w = 180f;

            if (Button(x, ref y, w, "劳动"))
                Issue(PlayerCommandKind.Labor);
            if (Button(x, ref y, w, "休息"))
                Issue(PlayerCommandKind.Rest);
            if (Button(x, ref y, w, "观察"))
                Issue(PlayerCommandKind.Observe);
            if (Button(x, ref y, w, "修炼"))
                Issue(PlayerCommandKind.Cultivate);
            if (Button(x, ref y, w, "探索(T)"))
                Issue(PlayerCommandKind.Explore);
            if (Button(x, ref y, w, "勘查(F7)"))
            {
                var survey = bootstrap != null ? bootstrap.CaveSurveyPresenter : null;
                if (survey != null)
                    survey.TrySurvey();
                else
                    _status = "无勘查组件";
            }

            if (Button(x, ref y, w, "离开洞窟"))
            {
                commandBridge?.IssueLeaveLocalMap();
                _status = commandBridge != null ? commandBridge.LastStatus : "无桥";
            }

            if (Button(x, ref y, w, "脱离战斗"))
            {
                var melee = bootstrap != null ? bootstrap.GetComponent<HostNpcMeleeAssault>() : null;
                if (melee != null && melee.IsFighting)
                {
                    melee.DisengageSelected(selectionController);
                    commandBridge?.IssueSelected(PlayerCommandKind.Stop, 0);
                    _status = "已脱离战斗";
                }
                else
                    _status = "当前无交战";
            }

            if (Button(x, ref y, w, "分工·劳动"))
                IssueAssign(WorkRoleKind.Labor);
            if (Button(x, ref y, w, "分工·采集"))
                IssueAssign(WorkRoleKind.Gather);
            if (Button(x, ref y, w, "分工·修炼"))
                IssueAssign(WorkRoleKind.Cultivate);

            GUI.Label(new Rect(x, y + 8f, w, 40f), _status);
        }

        static bool Button(float x, ref float y, float w, string label)
        {
            var hit = GUI.Button(new Rect(x, y, w, 26f), label);
            y += 30f;
            return hit;
        }

        void Issue(PlayerCommandKind kind)
        {
            var session = bootstrap.Session;
            if (selectionController == null || selectionController.State.Count == 0)
            {
                _status = "未选中角色";
                return;
            }

            var ok = 0;
            for (var i = 0; i < selectionController.State.Count; i++)
            {
                var id = selectionController.State.SelectedIds[i];
                var r = session.Port.Submit(new PlayerCommandRequest(id, kind, 4));
                if (r.IsSuccess)
                    ok++;
            }

            _status = kind + " ×" + ok;
        }

        void IssueAssign(WorkRoleKind role)
        {
            var session = bootstrap.Session;
            if (selectionController == null || selectionController.State.Count == 0)
            {
                _status = "未选中角色";
                return;
            }

            var id = selectionController.State.SelectedIds[0];
            var r = session.Port.Submit(new PlayerCommandRequest(
                id, PlayerCommandKind.AssignWork, 1, EntityId.None, role));
            _status = r.IsSuccess ? "分工 " + role : r.Error.ToString();
        }
    }
}
