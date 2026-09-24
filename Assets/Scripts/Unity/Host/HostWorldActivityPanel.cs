using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Time;
using XianXia.Core.World;

namespace XianXia.Unity.Host
{
    /// <summary>Persistent WorldActivity feed. It never pauses simulation and is not a Quest UI.</summary>
    public sealed class HostWorldActivityPanel : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;

        readonly List<WorldActivityEntry> _active = new List<WorldActivityEntry>(16);
        readonly List<WorldActivityEntry> _history = new List<WorldActivityEntry>(WorldActivityBoard.HistoryCapacity);
        string _selectedActivityId = string.Empty;
        bool _historyOpen;
        Vector2 _historyScroll;
        string _status = string.Empty;
        Texture2D _pixel;
        GUIStyle _title;
        GUIStyle _body;
        GUIStyle _small;

        static readonly Color Parchment = new Color(0.92f, 0.86f, 0.74f, 0.97f);
        static readonly Color Dark = new Color(0.38f, 0.29f, 0.19f, 1f);
        static readonly Color Ink = new Color(0.16f, 0.12f, 0.08f, 1f);
        static readonly Color Unread = new Color(0.92f, 0.55f, 0.16f, 1f);

        public void Bind(PlayableHostBootstrap host) => bootstrap = host;

        public void ClearSessionState()
        {
            _selectedActivityId = string.Empty;
            _historyOpen = false;
            _status = string.Empty;
        }

        void OnGUI()
        {
            var session = bootstrap?.Session;
            if (session == null || !session.IsInitialized || session.World?.WorldActivities == null) return;
            EnsureStyles();
            Collect(session.World.WorldActivities);

            const float railWidth = 168f;
            var railHeight = Mathf.Min(Screen.height - 180f, Mathf.Max(92f, 38f + _active.Count * 34f + 34f));
            var rail = new Rect(8f, 150f, railWidth, railHeight);
            Fill(rail, Parchment);
            DrawFrame(rail);
            GUI.Label(new Rect(rail.x + 10f, rail.y + 7f, rail.width - 20f, 24f), "活动动态", _title);
            var y = rail.y + 34f;
            for (var i = 0; i < _active.Count && y + 30f < rail.yMax - 30f; i++)
            {
                var entry = _active[i];
                var prefix = entry.IsRead ? "• " : "! 新 · ";
                var old = GUI.color;
                if (!entry.IsRead) GUI.color = Unread;
                if (GUI.Button(new Rect(rail.x + 8f, y, rail.width - 16f, 28f), prefix + entry.Title))
                    Open(entry);
                GUI.color = old;
                y += 32f;
            }
            if (GUI.Button(new Rect(rail.x + 8f, rail.yMax - 29f, rail.width - 16f, 23f),
                    "历史（" + _history.Count + "）"))
            {
                _historyOpen = !_historyOpen;
                _selectedActivityId = string.Empty;
                _status = string.Empty;
            }
            HostUiHitTest.Block(rail);

            if (_historyOpen) DrawHistory(session.World.WorldActivities);
            else if (!string.IsNullOrEmpty(_selectedActivityId)) DrawDetail(session.World.WorldActivities);
        }

        void Collect(WorldActivityBoard board)
        {
            _active.Clear();
            _history.Clear();
            foreach (var entry in board.Entries.Values)
                if (entry.State == WorldActivityState.Active) _active.Add(entry); else _history.Add(entry);
            _active.Sort((a, b) => b.CreatedDayIndex.CompareTo(a.CreatedDayIndex));
            _history.Sort((a, b) =>
            {
                var day = (b.ResolvedDayIndex ?? 0).CompareTo(a.ResolvedDayIndex ?? 0);
                return day != 0 ? day : string.CompareOrdinal(b.ActivityId, a.ActivityId);
            });
        }

        void Open(WorldActivityEntry entry)
        {
            bootstrap.Session.World.WorldActivities.MarkRead(entry.ActivityId);
            _selectedActivityId = entry.ActivityId;
            _historyOpen = false;
            _status = string.Empty;
        }

        void DrawDetail(WorldActivityBoard board)
        {
            if (!board.TryGet(_selectedActivityId, out var entry))
            {
                _selectedActivityId = string.Empty;
                return;
            }
            var rect = new Rect(184f, 150f, 420f, 252f);
            Fill(rect, Parchment);
            DrawFrame(rect);
            GUI.Label(new Rect(rect.x + 16f, rect.y + 12f, rect.width - 32f, 28f), entry.Title, _title);
            GUI.Label(new Rect(rect.x + 16f, rect.y + 47f, rect.width - 32f, 64f), entry.Body, _body);
            GUI.Label(new Rect(rect.x + 16f, rect.y + 116f, rect.width - 32f, 22f),
                "出现时间：第 " + (entry.CreatedDayIndex + 1) + " 天", _small);
            var stateText = entry.State == WorldActivityState.Active ? "状态：仍然有效" : "状态：已结束";
            GUI.Label(new Rect(rect.x + 16f, rect.y + 140f, rect.width - 32f, 22f), stateText, _small);
            var remaining = RemainingText(entry);
            if (!string.IsNullOrEmpty(remaining))
                GUI.Label(new Rect(rect.x + 16f, rect.y + 164f, rect.width - 32f, 22f), remaining, _small);
            if (!string.IsNullOrEmpty(_status))
                GUI.Label(new Rect(rect.x + 16f, rect.y + 187f, rect.width - 32f, 22f), _status, _small);

            var x = rect.xMax - 92f;
            if (CanLocate(entry))
            {
                if (GUI.Button(new Rect(x - 92f, rect.yMax - 38f, 82f, 26f), "定位")) Locate(entry);
            }
            if (GUI.Button(new Rect(x, rect.yMax - 38f, 76f, 26f), "关闭"))
            {
                _selectedActivityId = string.Empty;
                _status = string.Empty;
            }
            HostUiHitTest.Block(rect);
        }

