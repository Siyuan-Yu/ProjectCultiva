using System.Text;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Social;

namespace XianXia.Unity.Host
{
    /// <summary>人物关系面板：打开时暂停。</summary>
    public sealed class HostRelationPanel : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] bool open;

        EntityId _subject = EntityId.None;
        bool _holdingPause;
        Vector2 _scroll;
        GUIStyle _title;
        GUIStyle _body;
        Texture2D _px;

        static readonly Color Parchment = new Color(0.92f, 0.86f, 0.74f, 0.98f);
        static readonly Color ParchmentDark = new Color(0.70f, 0.58f, 0.42f, 1f);

        public bool IsOpen => open;

        public void Bind(PlayableHostBootstrap host) => bootstrap = host;

        public void ClearSessionState()
        {
            open = false;
            _subject = EntityId.None;
            _holdingPause = false;
            HostInputGate.Clear();
        }

        public void OpenFor(EntityId id)
        {
            if (id.IsNone) return;
            _subject = id;
            open = true;
            _scroll = Vector2.zero;
        }

        public void Close() => open = false;

        void Update()
        {
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;
            if (bootstrap.QuestJournal != null && bootstrap.QuestJournal.IsOpen)
            {
                open = false;
                ReleasePause();
                return;
            }

            if (open)
            {
                HostInputGate.BlockWorldCamera = true;
                HostInputGate.BlockWorldInteraction = true;
                if (!_holdingPause)
                {
                    bootstrap.Session.IsPaused = true;
                    _holdingPause = true;
                }
            }
            else
                ReleasePause();
        }

        void ReleasePause()
        {
            if (!_holdingPause) return;
            _holdingPause = false;
            if (bootstrap?.Session != null)
                bootstrap.Session.IsPaused = false;
            HostInputGate.Clear();
        }

        void OnGUI()
        {
            if (!open || bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;
            EnsureStyles();
            if (_subject.IsNone ||
                !bootstrap.Session.World.Entities.TryGet(_subject, out var self))
            {
                open = false;
                return;
            }

            var name = string.IsNullOrEmpty(self.DisplayName) ? _subject.ToString() : self.DisplayName;
            var w = Mathf.Min(520f, Screen.width - 40f);
            var h = Mathf.Min(420f, Screen.height - 40f);
            var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            HostUiHitTest.Block(rect);
            Fill(rect, Parchment);
            DrawFrame(rect, ParchmentDark);

            GUI.Label(new Rect(rect.x + 16f, rect.y + 12f, rect.width - 90f, 28f), "关系 · " + name, _title);
            if (HostImguiStyles.ParchmentBtn(new Rect(rect.xMax - 72f, rect.y + 10f, 56f, 28f), "关闭"))
                open = false;

            var text = BuildRelationText(self);
            var body = new Rect(rect.x + 16f, rect.y + 48f, rect.width - 32f, rect.height - 64f);
            var viewH = Mathf.Max(body.height, _body.CalcHeight(new GUIContent(text), body.width - 18f) + 8f);
            _scroll = GUI.BeginScrollView(body, _scroll, new Rect(0f, 0f, body.width - 18f, viewH));
            GUI.Label(new Rect(0f, 0f, body.width - 18f, viewH), text, _body);
            GUI.EndScrollView();
        }

        string BuildRelationText(XianXia.Core.Entities.Entity self)
        {
            var sb = new StringBuilder(256);
            if (!self.TryGet<RelationshipComponent>(out var rel))
                return "无关系数据";

            var n = 0;
            foreach (var e in bootstrap.Session.World.Entities.All)
            {
                if (e.Id == self.Id)
                    continue;
                if (!rel.TryGetCachedToward(e.Id, out var score))
                    continue;
                var nm = string.IsNullOrEmpty(e.DisplayName) ? e.Id.ToString() : e.DisplayName;
                sb.Append("· ").Append(nm).Append("　").Append(score).Append('\n');
                if (++n >= 40)
                    break;
            }

            return n == 0 ? "暂无显著关系" : sb.ToString();
        }

        void EnsureStyles()
        {
            if (_title != null) return;
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
