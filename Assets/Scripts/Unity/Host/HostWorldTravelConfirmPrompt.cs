using System.Collections.Generic;
using System.Text;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>宏观出行确认：会打断当前行为。</summary>
    public sealed class HostWorldTravelConfirmPrompt : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;

        bool _open;
        readonly List<EntityId> _agents = new List<EntityId>(8);
        WorldTravelTarget _target;
        string _destName = string.Empty;
        string _pursueStackId = string.Empty;
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
            _target = default;
            _pursueStackId = string.Empty;
            ReleasePause();
        }

        public void Open(IReadOnlyList<EntityId> agents, string destNodeId, string destName)
        {
            OpenInternal(agents, WorldTravelTarget.AtNode(destNodeId), destName, string.Empty);
        }

        public void OpenTarget(IReadOnlyList<EntityId> agents, WorldTravelTarget target, string destName = null)
        {
            OpenInternal(agents, target, destName, string.Empty);
        }

        public void OpenForStackPursuit(
            IReadOnlyList<EntityId> agents,
            ArmyStack stack,
            string destName)
        {
            if (stack == null)
                return;
            OpenInternal(
                agents,
                WorldTravelTarget.AtNode(stack.NodeId ?? string.Empty),
                destName,
                stack.Id ?? string.Empty);
        }

        void OpenInternal(
            IReadOnlyList<EntityId> agents,
            WorldTravelTarget target,
            string destName,
            string pursueStackId)
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

            if (_agents.Count == 0 || bootstrap?.Session == null)
                return;
            if (!target.IsRouteProgress && string.IsNullOrEmpty(target.NodeId))
                return;

            _target = target;
            var graph = bootstrap.Session.World.WorldGraph;
            _destName = string.IsNullOrEmpty(destName) ? target.Describe(graph) : destName;
            _pursueStackId = pursueStackId ?? string.Empty;
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

            sb.Append("」");
            if (!string.IsNullOrEmpty(_pursueStackId))
                sb.Append("沿道路追击「").Append(_destName).Append("」并在抵达其位置接战？\n\n");
            else
                sb.Append("移动到「").Append(_destName).Append("」？\n\n");
            sb.Append("确认后会打断当前宏观行动。\n");
            if (!string.IsNullOrEmpty(_pursueStackId))
                sb.Append("· 沿道路抵达敌军位置；先到者可先接战，后到者可加入战斗");
            else if (_target.IsRouteProgress)
                sb.Append("· 沿所选道路前往指定位置");
            else
                sb.Append("· 沿宏观道路逐段前进，途经节点时自动续走");
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
                var target = _target;
                var stackId = _pursueStackId;
                Close();
                if (!string.IsNullOrEmpty(stackId) &&
                    bootstrap?.Session != null &&
                    bootstrap.Session.World.Strategic.Armies.TryGet(stackId, out var stack) &&
                    stack != null)
                {
                    bootstrap.WorldTravelDeparture?.BeginPursuitToStackAnchor(agents, stack);
                }
                else
                {
                    bootstrap.WorldTravelDeparture?.BeginMacroOrder(
                        agents, target, closeWorldMap: false);
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
