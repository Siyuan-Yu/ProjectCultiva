using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>战略层角色列表。CW-U4.1 后产品界面只读，不再创建 FormalArmy。</summary>
    public sealed class HostStrategicCharacterListPanel
    {
        const float DoubleClickWindowSec = 0.35f;

        readonly List<StrategicCharacterRosterRow> _rows = new List<StrategicCharacterRosterRow>(32);
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
            _status = string.Empty;
        }

        public void Toggle()
        {
            if (_open)
                Close();
            else
                Open();
        }

        public void Draw(
            Rect panelRect,
            SimulationWorld world,
            IReadOnlyList<EntityId> partyCharacterIds,
            PlayerPartyRuntime partyRuntime,
            Func<SimulationWorld, EntityId, string> labelFn,
            Action<string> onFocusNpcSquad,
            Action<string> onFocusNode)
        {
            if (!_open || world == null)
                return;

            var factionId = HostStrategicRosterQueries.ResolvePlayerFactionId(world, partyCharacterIds);
            HostStrategicRosterQueries.CollectPlayerCharacters(
                world, factionId, partyCharacterIds, _rows, partyRuntime);

            HostUiHitTest.Block(panelRect);

            var prev = GUI.color;
            GUI.color = new Color(0.11f, 0.12f, 0.14f, 0.98f);
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
            GUI.color = prev;

            GUI.Label(new Rect(panelRect.x + 10f, panelRect.y + 8f, panelRect.width - 100f, 22f),
                "角色列表", _title);
            if (GUI.Button(new Rect(panelRect.xMax - 88f, panelRect.y + 6f, 76f, 24f), "关闭"))
                Close();

            const float footerH = 28f;
            const float footerPad = 8f;
            var contentTop = panelRect.y + 36f;
            var contentBottom = panelRect.yMax - footerPad - footerH;
            var listW = panelRect.width * 0.48f;
            var listRect = new Rect(panelRect.x + 8f, contentTop, listW - 8f, contentBottom - contentTop);
            var detailRect = new Rect(
                listRect.xMax + 8f,
                contentTop,
                panelRect.width - listW - 24f,
                contentBottom - contentTop);

            DrawCharacterList(listRect, onFocusNpcSquad, onFocusNode);

            DrawCharacterDetail(detailRect, world, labelFn);

            if (!string.IsNullOrEmpty(_status))
            {
                GUI.Label(new Rect(panelRect.x + 8f, panelRect.yMax - 28f, panelRect.width - 16f, 22f), _status, _body);
            }

        }

        void DrawCharacterList(Rect listRect, Action<string> onFocusNpcSquad, Action<string> onFocusNode)
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

                const float indent = 0f;

                var armyLabel = row.IsGrouped
                    ? "NPC 小队成员"
                    : row.SiteLabel;
                var label = row.DisplayName + "  ·  " + row.LifeStateLabel + "\n" +
                            StrategicFactionCatalog.DisplayName(row.FactionId) + "  ·  " + armyLabel;
                var labelRect = new Rect(indent, y, itemRect.width - indent, 48f);
                var prevColor = GUI.color;
                if (GUI.Button(labelRect, label, _body))
                    HandleCharacterClick(row, onFocusNpcSquad, onFocusNode);
                GUI.color = prevColor;

                y += 52f;
            }

            GUI.EndScrollView();
        }

        void HandleCharacterClick(
            StrategicCharacterRosterRow row,
            Action<string> onFocusNpcSquad,
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
                    onFocusNpcSquad?.Invoke(row.ArmyId);
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
                    "单击列表中的角色查看详情。", _body);
                return;
            }

            var row = FindRow(new EntityId(idVal));
            var membership = row?.IsGrouped == true ? "NPC 小队成员" : "\u2014";
            GUI.Label(new Rect(detailRect.x, y, detailRect.width, 190f),
                labelFn(world, entity.Id) + "\n" +
                "\u52bf\u529b\uff1a" + StrategicFactionCatalog.DisplayName(row?.FactionId) + "\n" +
                "\u72b6\u6001\uff1a" + (row?.LifeStateLabel ?? "\u2014") + "  \u7f16\u7ec4\uff1a" + membership + "\n" +
                FormatWorldPresence(world, entity.Id),
                _body);
        }

        static string FormatWorldPresence(SimulationWorld world, EntityId id)
        {
            if (world.LocalMap.IsInInterior && world.Strategic.PlayerPartyContext?.IsMember(id) == true)
                return "位置状态：独立空间 / 室内";

            world.WorldPresence.TryGet(id, out var personal);
            var siteId = personal?.SiteId ?? string.Empty;
            var surfaceId = personal?.PersonalSurfaceId ?? string.Empty;
            var hasPosition = personal?.HasContinuousWorldPosition == true;
            var position = hasPosition ? personal.ContinuousWorldPosition : default;
            var state = personal?.Mode == XianXia.Core.World.PartyWorldPresenceMode.AtSite
                ? "据点内" : "连续世界";
            var owner = "个人";

            if (ArmyService.TryGetArmyForCharacter(world, id, out var army) &&
                army != null && army.WorldMotion.HasPosition && !army.UsesHexStrategicPosition)
            {
                owner = "NPC 小队";
                position = army.WorldMotion.WorldPosition;
                hasPosition = true;
                surfaceId = army.WorldMotion.SurfaceId;
                siteId = army.WorldMotion.SiteId;
                state = string.IsNullOrEmpty(siteId) ? "连续世界" : "据点内";
            }

            var siteName = "—";
            if (!string.IsNullOrEmpty(siteId) &&
                world.Strategic.Sites.TryGet(siteId, out var site) && site != null)
                siteName = site.DisplayName;
            return "位置状态：" + state +
                   "\n所在据点：" + siteName +
                   "\n连续世界：" + (string.IsNullOrEmpty(surfaceId) ? "—" : "主大陆") +
                   "\n世界坐标：" + (hasPosition
                       ? "(" + position.X.ToString("0.000") + ", " + position.Y.ToString("0.000") + ")"
                       : "—") +
                   "\n空间归属：" + owner;
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
