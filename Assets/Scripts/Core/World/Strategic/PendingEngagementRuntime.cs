using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Pending Engagement Domain Runtime State（Phase 4 Battle Authority 真源）。</summary>
    public sealed class PendingEngagementRuntime
    {
        readonly List<string> _playerFormalArmyIds = new List<string>(8);
        readonly List<string> _enemyFormalArmyIds = new List<string>(8);
        readonly List<ulong> _playerPartyMemberIds = new List<ulong>(8);

        public string EngagementId { get; set; } = string.Empty;
        public BattleInitiatorKind InitiatorKind { get; set; } = BattleInitiatorKind.None;
        public string InitiatorFormalArmyId { get; set; } = string.Empty;
        public bool InitiatorIsPlayerSide { get; set; }
        public BattleDecisionSubjectKind DecisionSubjectKind { get; set; } = BattleDecisionSubjectKind.None;
        public string DecisionSubjectFormalArmyId { get; set; } = string.Empty;
        /// <summary>接战创建瞬间冻结的 BattleLocationHex（Participant 空间唯一 Authority）。</summary>
        public int BattleLocationHexQ { get; set; } = ArmyHexBattleAnchorService.InvalidHexComponent;
        public int BattleLocationHexR { get; set; } = ArmyHexBattleAnchorService.InvalidHexComponent;
        /// <summary>Debug-only：BattleInitiator 位置快照；不参与 Participant 空间资格。</summary>
        public int InitiatorEngagementHexQ { get; set; } = ArmyHexBattleAnchorService.InvalidHexComponent;
        public int InitiatorEngagementHexR { get; set; } = ArmyHexBattleAnchorService.InvalidHexComponent;
        public string InitiatorEngagementSiteId { get; set; } = string.Empty;
        public string PrimaryPlayerFactionId { get; set; } = string.Empty;
        public string PrimaryEnemyFactionId { get; set; } = string.Empty;
        public string AttackerFormalArmyId { get; set; } = string.Empty;
        public string DefenderFormalArmyId { get; set; } = string.Empty;
        public bool PlayerPartyIncluded { get; set; }
        public string PlayerInclusionReason { get; set; } = BattleParticipantInclusionReason.None;
        public BattlePlayerInclusionPipelineTrace PlayerInclusionTrace { get; } =
            new BattlePlayerInclusionPipelineTrace();
        public string PendingBattleTriggerReason { get; set; } = string.Empty;
        public int InitiatorCommittedHexQ { get; set; } = ArmyHexBattleAnchorService.InvalidHexComponent;
        public int InitiatorCommittedHexR { get; set; } = ArmyHexBattleAnchorService.InvalidHexComponent;
        public int DefenderCommittedHexQ { get; set; } = ArmyHexBattleAnchorService.InvalidHexComponent;
        public int DefenderCommittedHexR { get; set; } = ArmyHexBattleAnchorService.InvalidHexComponent;
        public bool InvolvesPlayerSide { get; set; }
        public bool RequiresPlayerDecision { get; set; }
        public PreEngagementLegalLocation DecisionSubjectRetreatLocation { get; set; }
        BattleEngagementSupportArea _supportArea;

        public IReadOnlyList<string> LockedPlayerFormalArmyIds => _playerFormalArmyIds;
        public IReadOnlyList<string> LockedEnemyFormalArmyIds => _enemyFormalArmyIds;
        public IReadOnlyList<ulong> LockedPlayerPartyMemberIds => _playerPartyMemberIds;

        public bool IsActive => !string.IsNullOrEmpty(EngagementId);

        public bool HasBattleLocation =>
            BattleLocationHexQ != ArmyHexBattleAnchorService.InvalidHexComponent &&
            BattleLocationHexR != ArmyHexBattleAnchorService.InvalidHexComponent;

        public HexCoord BattleLocation =>
            new HexCoord(BattleLocationHexQ, BattleLocationHexR);

        public bool HasInitiatorEngagementLocation =>
            InitiatorEngagementHexQ != ArmyHexBattleAnchorService.InvalidHexComponent &&
            InitiatorEngagementHexR != ArmyHexBattleAnchorService.InvalidHexComponent;

        public InitiatorEngagementLocation InitiatorEngagementLocation =>
            new InitiatorEngagementLocation(
                new HexCoord(InitiatorEngagementHexQ, InitiatorEngagementHexR),
                InitiatorEngagementSiteId,
                HasInitiatorEngagementLocation);

        public bool HasSupportArea => _supportArea != null && _supportArea.HasValue;

        public BattleEngagementSupportArea SupportArea => _supportArea;

        public HexCoord InitiatorCommittedHex =>
            new HexCoord(InitiatorCommittedHexQ, InitiatorCommittedHexR);

        public HexCoord DefenderCommittedHex =>
            new HexCoord(DefenderCommittedHexQ, DefenderCommittedHexR);

        public bool HasInitiatorCommittedHex =>
            InitiatorCommittedHexQ != ArmyHexBattleAnchorService.InvalidHexComponent &&
            InitiatorCommittedHexR != ArmyHexBattleAnchorService.InvalidHexComponent;

        public void SetTriggerSpatialSnapshot(
            string triggerReason,
            HexCoord initiatorCommittedHex,
            HexCoord defenderCommittedHex)
        {
            PendingBattleTriggerReason = triggerReason ?? string.Empty;
            InitiatorCommittedHexQ = initiatorCommittedHex.Q;
            InitiatorCommittedHexR = initiatorCommittedHex.R;
            DefenderCommittedHexQ = defenderCommittedHex.Q;
            DefenderCommittedHexR = defenderCommittedHex.R;
        }

        public void Clear()
        {
            EngagementId = string.Empty;
            InitiatorKind = BattleInitiatorKind.None;
            InitiatorFormalArmyId = string.Empty;
            InitiatorIsPlayerSide = false;
            DecisionSubjectKind = BattleDecisionSubjectKind.None;
            DecisionSubjectFormalArmyId = string.Empty;
            BattleLocationHexQ = ArmyHexBattleAnchorService.InvalidHexComponent;
            BattleLocationHexR = ArmyHexBattleAnchorService.InvalidHexComponent;
            InitiatorEngagementHexQ = ArmyHexBattleAnchorService.InvalidHexComponent;
            InitiatorEngagementHexR = ArmyHexBattleAnchorService.InvalidHexComponent;
            InitiatorEngagementSiteId = string.Empty;
            PrimaryPlayerFactionId = string.Empty;
            PrimaryEnemyFactionId = string.Empty;
            AttackerFormalArmyId = string.Empty;
            DefenderFormalArmyId = string.Empty;
            PlayerPartyIncluded = false;
            PlayerInclusionReason = BattleParticipantInclusionReason.None;
            PlayerInclusionTrace.PlayerIncludedBeforeGathering = false;
            PlayerInclusionTrace.PlayerIncludedAfterGathering = false;
            PlayerInclusionTrace.PlayerIncludedAfterSnapshot = false;
            PlayerInclusionTrace.PlayerInSnapshotRecords = false;
            PlayerInclusionTrace.PlayerIncludedReason = BattleParticipantInclusionReason.None;
            PlayerInclusionTrace.PlayerHexAuthoritySource = string.Empty;
            PlayerInclusionTrace.PlayerHexFormatted = string.Empty;
            PlayerInclusionTrace.SupportContainsPlayerHex = string.Empty;
            PlayerInclusionTrace.LastPlayerWriteSource = string.Empty;
            PendingBattleTriggerReason = string.Empty;
            InitiatorCommittedHexQ = ArmyHexBattleAnchorService.InvalidHexComponent;
            InitiatorCommittedHexR = ArmyHexBattleAnchorService.InvalidHexComponent;
            DefenderCommittedHexQ = ArmyHexBattleAnchorService.InvalidHexComponent;
            DefenderCommittedHexR = ArmyHexBattleAnchorService.InvalidHexComponent;
            InvolvesPlayerSide = false;
            RequiresPlayerDecision = false;
            DecisionSubjectRetreatLocation = null;
            _supportArea = null;
            _playerFormalArmyIds.Clear();
            _enemyFormalArmyIds.Clear();
            _playerPartyMemberIds.Clear();
        }

        public void SetBattleLocation(HexCoord hex)
        {
            BattleLocationHexQ = hex.Q;
            BattleLocationHexR = hex.R;
        }

        public void SetSupportArea(BattleEngagementSupportArea supportArea)
        {
            _supportArea = supportArea;
            if (supportArea != null && supportArea.HasValue && !supportArea.PresentationAnchorHex.Equals(default))
                SetBattleLocation(supportArea.PresentationAnchorHex);
        }

        public void SetInitiatorEngagementLocation(InitiatorEngagementLocation location)
        {
            if (!location.HasValue)
            {
                InitiatorEngagementHexQ = ArmyHexBattleAnchorService.InvalidHexComponent;
                InitiatorEngagementHexR = ArmyHexBattleAnchorService.InvalidHexComponent;
                InitiatorEngagementSiteId = string.Empty;
                return;
            }

            InitiatorEngagementHexQ = location.Hex.Q;
            InitiatorEngagementHexR = location.Hex.R;
            InitiatorEngagementSiteId = location.SiteId ?? string.Empty;
        }

        public void ClearLockedParticipants()
        {
            _playerFormalArmyIds.Clear();
            _enemyFormalArmyIds.Clear();
            _playerPartyMemberIds.Clear();
            PlayerPartyIncluded = false;
            PlayerInclusionReason = BattleParticipantInclusionReason.None;
        }

        public void AddPlayerFormalArmy(string armyId)
        {
            if (string.IsNullOrEmpty(armyId) || _playerFormalArmyIds.Contains(armyId))
                return;
            _playerFormalArmyIds.Add(armyId);
        }

        public void AddEnemyFormalArmy(string armyId)
        {
            if (string.IsNullOrEmpty(armyId) || _enemyFormalArmyIds.Contains(armyId))
                return;
            _enemyFormalArmyIds.Add(armyId);
        }

        public void SetPlayerPartyMembers(
            IReadOnlyList<EntityId> members,
            string inclusionReason,
            string writeSource)
        {
            _playerPartyMemberIds.Clear();
            PlayerPartyIncluded = false;
            PlayerInclusionReason = BattleParticipantInclusionReason.None;
            if (members == null)
                return;

            for (var i = 0; i < members.Count; i++)
            {
                if (members[i].IsNone || _playerPartyMemberIds.Contains(members[i].Value))
                    continue;
                _playerPartyMemberIds.Add(members[i].Value);
                PlayerPartyIncluded = true;
            }

            if (!PlayerPartyIncluded)
                return;

            PlayerInclusionReason = string.IsNullOrEmpty(inclusionReason)
                ? BattleParticipantInclusionReason.SupportAreaPlayer
                : inclusionReason;
            PlayerInclusionTrace.PlayerIncludedReason = PlayerInclusionReason;
            PlayerInclusionTrace.LastPlayerWriteSource = writeSource ?? string.Empty;
        }

        public void SetPlayerPartyMembers(IReadOnlyList<EntityId> members) =>
            SetPlayerPartyMembers(
                members,
                BattleParticipantInclusionReason.SupportAreaPlayer,
                "SetPlayerPartyMembers");

        public bool ContainsFormalArmy(string armyId) =>
            _playerFormalArmyIds.Contains(armyId) || _enemyFormalArmyIds.Contains(armyId);

        public bool ContainsLockedPartyMember(EntityId id) =>
            !id.IsNone && _playerPartyMemberIds.Contains(id.Value);
    }
}
