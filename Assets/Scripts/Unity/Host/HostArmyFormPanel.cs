using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Npc;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>Army Detail / Army Creation UI（仅嵌入军队列表；只调用 ArmyUiCommands / ArmyService）。</summary>
    public sealed class HostArmyFormPanel
    {
        readonly HashSet<ulong> _createSelection = new HashSet<ulong>();
        readonly List<EntityId> _scratchResidents = new List<EntityId>(16);
        readonly List<EntityId> _scratchParty = new List<EntityId>(8);
        readonly HashSet<ulong> _addMemberSelection = new HashSet<ulong>();
        readonly List<FormalArmy> _scratchArmies = new List<FormalArmy>(4);
        readonly GUIStyle _body;
        readonly GUIStyle _title;

        string _nodeId = string.Empty;
        string _detailArmyId = string.Empty;
        string _lastMessage = string.Empty;
        string _lastCreatedArmyId = string.Empty;
        bool _open;
        bool _wasDisbanded;

        public HostArmyFormPanel(GUIStyle body, GUIStyle title)
        {
            _body = body;
            _title = title;
        }

        public bool IsOpen => _open;

        public string LastCreatedArmyId => _lastCreatedArmyId;

        public bool WasDisbanded => _wasDisbanded;

        public void OpenGlobalDetail(string armyId)
        {
            _open = true;
            _detailArmyId = armyId ?? string.Empty;
            _lastMessage = string.Empty;
            _lastCreatedArmyId = string.Empty;
            _wasDisbanded = false;
            _nodeId = string.Empty;
            _createSelection.Clear();
        }

        public void OpenGlobalCreate()
        {
            _open = true;
            _nodeId = string.Empty;
            _detailArmyId = string.Empty;
            _createSelection.Clear();
            _lastMessage = string.Empty;
            _lastCreatedArmyId = string.Empty;
            _wasDisbanded = false;
        }

        public void Close()
        {
            _open = false;
            _nodeId = string.Empty;
            _detailArmyId = string.Empty;
            _createSelection.Clear();
        }

        public bool Draw(
            Rect panelRect,
            SimulationWorld world,
            IReadOnlyList<EntityId> partyCharacterIds,
            System.Func<SimulationWorld, EntityId, string> labelFn,
            bool embedded = true)
        {
            if (!_open || world == null)
                return false;

            var changed = false;
            var factionId = ResolvePlayerFaction(world, partyCharacterIds);
            if (string.IsNullOrEmpty(factionId))
            {
                GUI.Label(new Rect(panelRect.x + 8f, panelRect.y + 8f, panelRect.width - 16f, 40f),
                    "无可用势力身份，无法组军。", _body);
                return false;
            }

            if (!string.IsNullOrEmpty(_detailArmyId) &&
                world.Strategic.FormalArmies.TryGet(_detailArmyId, out var detailArmyRef) &&
                detailArmyRef != null &&
                string.IsNullOrEmpty(_nodeId))
            {
                ArmyService.TryResolveArmySiteId(world, detailArmyRef, out var formationSiteId);
                _nodeId = formationSiteId ?? string.Empty;
            }

            var y = panelRect.y + 8f;
            if (!string.IsNullOrEmpty(_detailArmyId))
            {
                GUI.Label(new Rect(panelRect.x + 8f, y, panelRect.width - 16f, 22f), "军队详情", _title);
                y += 26f;
            }
            else
            {
                GUI.Label(new Rect(panelRect.x + 8f, y, panelRect.width - 16f, 22f), "组建军队", _title);
                y += 26f;
            }

            if (!string.IsNullOrEmpty(_lastMessage))
            {
                GUI.Label(new Rect(panelRect.x + 8f, y, panelRect.width - 16f, 36f), _lastMessage, _body);
                y += 40f;
            }

            if (!string.IsNullOrEmpty(_detailArmyId) &&
                world.Strategic.FormalArmies.TryGet(_detailArmyId, out var detailArmy) &&
                detailArmy != null)
            {
                changed |= DrawArmyDetail(panelRect, ref y, world, detailArmy, partyCharacterIds, labelFn);
            }
            else
            {
                changed |= DrawCreateForm(panelRect, ref y, world, factionId, partyCharacterIds, labelFn);
            }

            return changed;
        }

        bool DrawCreateForm(
            Rect panelRect,
            ref float y,
            SimulationWorld world,
            string factionId,
            IReadOnlyList<EntityId> partyCharacterIds,
            System.Func<SimulationWorld, EntityId, string> labelFn)
        {
            var changed = false;
            _scratchResidents.Clear();
            HostStrategicRosterQueries.CollectUngroupedPlayerCharacters(
                world, factionId, partyCharacterIds, _scratchResidents);

            GUI.Label(new Rect(panelRect.x + 8f, y, panelRect.width - 16f, 20f), "选择未编组角色", _body);
            y += 22f;

            if (_scratchResidents.Count < 1)
            {
                GUI.Label(new Rect(panelRect.x + 8f, y, panelRect.width - 16f, 36f),
                    "当前没有可组建军队的角色。", _body);
                return false;
            }

            for (var i = 0; i < _scratchResidents.Count; i++)
            {
                var id = _scratchResidents[i];
                var toggle = _createSelection.Contains(id.Value);
                var next = GUI.Toggle(
                    new Rect(panelRect.x + 8f, y, panelRect.width - 16f, 20f),
                    toggle,
                    labelFn(world, id));
                if (next != toggle)
                {
                    if (next)
                        _createSelection.Add(id.Value);
                    else
                        _createSelection.Remove(id.Value);
                }

                y += 22f;
            }

            if (GUI.Button(new Rect(panelRect.x + 8f, y, panelRect.width - 16f, 24f), "组建军队"))
            {
                FillScratchPartyFromSelection(_scratchParty);
                if (_scratchParty.Count < 1)
                {
                    _lastMessage = "请至少选择一名角色。";
                }
                else
                {
                    var nodeId = ArmyService.ResolveCharacterFormationLocationId(world, _scratchParty[0]) ?? string.Empty;
                    var result = ArmyUiCommands.TryCreateArmy(world, nodeId, factionId, _scratchParty);
                    if (result.IsSuccess)
                    {
                        _lastMessage = "已创建 " + result.Value.ArmyId;
                        _detailArmyId = result.Value.ArmyId;
                        _lastCreatedArmyId = result.Value.ArmyId;
                        _createSelection.Clear();
                        changed = true;
                    }
                    else
                    {
                        _lastMessage = ArmyUiCommands.DescribeError(result.Error);
                    }
                }
            }

            y += 28f;
            return changed;
        }

        void FillScratchPartyFromSelection(List<EntityId> into)
        {
            into.Clear();
            foreach (var idVal in _createSelection)
                into.Add(new EntityId(idVal));
        }

        bool DrawArmyDetail(
            Rect panelRect,
            ref float y,
            SimulationWorld world,
            FormalArmy army,
            IReadOnlyList<EntityId> partyCharacterIds,
            System.Func<SimulationWorld, EntityId, string> labelFn)
        {
            var changed = false;
            var factionId = army.FactionId;
            GUI.Label(new Rect(panelRect.x + 8f, y, panelRect.width - 16f, 20f), army.ArmyId, _title);
            y += 22f;
            GUI.Label(new Rect(panelRect.x + 8f, y, panelRect.width - 16f, 20f),
                "Leader：" + labelFn(world, army.LeaderCharacterId), _body);
            y += 22f;
            GUI.Label(new Rect(panelRect.x + 8f, y, panelRect.width - 16f, 20f),
                "Faction：" + StrategicFactionCatalog.DisplayName(army.FactionId), _body);
            y += 22f;
            GUI.Label(new Rect(panelRect.x + 8f, y, panelRect.width - 16f, 20f),
                "State：" + army.State, _body);
            y += 22f;
            ArmyService.TryResolveArmySiteId(world, army, out var armySiteId);
            var travel = HostStrategicRosterQueries.ResolveSiteLabel(world, armySiteId);
            if (army.State == FormalArmyState.Moving)
                travel += " → " + HostStrategicRosterQueries.DescribeHexLabel(world, army.DestinationHex);
            GUI.Label(new Rect(panelRect.x + 8f, y, panelRect.width - 16f, 20f),
                "Location：" + travel, _body);
            y += 22f;
            GUI.Label(new Rect(panelRect.x + 8f, y, panelRect.width - 16f, 18f), "Members：", _body);
            y += 20f;
            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var memberId = new EntityId(army.MemberCharacterIds[i]);
                var life = CombatLifeStateService.FormatLifeStateWithCountdown(world,
                    world.Entities.TryGet(memberId, out var memberEnt) ? memberEnt : null);
                var memberLabel = labelFn(world, memberId);
                if (!string.IsNullOrEmpty(life) && life != "存活")
                    memberLabel += " · " + life;
                GUI.Label(new Rect(panelRect.x + 16f, y, panelRect.width - 100f, 18f),
                    memberLabel, _body);
                if (GUI.Button(new Rect(panelRect.xMax - 88f, y, 80f, 18f), "Set Leader"))
                {
                    var cl = ArmyUiCommands.TryChangeLeader(world, army.ArmyId, memberId);
                    _lastMessage = cl.IsSuccess ? "已更新 Leader" : ArmyUiCommands.DescribeError(cl.Error);
                    changed |= cl.IsSuccess;
                }

                if (GUI.Button(new Rect(panelRect.xMax - 172f, y, 78f, 18f), "Remove"))
                {
                    var rm = ArmyUiCommands.TryRemoveMember(world, army.ArmyId, memberId);
                    _lastMessage = rm.IsSuccess ? "已移除成员" : ArmyUiCommands.DescribeError(rm.Error);
                    changed |= rm.IsSuccess;
                }

                y += 20f;
            }

            y += 4f;
            changed |= DrawAddMemberSection(panelRect, ref y, world, army, factionId, partyCharacterIds, labelFn);

            var garrisoned = army.State == FormalArmyState.Garrisoned;
            if (GUI.Button(new Rect(panelRect.x + 8f, y, (panelRect.width - 20f) * 0.5f, 24f),
                    garrisoned ? "解除驻扎 Mobilize" : "驻扎 Garrison"))
            {
                var result = garrisoned
                    ? ArmyUiCommands.TryMobilize(world, army.ArmyId)
                    : ArmyUiCommands.TryGarrison(world, army.ArmyId);
                _lastMessage = result.IsSuccess
                    ? (garrisoned ? "已解除驻扎，可移动／追击" : "已驻扎。")
                    : ArmyUiCommands.DescribeError(result.Error);
                changed |= result.IsSuccess;
            }

            if (GUI.Button(new Rect(panelRect.x + 12f + (panelRect.width - 20f) * 0.5f, y, (panelRect.width - 20f) * 0.5f, 24f),
                    "解散 Disband"))
            {
                var d = ArmyUiCommands.TryDisband(world, army.ArmyId);
                _lastMessage = d.IsSuccess ? "已解散。" : ArmyUiCommands.DescribeError(d.Error);
                if (d.IsSuccess)
                {
                    _detailArmyId = string.Empty;
                    _wasDisbanded = true;
                    changed = true;
                }
            }

            y += 28f;
            return changed;
        }

        bool DrawAddMemberSection(
            Rect panelRect,
            ref float y,
            SimulationWorld world,
            FormalArmy army,
            string factionId,
            IReadOnlyList<EntityId> partyCharacterIds,
            System.Func<SimulationWorld, EntityId, string> labelFn)
        {
            var changed = false;
            _scratchResidents.Clear();
            ArmyService.TryResolveArmySiteId(world, army, out var formSiteId);
            CollectUngroupedResidentsAtSite(world, formSiteId, factionId, partyCharacterIds, army, _scratchResidents);

            var available = 0;
            for (var i = 0; i < _scratchResidents.Count; i++)
            {
                if (!army.ContainsMember(_scratchResidents[i]))
                    available++;
            }

            GUI.Label(new Rect(panelRect.x + 8f, y, panelRect.width - 16f, 18f),
                "Available Residents (" + available + ")", _body);
            y += 20f;

            for (var i = 0; i < _scratchResidents.Count; i++)
            {
                var id = _scratchResidents[i];
                if (army.ContainsMember(id))
                    continue;
                var toggle = _addMemberSelection.Contains(id.Value);
                var next = GUI.Toggle(
                    new Rect(panelRect.x + 8f, y, panelRect.width - 16f, 18f),
                    toggle,
                    labelFn(world, id));
                if (next != toggle)
                {
                    if (next)
                        _addMemberSelection.Add(id.Value);
                    else
                        _addMemberSelection.Remove(id.Value);
                }

                y += 20f;
            }

            if (GUI.Button(new Rect(panelRect.x + 8f, y, panelRect.width - 16f, 22f), "Add Selected"))
            {
                var added = 0;
                foreach (var idVal in _addMemberSelection)
                {
                    var add = ArmyUiCommands.TryAddMember(world, army.ArmyId, new EntityId(idVal));
                    if (add.IsSuccess)
                        added++;
                    else if (added == 0)
                        _lastMessage = ArmyUiCommands.DescribeError(add.Error);
                }

                if (added > 0)
                {
                    _lastMessage = "已添加 " + added + " 名成员。";
                    _addMemberSelection.Clear();
                    changed = true;
                }
            }

            y += 26f;
            return changed;
        }

        static void CollectUngroupedResidentsAtSite(
            SimulationWorld world,
            string nodeId,
            string factionId,
            IReadOnlyList<EntityId> partyCharacterIds,
            FormalArmy army,
            List<EntityId> into)
        {
            into.Clear();
            if (world == null || into == null || army == null)
                return;

            if (partyCharacterIds != null)
            {
                ArmyService.CollectResidentsAtSite(
                    world, nodeId, factionId, partyCharacterIds, into, _scratchArmiesStatic);
                for (var i = into.Count - 1; i >= 0; i--)
                {
                    if (army.ContainsMember(into[i]))
                        into.RemoveAt(i);
                }
            }
        }

        static readonly List<FormalArmy> _scratchArmiesStatic = new List<FormalArmy>(4);

        static string ResolvePlayerFaction(SimulationWorld world, IReadOnlyList<EntityId> partyCharacterIds)
        {
            var fromParty = HousingAssignmentService.ResolvePlayerFactionId(world, partyCharacterIds);
            if (!string.IsNullOrEmpty(fromParty))
                return fromParty;
            return world?.Strategic?.PlayerFactionId ?? string.Empty;
        }
    }
}
