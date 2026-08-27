using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>战略层军队列表（全战式 Host 入口；只读 + ArmyUiCommands）。</summary>
    public sealed class HostStrategicArmyListPanel
    {
        const float DoubleClickWindowSec = 0.35f;

        readonly List<StrategicArmyRosterRow> _rows = new List<StrategicArmyRosterRow>(16);
        readonly HostArmyFormPanel _detailPanel;
        readonly GUIStyle _body;
        readonly GUIStyle _title;

        bool _open;
        bool _showCreate;
        string _selectedArmyId = string.Empty;
        Vector2 _listScroll;
        string _lastClickArmyId = string.Empty;
        double _lastClickTime;

        public HostStrategicArmyListPanel(GUIStyle body, GUIStyle title, HostArmyFormPanel detailPanel)
        {
            _body = body;
            _title = title;
            _detailPanel = detailPanel;
        }

        public bool IsOpen => _open;

        public void Open()
        {
            _open = true;
            _showCreate = false;
        }

        public void Close()
        {
            _open = false;
            _showCreate = false;
            _selectedArmyId = string.Empty;
            _detailPanel?.Close();
        }

        public void Toggle()
        {
            if (_open)
                Close();
            else
                Open();
        }

        public string SelectedArmyId => _selectedArmyId;

        public bool Draw(
            Rect panelRect,
            SimulationWorld world,
            IReadOnlyList<EntityId> partyCharacterIds,
            Func<SimulationWorld, EntityId, string> labelFn,
            PlayerPartyRuntime partyRuntime,
            Action<string> onFocusArmy,
            Action onChanged)
        {
            if (!_open || world == null)
                return false;

            var changed = false;
            var factionId = HostStrategicRosterQueries.ResolvePlayerFactionId(world, partyCharacterIds);
            HostStrategicRosterQueries.CollectPlayerArmies(world, factionId, _rows);

            HostUiHitTest.Block(panelRect);
            var prev = GUI.color;
            GUI.color = new Color(0.11f, 0.12f, 0.14f, 0.98f);
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
            GUI.color = prev;

            GUI.Label(new Rect(panelRect.x + 10f, panelRect.y + 8f, panelRect.width - 120f, 22f),
                "军队列表 [Global Strategic UI]", _title);

            if (GUI.Button(new Rect(panelRect.xMax - 92f, panelRect.y + 6f, 80f, 24f), "关闭"))
                Close();

            var contentTop = panelRect.y + 36f;
            var listW = panelRect.width * 0.42f;
            const float createButtonH = 28f;
            const float createButtonPad = 6f;
            var createBtnY = panelRect.yMax - 8f - createButtonH;
            var listRect = new Rect(
                panelRect.x + 8f,
                contentTop,
                listW - 12f,
                createBtnY - contentTop - createButtonPad);
            var detailRect = new Rect(listRect.xMax + 8f, contentTop, panelRect.width - listW - 16f, panelRect.height - 44f);

            DrawArmyList(listRect, onFocusArmy);

            if (_rows.Count == 0 && !_showCreate)
            {
                GUI.Label(new Rect(listRect.x, listRect.y + 8f, listRect.width, 72f),
                    "当前没有军队\n可以从角色列表中选择符合条件的角色组建第一支军队", _body);
            }

            if (GUI.Button(
                    new Rect(panelRect.x + 10f, createBtnY, listW - 20f, createButtonH),
                    "组建军队"))
            {
                _showCreate = true;
                _selectedArmyId = string.Empty;
                _detailPanel?.OpenGlobalCreate();
            }

            if (_showCreate || string.IsNullOrEmpty(_selectedArmyId))
            {
                if (_detailPanel != null &&
                    _detailPanel.Draw(detailRect, world, partyCharacterIds, labelFn, partyRuntime, embedded: true))
                {
                    changed = true;
                    onChanged?.Invoke();
                    if (!string.IsNullOrEmpty(_detailPanel.LastCreatedArmyId))
                    {
                        _selectedArmyId = _detailPanel.LastCreatedArmyId;
                        _showCreate = false;
                        _detailPanel.OpenGlobalDetail(_selectedArmyId);
                    }
                }
            }
            else if (_detailPanel != null)
            {
                if (!world.Strategic.FormalArmies.TryGet(_selectedArmyId, out var army) || army == null)
                {
                    _selectedArmyId = string.Empty;
                }
                else if (_detailPanel.Draw(detailRect, world, partyCharacterIds, labelFn, partyRuntime, embedded: true))
                {
                    changed = true;
                    onChanged?.Invoke();
                    if (_detailPanel.WasDisbanded)
                    {
                        _selectedArmyId = string.Empty;
                        _showCreate = _rows.Count == 0;
                        if (_showCreate)
                            _detailPanel.OpenGlobalCreate();
                        else
                            _detailPanel.Close();
                    }
                }
            }

            return changed;
        }

        void DrawArmyList(Rect listRect, Action<string> onFocusArmy)
        {
            var viewH = Mathf.Max(listRect.height, _rows.Count * 58f + 8f);
            _listScroll = GUI.BeginScrollView(
                listRect,
                _listScroll,
                new Rect(0f, 0f, listRect.width - 18f, viewH));

            var y = 0f;
            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                var itemRect = new Rect(0f, y, listRect.width - 20f, 54f);
                var selected = string.Equals(row.ArmyId, _selectedArmyId, StringComparison.Ordinal);
                if (selected)
                {
                    GUI.color = new Color(0.25f, 0.35f, 0.42f, 0.85f);
                    GUI.DrawTexture(itemRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }

                var label = row.LeaderLabel + "  ·  " + row.MemberCount + "人  ·  " + row.State + "\n" +
                            HostStrategicRosterQueries.DescribeArmyTravel(row) +
                            "  ·  战力 " + row.CombatPower;
                if (GUI.Button(itemRect, label, _body))
                    HandleArmyListClick(row.ArmyId, onFocusArmy);

                y += 58f;
            }

            GUI.EndScrollView();
        }

        void HandleArmyListClick(string armyId, Action<string> onFocusArmy)
        {
            var now = Time.realtimeSinceStartupAsDouble;
            if (string.Equals(_lastClickArmyId, armyId, StringComparison.Ordinal) &&
                now - _lastClickTime <= DoubleClickWindowSec)
            {
                _lastClickArmyId = string.Empty;
                SelectArmy(armyId);
                onFocusArmy?.Invoke(armyId);
                return;
            }

            _lastClickArmyId = armyId;
            _lastClickTime = now;
            SelectArmy(armyId);
        }

        public void SelectArmy(string armyId)
        {
            _selectedArmyId = armyId ?? string.Empty;
            _showCreate = false;
            if (string.IsNullOrEmpty(_selectedArmyId))
                return;
            if (_detailPanel != null)
                _detailPanel.OpenGlobalDetail(_selectedArmyId);
        }
    }
}
