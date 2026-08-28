using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>LevelTester Cheat · FormalArmy。</summary>
    public sealed class LevelTesterCheatFormalArmySection
    {
        readonly List<EntityId> _candidates = new List<EntityId>(16);
        readonly List<string> _siteIds = new List<string>(16);
        readonly List<string> _armyIds = new List<string>(16);
        readonly List<EntityId> _createMembers = new List<EntityId>(16);
        readonly HashSet<ulong> _createMemberSet = new HashSet<ulong>();

        string _selectedArmyId = string.Empty;
        int _selectedSiteIndex;
        int _selectedArmyIndex;
        int _createLeaderIndex;
        int _incapTargetIndex;
        string _hexQText = "12";
        string _hexRText = "4";
        string _sectionStatus = string.Empty;

        public string SectionStatus => _sectionStatus;

        public float Draw(
            PlayableHostBootstrap bootstrap,
            float x,
            float y,
            float width,
            GUIStyle body)
        {
            var lineH = 18f;
            var session = bootstrap?.Session;
            var world = session?.World;
            var party = session?.PlayerParty;

            GUI.Label(new Rect(x, y, width, lineH), "正规军", body);
            y += lineH + 4f;

            if (world == null || !session.IsInitialized)
            {
                GUI.Label(new Rect(x, y, width, lineH), "会话未就绪。");
                return y + lineH;
            }

            RefreshSites(world, party);
            RefreshArmyList(world);
            EnsureArmySelection();

            GUI.Label(new Rect(x, y, 70f, lineH), "军队");
            if (_armyIds.Count > 0)
            {
                _selectedArmyIndex = Mathf.Clamp(_selectedArmyIndex, 0, _armyIds.Count - 1);
                if (GUI.Button(new Rect(x + 72f, y, 28f, 22f), "<"))
                    _selectedArmyIndex = (_selectedArmyIndex + _armyIds.Count - 1) % _armyIds.Count;
                if (GUI.Button(new Rect(x + width - 28f, y, 28f, 22f), ">"))
                    _selectedArmyIndex = (_selectedArmyIndex + 1) % _armyIds.Count;
                _selectedArmyId = _armyIds[_selectedArmyIndex];
                GUI.Label(new Rect(x + 104f, y, width - 140f, lineH), _selectedArmyId, body);
            }
            else
            {
                GUI.Label(new Rect(x + 72f, y, width - 72f, lineH), "（无军队）", body);
                _selectedArmyId = string.Empty;
            }

            y += 24f;
            y = DrawSelectedArmyStatus(world, x, y, width, lineH, body);
            y += 6f;

            GUI.Label(new Rect(x, y, width, lineH), "— 创建军队 —", body);
            y += lineH;
            if (_siteIds.Count > 0)
            {
                _selectedSiteIndex = Mathf.Clamp(_selectedSiteIndex, 0, _siteIds.Count - 1);
                _selectedSiteIndex = (int)GUI.HorizontalSlider(
                    new Rect(x, y, width, 20f), _selectedSiteIndex, 0, _siteIds.Count - 1);
                y += 22f;
                GUI.Label(new Rect(x, y, width, lineH), "组建地点：" + _siteIds[_selectedSiteIndex], body);
                y += lineH;
            }

            RefreshCandidates(world, party);
            _createMembers.Clear();
            for (var i = 0; i < _candidates.Count; i++)
            {
                var cid = _candidates[i];
                world.Entities.TryGet(cid, out var ent);
                var label = ent != null ? ent.DisplayName : cid.Value.ToString();
                var selected = _createMemberSet.Contains(cid.Value);
                var next = GUI.Toggle(new Rect(x, y, width, lineH), selected, label);
                if (next != selected)
                {
                    if (next)
                        _createMemberSet.Add(cid.Value);
                    else
                        _createMemberSet.Remove(cid.Value);
                }

                y += lineH;
            }

            RebuildCreateMembersFromSet();
            if (_createMembers.Count > 0)
            {
                GUI.Label(new Rect(x, y, width, lineH), "队长（须为已选成员）：", body);
                y += lineH;
                _createLeaderIndex = Mathf.Clamp(_createLeaderIndex, 0, _createMembers.Count - 1);
                for (var li = 0; li < _createMembers.Count; li++)
                {
                    world.Entities.TryGet(_createMembers[li], out var lm);
                    var llabel = lm != null ? lm.DisplayName : _createMembers[li].Value.ToString();
                    if (GUI.Toggle(new Rect(x, y, width, lineH), _createLeaderIndex == li, "队长：" + llabel))
                        _createLeaderIndex = li;
                    y += lineH;
                }
            }

            if (GUI.Button(new Rect(x, y, width * 0.48f, 24f), "创建军队"))
                CreateArmy(world, party);
            if (GUI.Button(new Rect(x + width * 0.52f, y, width * 0.48f, 24f), "解散军队"))
                DisbandArmy(world);
            y += 28f;

            GUI.Label(new Rect(x, y, 24f, lineH), "Q");
            _hexQText = GUI.TextField(new Rect(x + 26f, y, 48f, 22f), _hexQText);
            GUI.Label(new Rect(x + 80f, y, 24f, lineH), "R");
            _hexRText = GUI.TextField(new Rect(x + 106f, y, 48f, 22f), _hexRText);
            y += 26f;

            if (GUI.Button(new Rect(x, y, width * 0.48f, 24f), "前往 Hex"))
                TravelToHex(world);
            if (GUI.Button(new Rect(x + width * 0.52f, y, width * 0.48f, 24f), "前往地点"))
                TravelToSite(world);
            y += 28f;

            if (!string.IsNullOrEmpty(_selectedArmyId) &&
                world.Strategic.FormalArmies.TryGet(_selectedArmyId, out var incapArmy) &&
                incapArmy != null &&
                incapArmy.MemberCharacterIds.Count > 0)
            {
                GUI.Label(new Rect(x, y, width, lineH), "失能目标：", body);
                y += lineH;
                var memberCount = incapArmy.MemberCharacterIds.Count;
                _incapTargetIndex = Mathf.Clamp(_incapTargetIndex, 0, memberCount - 1);
                for (var mi = 0; mi < memberCount; mi++)
                {
                    var mid = new EntityId(incapArmy.MemberCharacterIds[mi]);
                    world.Entities.TryGet(mid, out var ment);
                    var mlabel = ment != null ? ment.DisplayName : mid.Value.ToString();
                    var isLeader = mid == incapArmy.LeaderCharacterId;
                    if (GUI.Toggle(new Rect(x, y, width, lineH), _incapTargetIndex == mi,
                            (isLeader ? "[队长] " : "") + mlabel))
                        _incapTargetIndex = mi;
                    y += lineH;
                }

                if (GUI.Button(new Rect(x, y, width, 24f), "使选中成员失能"))
                    IncapacitateSelected(world, incapArmy);
                y += 28f;
                if (GUI.Button(new Rect(x, y, width, 24f), "同步伤亡"))
                {
                    ArmyService.SyncNonLivingMembers(world, incapArmy);
                    _sectionStatus = world.Strategic.FormalArmies.TryGet(incapArmy.ArmyId, out _)
                        ? "成功：已同步伤亡。"
                        : "成功：同步后军队已销毁（G18）。";
                }

                y += 28f;
            }

            if (!string.IsNullOrEmpty(_sectionStatus))
            {
                GUI.Label(new Rect(x, y, width, lineH * 3f), _sectionStatus, body);
                y += lineH * 3f;
            }

            return y;
        }

        void RebuildCreateMembersFromSet()
        {
            _createMembers.Clear();
            for (var i = 0; i < _candidates.Count; i++)
            {
                var cid = _candidates[i];
                if (_createMemberSet.Contains(cid.Value))
                    _createMembers.Add(cid);
            }

            if (_createLeaderIndex >= _createMembers.Count)
                _createLeaderIndex = System.Math.Max(0, _createMembers.Count - 1);
        }

        float DrawSelectedArmyStatus(
            SimulationWorld world,
            float x,
            float y,
            float width,
            float lineH,
            GUIStyle body)
        {
            if (string.IsNullOrEmpty(_selectedArmyId) ||
                !world.Strategic.FormalArmies.TryGet(_selectedArmyId, out var army) ||
                army == null)
            {
                GUI.Label(new Rect(x, y, width, lineH), "未选择军队。", body);
                return y + lineH;
            }

            var motion = army.WorldMotion;
            y = DrawLine(x, y, width, lineH, body, "队长", army.LeaderCharacterId.Value.ToString());
            y = DrawLine(x, y, width, lineH, body, "成员", army.MemberCharacterIds.Count.ToString());
            y = DrawLine(x, y, width, lineH, body, "Hex", motion.CurrentHex.ToString());
            y = DrawLine(x, y, width, lineH, body, "WorldPos",
                "(" + motion.WorldPosition.X.ToString("0.0") + "," + motion.WorldPosition.Y.ToString("0.0") + ")");
            y = DrawLine(x, y, width, lineH, body, "TravelState",
                motion.IsMoving ? "Moving seg=" + motion.SegmentIndex : army.State.ToString());
            y = DrawLine(x, y, width, lineH, body, "Order", motion.CurrentOrderKind.ToString());
            y = DrawLine(x, y, width, lineH, body, "目的地",
                string.IsNullOrEmpty(motion.DestinationSiteId)
                    ? motion.DestinationHex.ToString()
                    : motion.DestinationSiteId);

            for (var mi = 0; mi < army.MemberCharacterIds.Count; mi++)
            {
                var memberId = new EntityId(army.MemberCharacterIds[mi]);
                world.WorldPresence.TryGet(memberId, out var presence);
                var life = world.Entities.TryGet(memberId, out var memberEnt)
                    ? CombatLifeStateService.FormatLifeStateWithCountdown(world, memberEnt)
                    : "?";
                GUI.Label(new Rect(x, y, width, lineH),
                    "  " + memberId.Value + " 生命=" + life +
                    " 存在=" + (presence?.ResidualHex.ToString() ?? "-"), body);
                y += lineH;
            }

            return y;
        }

        static float DrawLine(float x, float y, float width, float lineH, GUIStyle body, string key, string value)
        {
            GUI.Label(new Rect(x, y, 90f, lineH), key, body);
            GUI.Label(new Rect(x + 92f, y, width - 92f, lineH), value ?? "-", body);
            return y + lineH;
        }

        void RefreshSites(SimulationWorld world, PlayerPartyRuntime party)
        {
            _siteIds.Clear();
            var factionId = world.Strategic.PlayerFactionId;
            foreach (var kv in world.Strategic.Sites.Sites)
            {
                if (kv.Value != null &&
                    FormalArmyManagementSitePolicy.CanManageFormalArmyAtSite(world, kv.Key, factionId))
                    _siteIds.Add(kv.Key);
            }
        }

        void RefreshCandidates(SimulationWorld world, PlayerPartyRuntime party)
        {
            _candidates.Clear();
            if (_siteIds.Count == 0)
                return;
            _selectedSiteIndex = Mathf.Clamp(_selectedSiteIndex, 0, _siteIds.Count - 1);
            var siteId = _siteIds[_selectedSiteIndex];
            var factionId = world.Strategic.PlayerFactionId;
            foreach (var kv in world.WorldPresence.All)
            {
                var presence = kv.Value;
                if (presence == null || presence.EntityId.IsNone)
                    continue;
                if (presence.Mode != PartyWorldPresenceMode.AtSite)
                    continue;
                if (!string.Equals(presence.SiteId, siteId, StringComparison.Ordinal))
                    continue;
                if (!ArmyService.IsEligibleFormalArmyCandidate(world, presence.EntityId, party, out _))
                    continue;
                if (!string.Equals(
                        ArmyService.ResolveCharacterFactionId(world, presence.EntityId),
                        factionId,
                        StringComparison.Ordinal))
                    continue;
                _candidates.Add(presence.EntityId);
            }
        }

        void RefreshArmyList(SimulationWorld world)
        {
            _armyIds.Clear();
            foreach (var kv in world.Strategic.FormalArmies.Armies)
                _armyIds.Add(kv.Key);
            _armyIds.Sort(StringComparer.Ordinal);
        }

        void EnsureArmySelection()
        {
            if (_armyIds.Count == 0)
            {
                _selectedArmyId = string.Empty;
                _selectedArmyIndex = 0;
                return;
            }

            if (!string.IsNullOrEmpty(_selectedArmyId))
            {
                var idx = _armyIds.IndexOf(_selectedArmyId);
                if (idx >= 0)
                {
                    _selectedArmyIndex = idx;
                    return;
                }
            }

            _selectedArmyIndex = Mathf.Clamp(_selectedArmyIndex, 0, _armyIds.Count - 1);
            _selectedArmyId = _armyIds[_selectedArmyIndex];
        }

        void CreateArmy(SimulationWorld world, PlayerPartyRuntime party)
        {
            RebuildCreateMembersFromSet();
            if (_siteIds.Count == 0 || _createMembers.Count < 1)
            {
                _sectionStatus = "失败：请选择地点和至少一名成员。";
                return;
            }

            var leader = _createMembers[Mathf.Clamp(_createLeaderIndex, 0, _createMembers.Count - 1)];
            var siteId = _siteIds[_selectedSiteIndex];
            var result = ArmyService.CreateArmy(
                world,
                world.Strategic.PlayerFactionId,
                siteId,
                _createMembers,
                leader,
                party);
            if (result.IsSuccess)
            {
                _selectedArmyId = result.Value.ArmyId;
                _createMemberSet.Clear();
                _createMembers.Clear();
                _sectionStatus = "成功：已创建 " + result.Value.ArmyId;
            }
            else
            {
                _sectionStatus = "失败：" + result.Error;
            }
        }

        void DisbandArmy(SimulationWorld world)
        {
            if (string.IsNullOrEmpty(_selectedArmyId))
            {
                _sectionStatus = "失败：未选择军队。";
                return;
            }

            var result = ArmyService.DisbandArmy(world, _selectedArmyId);
            if (result.IsSuccess)
            {
                _sectionStatus = "成功：已解散。";
                _selectedArmyId = string.Empty;
            }
            else
            {
                _sectionStatus = "失败：" + result.Error;
            }
        }

        void TravelToHex(SimulationWorld world)
        {
            if (string.IsNullOrEmpty(_selectedArmyId))
            {
                _sectionStatus = "失败：请选择军队。";
                return;
            }

            if (!int.TryParse(_hexQText, out var q) || !int.TryParse(_hexRText, out var r))
            {
                _sectionStatus = "失败：Hex 无效。";
                return;
            }

            var result = FormalArmyContinuousTravelService.MoveArmyToHex(
                world, _selectedArmyId, new XianXia.Core.World.Hex.HexCoord(q, r));
            _sectionStatus = result.IsSuccess ? "成功：已开始前往 Hex。" : "失败：" + result.Error;
        }

        void TravelToSite(SimulationWorld world)
        {
            if (string.IsNullOrEmpty(_selectedArmyId) || _siteIds.Count == 0)
            {
                _sectionStatus = "失败：请选择军队和地点。";
                return;
            }

            var siteId = _siteIds[Mathf.Clamp(_selectedSiteIndex, 0, _siteIds.Count - 1)];
            var result = FormalArmyContinuousTravelService.MoveArmyToWorldSite(world, _selectedArmyId, siteId);
            _sectionStatus = result.IsSuccess
                ? "成功：前往地点 -> " + siteId
                : "失败：" + result.Error;
        }

        void IncapacitateSelected(SimulationWorld world, FormalArmy army)
        {
            if (army == null || army.MemberCharacterIds.Count == 0)
            {
                _sectionStatus = "失败：无成员。";
                return;
            }

            var idx = Mathf.Clamp(_incapTargetIndex, 0, army.MemberCharacterIds.Count - 1);
            var memberId = new EntityId(army.MemberCharacterIds[idx]);
            if (!world.Entities.TryGet(memberId, out var entity) || entity == null)
            {
                _sectionStatus = "失败：成员实体缺失。";
                return;
            }

            CombatDamageRules.EnsureVitals(entity);
            if (!CombatLifeStateService.TryEnterIncapacitated(world, entity))
            {
                _sectionStatus = "失败：失能失败 " + memberId.Value;
                return;
            }

            ArmyService.SyncNonLivingMembers(world, army);
            _sectionStatus = "成功：已使 " + memberId.Value + " 失能";
        }
    }
}