        string RemainingText(WorldActivityEntry entry)
        {
            if (entry.State != WorldActivityState.Active ||
                !bootstrap.Session.World.WorldOpportunities.ActiveInstances.TryGetValue(entry.SourceId, out var instance))
                return string.Empty;
            var day = DayClock.FromWorldTick(bootstrap.Session.World.Tick).DayIndex;
            var remaining = instance.ExpireDayIndexExclusive > day ? instance.ExpireDayIndexExclusive - day : 0;
            return "预计还会停留 " + remaining + " 天";
        }

        bool CanLocate(WorldActivityEntry entry)
        {
            var world = bootstrap?.Session?.World;
            return world != null && entry.State == WorldActivityState.Active &&
                   entry.SourceKind == WorldActivitySourceKind.WorldOpportunity &&
                   world.WorldOpportunities.ActiveInstances.TryGetValue(entry.SourceId, out var instance) &&
                   world.WorldOpportunities.TryGetSpec(instance.OpportunityDefinitionId, out var spec) &&
                   ((instance.DiscoveryMode == XianXia.Core.Opportunity.WorldOpportunityDiscoveryMode.PublicNotice &&
                     spec.PublicNoticeRevealExactLocation) ||
                    (instance.DiscoveryMode == XianXia.Core.Opportunity.WorldOpportunityDiscoveryMode.HiddenUntilDiscovered &&
                     instance.IsDiscovered)) && HasPosition(world, instance);
        }

        void Locate(WorldActivityEntry entry)
        {
            var world = bootstrap.Session.World;
            if (!world.WorldOpportunities.ActiveInstances.TryGetValue(entry.SourceId, out var instance) ||
                !TryPosition(world, instance, out var surfaceId, out var position))
            {
                _status = "该活动已结束。";
                return;
            }
            if (!bootstrap.TryFocusContinuousWorldPosition(
                    surfaceId, position, out var message))
            {
                _status = message;
                return;
            }
            _status = "镜头已定位；角色位置未改变。";
        }

        void DrawHistory(WorldActivityBoard board)
        {
            var rect = new Rect(184f, 150f, 430f, Mathf.Min(430f, Screen.height - 180f));
            Fill(rect, Parchment);
            DrawFrame(rect);
            GUI.Label(new Rect(rect.x + 16f, rect.y + 10f, rect.width - 100f, 28f), "World Activity History", _title);
            if (GUI.Button(new Rect(rect.xMax - 84f, rect.y + 9f, 68f, 24f), "关闭")) _historyOpen = false;
            var view = new Rect(0f, 0f, rect.width - 42f, Mathf.Max(rect.height - 54f, _history.Count * 54f));
            _historyScroll = GUI.BeginScrollView(new Rect(rect.x + 12f, rect.y + 42f, rect.width - 24f, rect.height - 52f), _historyScroll, view);
            var y = 0f;
            for (var i = 0; i < _history.Count; i++)
            {
                var entry = _history[i];
                if (GUI.Button(new Rect(4f, y, view.width - 8f, 24f),
                    "第 " + ((entry.ResolvedDayIndex ?? entry.CreatedDayIndex) + 1) + " 天 · " + entry.Title + " · 已结束"))
                {
                    _selectedActivityId = entry.ActivityId;
                    _historyOpen = false;
                    _status = string.Empty;
                }
                GUI.Label(new Rect(4f, y + 23f, view.width - 8f, 20f), "出现于第 " + (entry.CreatedDayIndex + 1) + " 天 · 已结束", _small);
                y += 54f;
            }
            GUI.EndScrollView();
            HostUiHitTest.Block(rect);
        }

        static bool HasPosition(XianXia.Core.Simulation.SimulationWorld world,
            XianXia.Core.Opportunity.WorldOpportunityInstance instance) =>
            TryPosition(world, instance, out _, out _);

        static bool TryPosition(XianXia.Core.Simulation.SimulationWorld world,
            XianXia.Core.Opportunity.WorldOpportunityInstance instance, out string surfaceId,
            out XianXia.Core.World.WorldVec2 position)
        {
            surfaceId = string.Empty; position = default;
            if (instance.SpawnKind == XianXia.Core.Opportunity.WorldOpportunitySpawnKind.WorldObject)
            {
                surfaceId = instance.SurfaceId;
                position = new XianXia.Core.World.WorldVec2(instance.WorldX, instance.WorldY);
                return true;
            }
            if (!world.WorldPresence.TryGet(instance.SpawnedEntityId, out var presence) || presence == null ||
                !presence.HasContinuousWorldPosition) return false;
            surfaceId = presence.PersonalSurfaceId; position = presence.ContinuousWorldPosition; return true;
        }

        void EnsureStyles()
        {
            if (_pixel != null) return;
            _pixel = new Texture2D(1, 1); _pixel.SetPixel(0, 0, Color.white); _pixel.Apply();
            _title = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = Ink } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, normal = { textColor = Ink } };
            _small = new GUIStyle(_body) { fontSize = 12 };
        }

        void Fill(Rect rect, Color color) { var old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, _pixel); GUI.color = old; }
        void DrawFrame(Rect rect)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, 2f), Dark); Fill(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), Dark);
            Fill(new Rect(rect.x, rect.y, 2f, rect.height), Dark); Fill(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), Dark);
        }
    }
}
