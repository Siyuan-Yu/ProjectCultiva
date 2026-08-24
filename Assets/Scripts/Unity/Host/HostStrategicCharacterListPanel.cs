using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>战略层角色列表（全战Host 一级入口；只读 + 组军薄命令）/summary>
    public sealed class HostStrategicCharacterListPanel
    {
        const float DoubleClickWindowSec = 0.35f;

        readonly List<StrategicCharacterRosterRow> _rows = new List<StrategicCharacterRosterRow>(32);
        readonly HashSet<ulong> _createSelection = new HashSet<ulong>();
        readonly List<EntityId> _createPartyScratch = new List<EntityId>(8);
        readonly GUIStyle _body;
        readonly GUIStyle _title;

        bool _open;
        string _selectedCharacterValue = string.Empty;
        Vector2 _listScroll;
        string _lastClickCharacterId = string.Empty;
        double _lastClickTime;
        string _status = string.Empty;

        public HostStrategicCharacterListPanel(GUIStyle body, GUIStyle title)
        {
            _body = body;
            _title = title;
        }

        public bool IsOpen => _open;

        public void Open() => _open = true;

        public void Close()
        {
            _open = false;
            _selectedCharacterValue = string.Empty;
            _createSelection.Clear();
            _status = string.Empty;
        }

        public void Toggle()
        {
            if (_open)
                Close();
            else
                Open();
        }

        public bool Draw(
            SimulationWorld world,
            IReadOnlyList<EntityId> partyCharacterIds,
            Func<SimulationWorld, EntityId, string> labelFn,
            Action<string> onFocusArmy,
            Action<string> onFocusNode,
            Action<string> onArmyCreated,
            Action onChanged)
        {
            if (!_open || world == null)
                return false;

            var changed = false;
            var factionId = HostStrategicRosterQueries.ResolvePlayerFactionId(world, partyCharacterIds);
            HostStrategicRosterQueries.CollectPlayerCharacters(world, factionId, partyCharacterIds, _rows);

            var panelW = Mathf.Min(640f, Screen.width - 24f);
            var panelH = Screen.height - 120f;
            var panelRect = new Rect(12f, 100f, panelW, panelH);
            HostUiHitTest.Block(panelRect);

            var prev = GUI.color;
            GUI.color = new Color(0.11f, 0.12f, 0.14f, 0.98f);
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
            GUI.color = prev;

            GUI.Label(new Rect(panelRect.x + 10f, panelRect.y + 8f, panelRect.width - 100f, 22f),
                "角色列表 [Global Strategic UI]", _title);
            if (GUI.Button(new Rect(panelRect.xMax - 88f, panelRect.y + 6f, 76f, 24f), "关闭"))
                Close();

            var contentTop = panelRect.y + 36f;
            var listW = panelRect.width * 0.48f;
            var listRect = new Rect(panelRect.x + 8f, contentTop, listW - 8f, panelRect.height - 88f);
            var detailRect = new Rect(listRect.xMax + 8f, contentTop, panelRect.width - listW - 24f, listRect.height);

            DrawCharacterList(listRect, onFocusArmy, onFocusNode);

            DrawCharacterDetail(detailRect, world, labelFn);

            if (GUI.Button(new Rect(panelRect.x + 8f, panelRect.yMax - 36f, 120f, 28f), "组建军队"))
            {
                changed |= TryCreateArmyFromSelection(world, factionId, onArmyCreated, onChanged);
            }

            if (!string.IsNullOrEmpty(_status))
            {
                GUI.Label(new Rect(panelRect.x + 136f, panelRect.yMax - 32f, panelRect.width - 144f, 22f), _status, _body);
            }

            return changed;
        }

        void DrawCharacterList(Rect listRect, Action<string> onFocusArmy, Action<string> onFocusNode)
        {
            var viewH = Mathf.Max(listRect.height, _rows.Count * 52f + 8f);
            _listScroll = GUI.BeginScrollView(
                listRect,
                _listScroll,
                new Rect(0f, 0f, listRect.width - 18f, viewH));

            var y = 0f;
            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                var itemRect = new Rect(0f, y, listRect.width - 20f, 48f);
                var selected = string.Equals(row.CharacterId.Value.ToString(), _selectedCharacterValue, StringComparison.Ordinal);
                if (selected)
                {
                    GUI.color = new Color(0.25f, 0.35f, 0.42f, 0.85f);
                    GUI.DrawTexture(itemRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }

                var indent = 0f;
                if (!row.IsGrouped && row.CanSelectForArmyCreation)
                {
                    var toggle = _createSelection.Contains(row.CharacterId.Value);
                    var next = GUI.Toggle(new Rect(4f, y + 14f, 18f, 18f), toggle, GUIContent.none);
                    if (next != toggle)
                    {
                        if (next)
                            _createSelection.Add(row.CharacterId.Value);
                        else
                            _createSelection.Remove(row.CharacterId.Value);
                    }

                    indent = 24f;
                }
                else if (!row.IsGrouped)
                {
                    indent = 24f;
                }

                var armyLabel = row.IsGrouped
                    ? "Army: " + row.ArmyId
                    : row.SiteLabel + "  \u00b7  \u672a\u7f16\u7ec4";
                var label = row.DisplayName + "  ·  " + row.LifeStateLabel + "\n" +
                            StrategicFactionCatalog.DisplayName(row.FactionId) + "  ·  " + armyLabel;
                var labelRect = new Rect(indent, y, itemRect.width - indent, 48f);
                var prevColor = GUI.color;
                if (!row.CanSelectForArmyCreation && !row.IsGrouped)
                    GUI.color = new Color(0.72f, 0.72f, 0.75f, 1f);
                if (GUI.Button(labelRect, label, _body))
                    HandleCharacterClick(row, onFocusArmy, onFocusNode);
                GUI.color = prevColor;

                y += 52f;
            }

            GUI.EndScrollView();
        }

        void HandleCharacterClick(
            StrategicCharacterRosterRow row,
            Action<string> onFocusArmy,
            Action<string> onFocusNode)
        {
            var idKey = row.CharacterId.Value.ToString();
            var now = Time.realtimeSinceStartupAsDouble;
            if (string.Equals(_lastClickCharacterId, idKey, StringComparison.Ordinal) &&
                now - _lastClickTime <= DoubleClickWindowSec)
            {
                _lastClickCharacterId = string.Empty;
                _selectedCharacterValue = idKey;
                if (row.IsGrouped && !string.IsNullOrEmpty(row.ArmyId))
                    onFocusArmy?.Invoke(row.ArmyId);
                else if (!string.IsNullOrEmpty(row.SiteId))
                    onFocusNode?.Invoke(row.SiteId);
                return;
            }

            _lastClickCharacterId = idKey;
            _lastClickTime = now;
            _selectedCharacterValue = idKey;
        }

        void DrawCharacterDetail(
            Rect detailRect,
            SimulationWorld world,
            Func<SimulationWorld, EntityId, string> labelFn)
        {
            GUI.Label(new Rect(detailRect.x, detailRect.y, detailRect.width, 20f), "角色详情", _title);
            var y = detailRect.y + 24f;

            if (string.IsNullOrEmpty(_selectedCharacterValue) ||
                !ulong.TryParse(_selectedCharacterValue, out var idVal) ||
                !world.Entities.TryGet(new EntityId(idVal), out var entity) ||
                entity == null)
            {
                GUI.Label(new Rect(detailRect.x, y, detailRect.width, 56f),
                    "单击列表中的角色查看详情。\n存活且未编组角色可勾选，再点底部「组建军队」。", _body);
                return;
            }

            var row = FindRow(new EntityId(idVal));
            var membership = row?.IsGrouped == true ? row.ArmyId : "\u2014";
            GUI.Label(new Rect(detailRect.x, y, detailRect.width, 88f),
                labelFn(world, entity.Id) + "\n" +
                "\u52bf\u529b\uff1a" + StrategicFactionCatalog.DisplayName(row?.FactionId) + "\n" +
                "\u4f4d\u7f6e\uff1a" + (row?.SiteLabel ?? "\u2014") + "\n" +
                "\u72b6\u6001\uff1a" + (row?.LifeStateLabel ?? "\u2014") + "  \u7f16\u7ec4\uff1a" + membership,
                _body);
        }

        bool TryCreateArmyFromSelection(
            SimulationWorld world,
            string factionId,
            Action<string> onArmyCreated,
            Action onChanged)
        {
            _createPartyScratch.Clear();
            foreach (var sel in _createSelection)
            {
                var id = new EntityId(sel);
                if (!LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, id))
                    continue;
                _createPartyScratch.Add(id);
            }
            if (_createPartyScratch.Count < 1)
            {
                _status = "请至少勾选一名未编组角色";
                return false;
            }

            var nodeId = ArmyService.ResolveCharacterFormationLocationId(world, _createPartyScratch[0]) ?? string.Empty;
            var result = ArmyUiCommands.TryCreateArmy(world, nodeId, factionId, _createPartyScratch);
            if (result.IsSuccess)
            {
                _status = "已创建 " + result.Value.ArmyId;
                _createSelection.Clear();
                onArmyCreated?.Invoke(result.Value.ArmyId);
                onChanged?.Invoke();
                return true;
            }

            _status = ArmyUiCommands.DescribeError(result.Error);
            return false;
        }

        StrategicCharacterRosterRow FindRow(EntityId id)
        {
            for (var i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].CharacterId == id)
                    return _rows[i];
            }

            return null;
        }
    }
}
