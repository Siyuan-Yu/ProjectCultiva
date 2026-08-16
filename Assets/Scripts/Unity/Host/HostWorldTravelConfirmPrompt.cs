using System.Collections.Generic;
using System.Text;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.World;

namespace XianXia.Unity.Host
{
    /// <summary>宏观出行确认：会打断当前行为。</summary>
    public sealed class HostWorldTravelConfirmPrompt : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;

        bool _open;
        readonly List<EntityId> _agents = new List<EntityId>(8);
        string _destNodeId = string.Empty;
        string _destName = string.Empty;
        string _bodyText = string.Empty;
        bool _holdingPause;
        GUIStyle _title;
        GUIStyle _body;
        Texture2D _px;

        static readonly Color Parchment = new Color(0.92f, 0.86f, 0.74f, 0.98f);
        static readonly Color ParchmentDark = new Color(0.70f, 0.58f, 0.42f, 1f);

        public bool IsOpen => _open;

        public void Bind(PlayableHostBootstrap host) => bootstrap = host;

        public void ClearSessionState()
        {
            _open = false;
            _agents.Clear();
            _destNodeId = string.Empty;
            ReleasePause();
        }

        public void Open(IReadOnlyList<EntityId> agents, string destNodeId, string destName)
        {
            _agents.Clear();
            if (agents != null)
            {
                for (var i = 0; i < agents.Count; i++)
                {
                    if (!agents[i].IsNone)
                        _agents.Add(agents[i]);
                }
            }

            if (_agents.Count == 0 || string.IsNullOrEmpty(destNodeId) || bootstrap?.Session == null)
                return;

            _destNodeId = destNodeId;
            _destName = string.IsNullOrEmpty(destName) ? destNodeId : destName;
            _bodyText = BuildBody(bootstrap.Session.World);
            _open = true;
        }

        public void Close()
        {
            _open = false;
            ReleasePause();
        }

        string BuildBody(XianXia.Core.Simulation.SimulationWorld world)
        {
            var sb = new StringBuilder();
            sb.Append("是否让「");
            for (var i = 0; i < _agents.Count; i++)
            {
                if (i > 0)
                    sb.Append(i == _agents.Count - 1 ? "和" : "、");
                sb.Append(EntityLabel(world, _agents[i]));
            }

            sb.Append("」前往「").Append(_destName).Append("」？\n\n");
            sb.Append("确认后会打断他们当前行为：\n");
            sb.Append("· 若人在当前场景 → 先走到地图边缘再离开\n");
            sb.Append("· 随后在大地图上慢慢移动；途中无法进入其所在场景");
            return sb.ToString();
        }

        static string EntityLabel(XianXia.Core.Simulation.SimulationWorld world, EntityId id)
        {
            if (!world.Entities.TryGet(id, out var e) || e == null)
                return id.Value.ToString();
            if (!string.IsNullOrWhiteSpace(e.DisplayName))
                return e.DisplayName;
            return e.DefinitionId.ToString();
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
            GUI.depth = -120;

            var w = 480f;
            var h = 240f;
            var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            HostUiHitTest.Block(rect);
            Fill(rect, Parchment);
            DrawFrame(rect, ParchmentDark);

            GUI.Label(new Rect(rect.x + 16f, rect.y + 14f, rect.width - 32f, 28f), "宏观出行", _title);
            GUI.Label(new Rect(rect.x + 16f, rect.y + 48f, rect.width - 32f, 120f), _bodyText, _body);

            var yes = new Rect(rect.x + 70f, rect.yMax - 48f, 120f, 32f);
            var no = new Rect(rect.xMax - 190f, rect.yMax - 48f, 120f, 32f);
            if (HostImguiStyles.ParchmentBtn(yes, "确认出发"))
            {
                Event.current.Use();
                var agents = new List<EntityId>(_agents);
                var dest = _destNodeId;
                Close();
                bootstrap.WorldTravelDeparture?.BeginDeparture(agents, dest);
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
