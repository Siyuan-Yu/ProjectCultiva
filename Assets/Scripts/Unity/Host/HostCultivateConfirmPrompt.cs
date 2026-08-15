using UnityEngine;
using XianXia.Core.Content;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Input;
using XianXia.Core.Social;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// F6／指令钮：就地确认是否打坐（不点选修炼点）。确认后持续入定。
    /// </summary>
    public sealed class HostCultivateConfirmPrompt : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] HostCommandBridge commandBridge;

        bool _open;
        EntityId _subject = EntityId.None;
        bool _holdingPause;
        GUIStyle _title;
        GUIStyle _body;
        Texture2D _px;

        static readonly Color Parchment = new Color(0.92f, 0.86f, 0.74f, 0.98f);
        static readonly Color ParchmentDark = new Color(0.70f, 0.58f, 0.42f, 1f);

        public bool IsOpen => _open;

        public void Bind(
            PlayableHostBootstrap host,
            HostSelectionController selection,
            HostCommandBridge bridge)
        {
            bootstrap = host;
            selectionController = selection;
            commandBridge = bridge;
        }

        public void ClearSessionState()
        {
            _open = false;
            _subject = EntityId.None;
            ReleasePause();
        }

        public void OpenFor(EntityId id)
        {
            if (id.IsNone)
                return;
            _subject = id;
            _open = true;
        }

        public void Close()
        {
            _open = false;
            ReleasePause();
        }

        void Update()
        {
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;
            if (!_open)
            {
                ReleasePause();
                return;
            }

            HostInputGate.BlockWorldCamera = true;
            HostInputGate.BlockWorldInteraction = true;
            if (!_holdingPause)
            {
                bootstrap.Session.IsPaused = true;
                _holdingPause = true;
            }
        }

        void ReleasePause()
        {
            if (!_holdingPause)
                return;
            _holdingPause = false;
            if (bootstrap?.Session != null)
                bootstrap.Session.IsPaused = false;
            HostInputGate.Clear();
        }

        void OnGUI()
        {
            if (!_open || bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;
            EnsureStyles();

            if (_subject.IsNone ||
                !bootstrap.Session.World.Entities.TryGet(_subject, out var entity))
            {
                Close();
                return;
            }

            var name = string.IsNullOrEmpty(entity.DisplayName) ? _subject.ToString() : entity.DisplayName;
            var rate = CultivationProgressRules.BaseProgressPerTick;
            if (entity.TryGet<PersonalityProfileComponent>(out var profile))
                rate += TalentGrowthRules.ExtraCultivateProgress(profile);

            var place = "当前位置";
            if (entity.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var loc) &&
                loc.HasLocation &&
                bootstrap.Session.World.WorldRegion.TryGet(loc.LocationId, out var site) &&
                !string.IsNullOrEmpty(site.Name))
                place = site.Name;

            var w = 420f;
            var h = 200f;
            var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            HostUiHitTest.Block(rect);
            Fill(rect, Parchment);
            DrawFrame(rect, ParchmentDark);

            GUI.Label(new Rect(rect.x + 16f, rect.y + 14f, rect.width - 32f, 28f), "就地打坐", _title);
            GUI.Label(
                new Rect(rect.x + 16f, rect.y + 48f, rect.width - 32f, 80f),
                "是否让「" + name + "」在「" + place + "」打坐修炼？\n\n" +
                "修炼速度：约每 5 游戏分钟 +" + rate + " 修为\n" +
                "（受倍速加速；地点／天气修正以后再加）",
                _body);

            var yes = new Rect(rect.x + 70f, rect.yMax - 48f, 110f, 32f);
            var no = new Rect(rect.xMax - 180f, rect.yMax - 48f, 110f, 32f);
            if (HostImguiStyles.ParchmentBtn(yes, "确认打坐"))
            {
                Event.current.Use();
                var id = _subject;
                Close();
                if (commandBridge != null)
                {
                    if (selectionController != null && !selectionController.State.Contains(id))
                        selectionController.SelectEntity(id, false);
                    commandBridge.IssueOne(id, PlayerCommandKind.Cultivate, HostCommandBridge.DefaultDurationTicks);
                }
            }

            if (HostImguiStyles.ParchmentBtn(no, "取消"))
            {
                Event.current.Use();
                Close();
            }
        }

        void EnsureStyles()
        {
            if (_title != null)
                return;
            _px = Texture2D.whiteTexture;
            _title = HostImguiStyles.InkLabel(18, bold: true);
            _body = HostImguiStyles.InkLabel(13, wordWrap: true);
        }

        void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _px);
            GUI.color = prev;
        }

        void DrawFrame(Rect r, Color c)
        {
            Fill(new Rect(r.x, r.y, r.width, 1f), c);
            Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), c);
            Fill(new Rect(r.x, r.y, 1f, r.height), c);
            Fill(new Rect(r.xMax - 1f, r.y, 1f, r.height), c);
        }
    }
}
