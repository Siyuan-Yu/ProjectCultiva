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
    /// <summary>Phase 3：FormalArmy DEBUG（LevelTester 验收）。</summary>
    public sealed class HostFormalArmyDebugPanel : MonoBehaviour
    {
        const int WindowId = 0xFA3D711;
        const float PanelWidth = 580f;
        const float LineH = 18f;

        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] bool visible;
        [SerializeField] KeyCode toggleKey = KeyCode.F11;
        [SerializeField] int targetHexQ = 12;
        [SerializeField] int targetHexR = 4;

        readonly List<EntityId> _candidates = new List<EntityId>(16);
        readonly List<string> _siteIds = new List<string>(16);
        readonly HashSet<ulong> _selectedMembers = new HashSet<ulong>();

        string _selectedArmyId = string.Empty;
        int _selectedSiteIndex;
        int _leaderIndex;
        string _actionLog = "F11 · FormalArmy DEBUG";
        Rect _panelRect;
        bool _panelRectInitialized;

        public void Bind(PlayableHostBootstrap host) => bootstrap = host;

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                visible = !visible;
        }

        void OnGUI()
        {
            if (!visible)
                return;

            EnsurePanelRect();
            _panelRect = GUI.Window(WindowId, _panelRect, DrawPanel, "FormalArmy DEBUG (F11)");
            HostUiHitTest.Block(_panelRect);
        }

        void EnsurePanelRect()
        {
            if (_panelRectInitialized)
                return;
            _panelRect = new Rect(24f, 120f, PanelWidth, 520f);
            _panelRectInitialized = true;
        }

        void DrawPanel(int id)
        {
            GUI.DragWindow(new Rect(0f, 0f, PanelWidth, 22f));
            var session = bootstrap?.Session;
            var world = session?.World;
            if (world == null)
            {
                GUILayout.Label("Session not ready.");
                return;
            }

            RefreshLists(world, session.PlayerParty);
            var y = 28f;
            var pad = 8f;
            var innerW = PanelWidth - pad * 2f;

            GUI.Label(new Rect(pad, y, innerW, LineH), "Selected Army: " + (_selectedArmyId ?? "(none)"));
            y += LineH + 4f;

            if (!string.IsNullOrEmpty(_selectedArmyId) &&
                world.Strategic.FormalArmies.TryGet(_selectedArmyId, out var army) &&
                army != null)
            {
                var motion = army.WorldMotion;
                GUI.Label(new Rect(pad, y, innerW, LineH),
                    "LocationKind=" + motion.LocationKind + " SiteId=" + (motion.SiteId ?? "—"));
                y += LineH;
                GUI.Label(new Rect(pad, y, innerW, LineH),
                    "WorldPos=(" + motion.WorldPosition.X.ToString("0.##") + "," +
                    motion.WorldPosition.Y.ToString("0.##") + ") Hex=" + motion.CurrentHex);
                y += LineH;
                GUI.Label(new Rect(pad, y, innerW, LineH),
                    "InsideSite=" + (motion.LocationKind == FormalArmyLocationKind.AtWorldSite) +
                    " Order=" + motion.CurrentOrderKind +
                    " Dest=" + motion.DestinationHex +
                    (string.IsNullOrEmpty(motion.DestinationSiteId) ? "" : "→" + motion.DestinationSiteId));
                y += LineH;
                GUI.Label(new Rect(pad, y, innerW, LineH),
                    "Travel Moving=" + motion.IsMoving +
                    " Seg=" + motion.SegmentIndex + "/" + Math.Max(0, motion.HexPathCount - 1) +
                    " Prog=" + motion.SegmentProgress.ToString("0.##") +
                    " SiteDeparturePending=" + motion.IsSiteDeparturePending);
                y += LineH;
                GUI.Label(new Rect(pad, y, innerW, LineH),
                    "Leader=" + army.LeaderCharacterId.Value + " Members=" + army.MemberCharacterIds.Count);
                y += LineH + 4f;
            }

            GUI.Label(new Rect(pad, y, innerW, LineH), "Formation Site:");
            y += LineH;
            if (_siteIds.Count > 0)
            {
                _selectedSiteIndex = Mathf.Clamp(_selectedSiteIndex, 0, _siteIds.Count - 1);
                _selectedSiteIndex = (int)GUI.HorizontalSlider(new Rect(pad, y, innerW, 20f), _selectedSiteIndex, 0, _siteIds.Count - 1);
                y += 22f;
                GUI.Label(new Rect(pad, y, innerW, LineH), _siteIds[_selectedSiteIndex]);
                y += LineH + 4f;
            }

            GUI.Label(new Rect(pad, y, innerW, LineH), "Candidates (toggle member):");
            y += LineH;
            for (var i = 0; i < _candidates.Count; i++)
            {
                var cid = _candidates[i];
                world.Entities.TryGet(cid, out var ent);
                var label = ent != null ? ent.DisplayName : cid.Value.ToString();
                var selected = _selectedMembers.Contains(cid.Value);
                if (GUI.Toggle(new Rect(pad, y, innerW, LineH), selected, label))
                    _selectedMembers.Add(cid.Value);
                else
                    _selectedMembers.Remove(cid.Value);
                y += LineH;
            }

            y += 4f;
            if (GUI.Button(new Rect(pad, y, innerW * 0.48f, 24f), "Create Army"))
                CreateArmy(world, session.PlayerParty);
            if (GUI.Button(new Rect(pad + innerW * 0.52f, y, innerW * 0.48f, 24f), "Disband Army"))
                DisbandArmy(world);
            y += 28f;

            targetHexQ = (int)GUI.HorizontalSlider(new Rect(pad, y, innerW * 0.45f, 20f), targetHexQ, 0, 29);
            targetHexR = (int)GUI.HorizontalSlider(new Rect(pad + innerW * 0.5f, y, innerW * 0.45f, 20f), targetHexR, 0, 14);
            y += 22f;
            GUI.Label(new Rect(pad, y, innerW, LineH), "Target Hex: (" + targetHexQ + "," + targetHexR + ")");
            y += LineH + 4f;

            if (GUI.Button(new Rect(pad, y, innerW * 0.48f, 24f), "Travel To Hex"))
                TravelToHex(world);
            if (GUI.Button(new Rect(pad + innerW * 0.52f, y, innerW * 0.48f, 24f), "Travel To Site"))
                TravelToSite(world);
            y += 28f;
            if (GUI.Button(new Rect(pad, y, innerW * 0.48f, 24f), "Advance 16 Ticks"))
                AdvanceTicks(16);
            if (GUI.Button(new Rect(pad + innerW * 0.52f, y, innerW * 0.48f, 24f), "Select First Army"))
                SelectFirstArmy(world);
            y += 28f;

            if (!string.IsNullOrEmpty(_selectedArmyId) &&
                world.Strategic.FormalArmies.TryGet(_selectedArmyId, out var casualtyArmy) &&
                casualtyArmy != null &&
                casualtyArmy.MemberCharacterIds.Count > 0)
            {
                if (GUI.Button(new Rect(pad, y, innerW * 0.48f, 24f), "Incap Leader (G16)"))
                    IncapacitateArmyMember(world, casualtyArmy, casualtyArmy.LeaderCharacterId);
                if (GUI.Button(new Rect(pad + innerW * 0.52f, y, innerW * 0.48f, 24f), "Incap Member[1] (G17)"))
                {
                    var idx = casualtyArmy.MemberCharacterIds.Count > 1 ? 1 : 0;
                    IncapacitateArmyMember(
                        world, casualtyArmy, new EntityId(casualtyArmy.MemberCharacterIds[idx]));
                }
                y += 28f;
                if (GUI.Button(new Rect(pad, y, innerW, 24f), "Sync Casualties (G16/G17/G18)"))
                    SyncCasualties(world, casualtyArmy);
                y += 28f;
            }

            if (!string.IsNullOrEmpty(_selectedArmyId) &&
                world.Strategic.FormalArmies.TryGet(_selectedArmyId, out var inspectArmy) &&
                inspectArmy != null)
            {
                for (var mi = 0; mi < inspectArmy.MemberCharacterIds.Count; mi++)
                {
                    var memberId = new EntityId(inspectArmy.MemberCharacterIds[mi]);
                    world.WorldPresence.TryGet(memberId, out var memberPresence);
                    var life = world.Entities.TryGet(memberId, out var memberEnt)
                        ? CombatLifeStateService.FormatLifeStateWithCountdown(world, memberEnt)
                        : "?";
                    GUI.Label(new Rect(pad, y, innerW, LineH),
                        "Member " + memberId.Value + " Life=" + life +
                        " PresenceHex=" + (memberPresence?.ResidualHex.ToString() ?? "—"));
                    y += LineH;
                }
                y += 4f;
            }

            GUI.Label(new Rect(pad, y, innerW, 40f), _actionLog);
        }

        void RefreshLists(SimulationWorld world, PlayerPartyRuntime party)
        {
            _siteIds.Clear();
            var factionId = world.Strategic.PlayerFactionId;
            foreach (var kv in world.Strategic.Sites.Sites)
            {
                if (kv.Value != null &&
                    FormalArmyManagementSitePolicy.CanManageFormalArmyAtSite(world, kv.Key, factionId))
                    _siteIds.Add(kv.Key);
            }

            _candidates.Clear();
            if (_siteIds.Count == 0)
                return;
            _selectedSiteIndex = Mathf.Clamp(_selectedSiteIndex, 0, _siteIds.Count - 1);
            var siteId = _siteIds[_selectedSiteIndex];
            foreach (var kv in world.WorldPresence.All)
            {
                var presence = kv.Value;
                if (presence == null || presence.EntityId.IsNone)
                    continue;
                if (presence.Mode != XianXia.Core.World.PartyWorldPresenceMode.AtSite)
                    continue;
                if (!string.Equals(presence.SiteId, siteId, System.StringComparison.Ordinal))
                    continue;
                if (!ArmyService.IsEligibleFormalArmyCandidate(world, presence.EntityId, party, out _))
                    continue;
                if (!string.Equals(
                        ArmyService.ResolveCharacterFactionId(world, presence.EntityId),
                        factionId,
                        System.StringComparison.Ordinal))
                    continue;
                _candidates.Add(presence.EntityId);
            }
        }

        void CreateArmy(SimulationWorld world, PlayerPartyRuntime party)
        {
            if (_siteIds.Count == 0 || _selectedMembers.Count < 1)
            {
                _actionLog = "Select site and at least one member.";
                return;
            }

            var members = new List<EntityId>(_selectedMembers.Count);
            foreach (var v in _selectedMembers)
                members.Add(new EntityId(v));
            _leaderIndex = Mathf.Clamp(_leaderIndex, 0, members.Count - 1);
            var leader = members[_leaderIndex];
            var siteId = _siteIds[_selectedSiteIndex];
            var result = ArmyService.CreateArmy(
                world,
                world.Strategic.PlayerFactionId,
                siteId,
                members,
                leader,
                party);
            _actionLog = result.IsSuccess
                ? "Created army " + result.Value.ArmyId
                : result.Error.ToString();
            if (result.IsSuccess)
            {
                _selectedArmyId = result.Value.ArmyId;
                _selectedMembers.Clear();
            }
        }

        void DisbandArmy(SimulationWorld world)
        {
            if (string.IsNullOrEmpty(_selectedArmyId))
            {
                _actionLog = "No army selected.";
                return;
            }

            var result = ArmyService.DisbandArmy(world, _selectedArmyId);
            _actionLog = result.IsSuccess ? "Disbanded." : result.Error.ToString();
            if (result.IsSuccess)
                _selectedArmyId = string.Empty;
        }

        void TravelToHex(SimulationWorld world)
        {
            if (string.IsNullOrEmpty(_selectedArmyId))
            {
                _actionLog = "Select army first.";
                return;
            }

            var hex = new XianXia.Core.World.Hex.HexCoord(targetHexQ, targetHexR);
            var result = FormalArmyContinuousTravelService.MoveArmyToHex(world, _selectedArmyId, hex);
            _actionLog = result.IsSuccess ? "Travel To Hex started." : result.Error.ToString();
        }

        void TravelToSite(SimulationWorld world)
        {
            if (string.IsNullOrEmpty(_selectedArmyId) || _siteIds.Count == 0)
            {
                _actionLog = "Select army and site.";
                return;
            }

            var siteId = _siteIds[_selectedSiteIndex];
            var result = FormalArmyContinuousTravelService.MoveArmyToWorldSite(world, _selectedArmyId, siteId);
            _actionLog = result.IsSuccess ? "Travel To Site started → " + siteId : result.Error.ToString();
        }

        void AdvanceTicks(int ticks)
        {
            var loop = bootstrap?.Session?.Loop;
            if (loop == null)
            {
                _actionLog = "Loop missing.";
                return;
            }

            for (var i = 0; i < ticks; i++)
                loop.TickOnce();
            _actionLog = "Advanced " + ticks + " ticks.";
        }

        void SelectFirstArmy(SimulationWorld world)
        {
            foreach (var kv in world.Strategic.FormalArmies.Armies)
            {
                _selectedArmyId = kv.Key;
                _actionLog = "Selected " + kv.Key;
                return;
            }

            _actionLog = "No armies.";
        }

        void IncapacitateArmyMember(SimulationWorld world, FormalArmy army, EntityId memberId)
        {
            if (world == null || army == null || memberId.IsNone)
            {
                _actionLog = "Invalid incap target.";
                return;
            }

            if (!world.Entities.TryGet(memberId, out var entity) || entity == null)
            {
                _actionLog = "Member entity missing.";
                return;
            }

            CombatDamageRules.EnsureVitals(entity);
            if (!CombatLifeStateService.TryEnterIncapacitated(world, entity))
            {
                _actionLog = "Incap failed for " + memberId.Value;
                return;
            }

            ArmyService.SyncNonLivingMembers(world, army);
            _actionLog = "Incapacitated " + memberId.Value + " and synced casualties.";
        }

        void SyncCasualties(SimulationWorld world, FormalArmy army)
        {
            if (world == null || army == null)
            {
                _actionLog = "No army for casualty sync.";
                return;
            }

            ArmyService.SyncNonLivingMembers(world, army);
            if (world.Strategic.FormalArmies.TryGet(army.ArmyId, out _))
                _actionLog = "Synced casualties for " + army.ArmyId + ".";
            else
                _actionLog = "Army destroyed after casualty sync (G18).";
        }
    }
}
