using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>世界地图势力／外交总览 V0：仅读取当前运行时战略状态。</summary>
    public sealed class HostFactionDiplomacyOverviewPanel
    {
        readonly List<string> _factionIds = new List<string>(16);
        readonly List<string> _vassalIds = new List<string>(8);
        readonly GUIStyle _body;
        readonly GUIStyle _title;

        bool _open;
        string _selectedFactionId = string.Empty;
        Vector2 _listScroll;
        Vector2 _detailScroll;

        public HostFactionDiplomacyOverviewPanel(GUIStyle body, GUIStyle title)
        {
            _body = body;
            _title = title;
        }

        public bool IsOpen => _open;

        public void Open()
        {
            _open = true;
            _selectedFactionId = string.Empty;
        }

        public void Close()
        {
            _open = false;
            _selectedFactionId = string.Empty;
        }

        public void Draw(Rect panelRect, SimulationWorld world)
        {
            if (!_open || world == null)
                return;

            FactionDiplomacyOverviewQuery.CollectRuntimeFactionIds(world, _factionIds);
            EnsureSelectedFaction(world);

            HostUiHitTest.Block(panelRect);
            var previousColor = GUI.color;
            GUI.color = new Color(0.11f, 0.12f, 0.14f, 0.98f);
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
            GUI.color = previousColor;

            GUI.Label(new Rect(panelRect.x + 10f, panelRect.y + 8f, panelRect.width - 104f, 22f),
                "势力 / 外交", _title);
            if (GUI.Button(new Rect(panelRect.xMax - 88f, panelRect.y + 6f, 76f, 24f), "关闭"))
                Close();

            var contentTop = panelRect.y + 36f;
            var contentBottom = panelRect.yMax - 8f;
            var listWidth = panelRect.width * 0.40f;
            var listRect = new Rect(panelRect.x + 8f, contentTop, listWidth - 12f, contentBottom - contentTop);
            var detailRect = new Rect(
                listRect.xMax + 8f,
                contentTop,
                panelRect.xMax - listRect.xMax - 16f,
                contentBottom - contentTop);

            DrawFactionList(listRect, world);
            DrawFactionDetail(detailRect, world);
        }

        void EnsureSelectedFaction(SimulationWorld world)
        {
            if (_factionIds.Count == 0)
            {
                _selectedFactionId = string.Empty;
                return;
            }

            if (_factionIds.Contains(_selectedFactionId))
                return;

            var playerFactionId = world.Strategic.PlayerFactionId ?? string.Empty;
            _selectedFactionId = _factionIds.Contains(playerFactionId)
                ? playerFactionId
                : _factionIds[0];
        }

        void DrawFactionList(Rect listRect, SimulationWorld world)
        {
            GUI.Label(new Rect(listRect.x, listRect.y, listRect.width, 20f), "当前势力", _title);
            if (_factionIds.Count == 0)
            {
                GUI.Label(new Rect(listRect.x, listRect.y + 26f, listRect.width, 48f),
                    "当前战略世界没有可显示的势力。", _body);
                return;
            }

            var viewport = new Rect(listRect.x, listRect.y + 24f, listRect.width, listRect.height - 24f);
            var contentHeight = Mathf.Max(viewport.height, _factionIds.Count * 52f + 8f);
            _listScroll = GUI.BeginScrollView(
                viewport,
                _listScroll,
                new Rect(0f, 0f, viewport.width - 18f, contentHeight));

            var y = 0f;
            for (var i = 0; i < _factionIds.Count; i++)
            {
                var factionId = _factionIds[i];
                var itemRect = new Rect(0f, y, viewport.width - 20f, 48f);
                var selected = string.Equals(factionId, _selectedFactionId, StringComparison.Ordinal);
                if (selected)
                {
                    GUI.color = new Color(0.25f, 0.35f, 0.42f, 0.85f);
                    GUI.DrawTexture(itemRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }

                var relation = FactionDiplomacyRelationQuery.GetRelationToPlayer(world, factionId);
                var label = StrategicFactionCatalog.DisplayName(factionId) + "\n与你的关系：" + DescribeRelation(relation);
                if (GUI.Button(itemRect, label, _body))
                    _selectedFactionId = factionId;

                y += 52f;
            }

            GUI.EndScrollView();
        }

        void DrawFactionDetail(Rect detailRect, SimulationWorld world)
        {
            GUI.Label(new Rect(detailRect.x, detailRect.y, detailRect.width, 20f), "势力详情", _title);
            if (string.IsNullOrEmpty(_selectedFactionId))
                return;

            var viewport = new Rect(detailRect.x, detailRect.y + 24f, detailRect.width, detailRect.height - 24f);
            var contentHeight = Mathf.Max(viewport.height, 220f + _factionIds.Count * 26f);
            _detailScroll = GUI.BeginScrollView(
                viewport,
                _detailScroll,
                new Rect(0f, 0f, viewport.width - 18f, contentHeight));

            var y = 0f;
            var playerRelation = FactionDiplomacyRelationQuery.GetRelationToPlayer(world, _selectedFactionId);
            y = DrawLine(viewport.width, y, StrategicFactionCatalog.DisplayName(_selectedFactionId), _title, 24f);
            y = DrawLine(viewport.width, y, "势力 ID：" + _selectedFactionId, _body, 20f);
            y = DrawLine(viewport.width, y, "与你的关系：" + DescribeRelation(playerRelation), _body, 20f);
            y = DrawLine(viewport.width, y,
                "领地区域：" + FactionDiplomacyOverviewQuery.CountControlledTerritoryRegions(world, _selectedFactionId) +
                "    正式军队：" + FactionDiplomacyOverviewQuery.CountFormalArmies(world, _selectedFactionId),
                _body,
                20f);

            if (world.Strategic.Vassalages.TryGetOverlord(_selectedFactionId, out var overlordFactionId))
                y = DrawLine(viewport.width, y, "宗主：" + StrategicFactionCatalog.DisplayName(overlordFactionId), _body, 20f);

            FactionDiplomacyOverviewQuery.CollectVassalIds(world, _selectedFactionId, _vassalIds);
            if (_vassalIds.Count > 0)
                y = DrawLine(viewport.width, y, "附庸：" + JoinFactionNames(_vassalIds), _body, 20f);

            y += 8f;
            y = DrawLine(viewport.width, y, "外交关系", _title, 22f);
            for (var i = 0; i < _factionIds.Count; i++)
            {
                var targetFactionId = _factionIds[i];
                if (string.Equals(targetFactionId, _selectedFactionId, StringComparison.Ordinal))
                    continue;

                var relation = FactionDiplomacyRelationQuery.GetRelation(world, _selectedFactionId, targetFactionId);
                y = DrawLine(viewport.width, y,
                    StrategicFactionCatalog.DisplayName(targetFactionId) + "    " + DescribeRelation(relation),
                    _body,
                    24f);
            }

            GUI.EndScrollView();
        }

        float DrawLine(float width, float y, string text, GUIStyle style, float height)
        {
            GUI.Label(new Rect(0f, y, width, height), text, style);
            return y + height;
        }

        string JoinFactionNames(List<string> factionIds)
        {
            var names = new List<string>(factionIds.Count);
            for (var i = 0; i < factionIds.Count; i++)
                names.Add(StrategicFactionCatalog.DisplayName(factionIds[i]));
            return string.Join("、", names.ToArray());
        }

        static string DescribeRelation(FactionDiplomacyRelation relation)
        {
            switch (relation)
            {
                case FactionDiplomacyRelation.Self:
                    return "自己";
                case FactionDiplomacyRelation.War:
                    return "战争";
                case FactionDiplomacyRelation.Alliance:
                    return "联盟";
                case FactionDiplomacyRelation.Overlord:
                    return "宗主";
                case FactionDiplomacyRelation.Vassal:
                    return "附庸";
                default:
                    return "普通";
            }
        }
    }
}
