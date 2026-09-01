using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase 4：Engagement 成立 → Participant Gather → Lock → Decision / Auto 分流。
    /// BattleInitiator 与 PlayerDecisionSubject 均写入 PendingEngagementRuntime。
    /// </summary>
    public static class BattleEngagementAuthorityService
    {
        public static bool TryBeginEngagement(
            SimulationWorld world,
            PlayerPartyRuntime party,
            string attackerArmyId,
            string defenderArmyId,
            ArmyStack primaryEnemyStack,
            IReadOnlyList<EntityId> seedMandatoryAttackers,
            string offerId,
            out bool resolvedWithoutPlayerPrompt)
        {
            resolvedWithoutPlayerPrompt = false;
            if (world?.Strategic == null)
                return false;

            var playerFaction = world.Strategic.PlayerFactionId ?? string.Empty;
            var attackerIsPlayer = IsPlayerFormalArmy(world, attackerArmyId, playerFaction);
            var defenderIsPlayer = IsPlayerFormalArmy(world, defenderArmyId, playerFaction);
            var involvesPlayer = attackerIsPlayer || defenderIsPlayer;

            if (!BattleEngagementTriggerService.CanTriggerEngagement(
                    world,
                    attackerArmyId,
                    defenderArmyId,
                    out var triggerReason))
                return false;

            BattleEngagementSpatialQuery.TryGetCommittedArmyHex(
                world, attackerArmyId, out var initiatorCommittedHex);
            BattleEngagementSpatialQuery.TryGetCommittedArmyHex(
                world, defenderArmyId, out var defenderCommittedHex);

            var supportArea = BattleEngagementSupportArea.ResolveAndFreeze(world, defenderArmyId);
            var primaryEnemyFaction = ResolvePrimaryEnemyFaction(
                world, attackerArmyId, defenderArmyId, primaryEnemyStack, attackerIsPlayer);

            return CommitEngagement(
                world,
                party,
                attackerArmyId,
                defenderArmyId,
                primaryEnemyStack,
                seedMandatoryAttackers,
                offerId,
                triggerReason,
                initiatorCommittedHex,
                defenderCommittedHex,
                supportArea,
                BattleInitiatorKind.FormalArmy,
                attackerArmyId,
                attackerIsPlayer,
                defenderIsPlayer,
                playerFaction,
                primaryEnemyFaction,
                involvesPlayer,
                out resolvedWithoutPlayerPrompt);
        }

        /// <summary>
        /// Phase 5S-B2-3.4：PlayerParty 作为直接 Initiator 成立 PendingEngagement。
        /// 不伪造 FormalArmyId：AttackerFormalArmyId = ""，InitiatorFormalArmyId = ""，
        /// DecisionSubject = PlayerParty，Retreat 落回接战前合法位置（PreEngagementLegalLocation）。
        /// 空间 trigger = CanTriggerPlayerPartyEngagement（PlayerParty committed Hex ∈ Defender SupportArea）。
        /// 后续 Participant Gathering / Snapshot / Offer 全部复用既有主链（TryBeginEngagement 同一套）。
        /// </summary>
        public static bool TryBeginPlayerPartyEngagement(
            SimulationWorld world,
            PlayerPartyRuntime party,
            string defenderArmyId,
            ArmyStack primaryEnemyStack,
            string offerId,
            out bool resolvedWithoutPlayerPrompt)
        {
            resolvedWithoutPlayerPrompt = false;
            if (world?.Strategic == null)
                return false;
            if (party == null || !party.HasActive)
                return false;

            if (!BattleEngagementTriggerService.CanTriggerPlayerPartyEngagement(
                    world,
                    party,
                    defenderArmyId,
                    out _))
                return false;

            if (!BattleEngagementSpatialQuery.TryGetCommittedPartyHex(
                    world, party, out var initiatorCommittedHex))
                return false;
            BattleEngagementSpatialQuery.TryGetCommittedArmyHex(
                world, defenderArmyId, out var defenderCommittedHex);

            var supportArea = BattleEngagementSupportArea.ResolveAndFreeze(world, defenderArmyId);
            var playerFaction = world.Strategic.PlayerFactionId ?? string.Empty;
            var enemyFaction = string.Empty;
            if (world.Strategic.FormalArmies.TryGet(defenderArmyId, out var defender) &&
                defender != null)
                enemyFaction = defender.FactionId ?? string.Empty;
            if (string.IsNullOrEmpty(enemyFaction))
                enemyFaction = primaryEnemyStack?.FactionId ?? string.Empty;

            var committed = CommitEngagement(
                world,
                party,
                string.Empty,
                defenderArmyId,
                primaryEnemyStack,
                party.Members,
                offerId,
                BattleEngagementTriggerService.ReasonAdjacentToBattleArea,
                initiatorCommittedHex,
                defenderCommittedHex,
                supportArea,
                BattleInitiatorKind.PlayerParty,
                string.Empty,
                true,
                false,
                playerFaction,
                enemyFaction,
                true,
                out resolvedWithoutPlayerPrompt);
            if (!committed)
                return false;

            // 直接 combatant invariant：Active Character 必须实际被成功加入。
            // 若 Active 都不在 SupportArea，整个 PlayerParty engagement 不成立。
            var engagement = world.Strategic.PendingEngagement;
            if (!engagement.PlayerPartyIncluded ||
                !engagement.ContainsLockedPartyMember(party.ActiveCharacterId))
            {
                engagement.Clear();
                return false;
            }

            return true;
        }

        /// <summary>
        /// 双方共享的 Engagement 建立核心：frozen SupportArea / BattleLocation / defender hex /
        /// EngagementId / faction / initiator+decision subject / GatherAndLock / snapshot lifecycle。
        /// 两个薄入口（FormalArmy / PlayerParty）只负责各自的 trigger、空间解析与 initiator 字段。
        /// </summary>
        static bool CommitEngagement(
            SimulationWorld world,
            PlayerPartyRuntime party,
            string attackerFormalArmyId,
            string defenderFormalArmyId,
            ArmyStack primaryEnemyStack,
            IReadOnlyList<EntityId> seedMandatoryAttackers,
            string offerId,
            string triggerReason,
            HexCoord initiatorCommittedHex,
            HexCoord defenderCommittedHex,
            BattleEngagementSupportArea supportArea,
            BattleInitiatorKind initiatorKind,
            string initiatorFormalArmyId,
            bool initiatorIsPlayerSide,
            bool defenderIsPlayer,
            string primaryPlayerFactionId,
            string primaryEnemyFactionId,
            bool involvesPlayer,
            out bool resolvedWithoutPlayerPrompt)
        {
            resolvedWithoutPlayerPrompt = false;
            var engagement = world.Strategic.PendingEngagement;
            engagement.Clear();
            engagement.EngagementId = string.IsNullOrEmpty(offerId)
                ? "eng:" + world.Tick.Value
                : offerId;
            engagement.SetSupportArea(supportArea);
            if (!supportArea.HasValue)
            {
                engagement.SetBattleLocation(BattleEngagementHexDistance.ResolveBattleLocationHex(
                    world, attackerFormalArmyId, defenderFormalArmyId));
            }

            engagement.SetTriggerSpatialSnapshot(
                triggerReason,
                initiatorCommittedHex,
                defenderCommittedHex);
            engagement.AttackerFormalArmyId = attackerFormalArmyId ?? string.Empty;
            engagement.DefenderFormalArmyId = defenderFormalArmyId ?? string.Empty;
            engagement.PrimaryPlayerFactionId = primaryPlayerFactionId ?? string.Empty;
            engagement.PrimaryEnemyFactionId = primaryEnemyFactionId ?? string.Empty;
            engagement.InvolvesPlayerSide = involvesPlayer;
            engagement.RequiresPlayerDecision = involvesPlayer;

            engagement.InitiatorKind = initiatorKind;
            engagement.InitiatorFormalArmyId = initiatorFormalArmyId ?? string.Empty;
            engagement.InitiatorIsPlayerSide = initiatorIsPlayerSide;
            engagement.SetInitiatorEngagementLocation(
                initiatorKind == BattleInitiatorKind.FormalArmy
                    ? BattleEngagementHexDistance.ResolveInitiatorEngagementLocation(
                        world, engagement.InitiatorFormalArmyId)
                    : BattleEngagementHexDistance.ResolvePlayerPartyInitiatorEngagementLocation(
                        world, party));

            ResolveDecisionSubject(
                engagement,
                initiatorKind,
                initiatorIsPlayerSide,
                defenderIsPlayer,
                attackerFormalArmyId,
                defenderFormalArmyId);
            engagement.DecisionSubjectRetreatLocation = CaptureDecisionSubjectRetreatLocation(
                world, party, engagement);

            BattleParticipantGatheringService.GatherAndLock(world, engagement, party, seedMandatoryAttackers);

            if (!involvesPlayer)
            {
                BuildSnapshotFromEngagement(
                    world, engagement, primaryEnemyStack, seedMandatoryAttackers, offerId);
                resolvedWithoutPlayerPrompt = TryResolveThirdPartyAuto(world);
                return true;
            }

            return true;
        }

        public static void BuildSnapshotFromEngagement(
            SimulationWorld world,
            PendingEngagementRuntime engagement,
            ArmyStack primaryEnemyStack,
            IReadOnlyList<EntityId> seedMandatoryAttackers,
            string offerId)
        {
            var snap = world.Strategic.Participants;
            snap.OfferId = offerId ?? string.Empty;
            snap.AttackerArmyId = engagement.AttackerFormalArmyId;
            snap.DefenderArmyId = engagement.DefenderFormalArmyId;
            snap.PrimaryEnemyStackId = primaryEnemyStack?.Id ?? string.Empty;

            BattleParticipantGatheringService.ApplyLockedParticipantsToSnapshot(
                world,
                engagement,
                snap,
                primaryEnemyStack,
                seedMandatoryAttackers);

            BattleOfferService.PromoteInRangeIncapacitatedToMandatory(world, snap);
        }

        public static bool TryResolveThirdPartyAuto(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return false;

            var engagement = world.Strategic.PendingEngagement;
            if (engagement == null || engagement.InvolvesPlayerSide)
                return false;

            var offer = world.Strategic.BattleOffer;
            offer.Resolved = false;
            offer.OfferId = engagement.EngagementId;
            offer.ArmyStackId = world.Strategic.Participants.PrimaryEnemyStackId;
            offer.AttackerArmyId = engagement.AttackerFormalArmyId;
            offer.DefenderArmyId = engagement.DefenderFormalArmyId;
            offer.Title = "第三方接战";
            BattleOfferService.RefreshOfferPowerLabels(world);

            var resolved = BattleOfferService.ResolveAuto(world, executeOnWin: false, out _, out _);
            if (resolved.IsSuccess)
            {
                world.Strategic.Participants.IsAutoSettlement = false;
                world.Strategic.PendingEngagement.Clear();
                BattleOfferService.FinishOfferResolution(world);
            }

            return resolved.IsSuccess;
        }

        static void ResolveDecisionSubject(
            PendingEngagementRuntime engagement,
            BattleInitiatorKind initiatorKind,
            bool attackerIsPlayer,
            bool defenderIsPlayer,
            string attackerArmyId,
            string defenderArmyId)
        {
            if (initiatorKind == BattleInitiatorKind.PlayerParty)
            {
                // PlayerParty Initiator：DecisionSubject = PlayerParty，不伪造 FormalArmyId。
                engagement.DecisionSubjectKind = BattleDecisionSubjectKind.PlayerParty;
                engagement.DecisionSubjectFormalArmyId = string.Empty;
                return;
            }

            if (attackerIsPlayer)
            {
                engagement.DecisionSubjectKind = BattleDecisionSubjectKind.FormalArmy;
                engagement.DecisionSubjectFormalArmyId = attackerArmyId ?? string.Empty;
                return;
            }

            if (defenderIsPlayer)
            {
                engagement.DecisionSubjectKind = BattleDecisionSubjectKind.FormalArmy;
                engagement.DecisionSubjectFormalArmyId = defenderArmyId ?? string.Empty;
                return;
            }

            engagement.DecisionSubjectKind = BattleDecisionSubjectKind.None;
            engagement.DecisionSubjectFormalArmyId = string.Empty;
        }

        static PreEngagementLegalLocation CaptureDecisionSubjectRetreatLocation(
            SimulationWorld world,
            PlayerPartyRuntime party,
            PendingEngagementRuntime engagement)
        {
            switch (engagement.DecisionSubjectKind)
            {
                case BattleDecisionSubjectKind.FormalArmy:
                    if (world.Strategic.FormalArmies.TryGet(
                            engagement.DecisionSubjectFormalArmyId, out var army) &&
                        army != null)
                        return PreEngagementLegalLocation.CaptureFormalArmy(world, army);
                    break;
                case BattleDecisionSubjectKind.PlayerParty:
                    return PreEngagementLegalLocation.CapturePlayerParty(world, party);
            }

            return null;
        }

        static string ResolvePrimaryEnemyFaction(
            SimulationWorld world,
            string attackerArmyId,
            string defenderArmyId,
            ArmyStack primaryEnemyStack,
            bool attackerIsPlayer)
        {
            if (attackerIsPlayer)
            {
                if (!string.IsNullOrEmpty(defenderArmyId) &&
                    world.Strategic.FormalArmies.TryGet(defenderArmyId, out var defender) &&
                    defender != null)
                    return defender.FactionId ?? string.Empty;
                return primaryEnemyStack?.FactionId ?? string.Empty;
            }

            if (!string.IsNullOrEmpty(attackerArmyId) &&
                world.Strategic.FormalArmies.TryGet(attackerArmyId, out var attacker) &&
                attacker != null)
                return attacker.FactionId ?? string.Empty;

            return primaryEnemyStack?.FactionId ?? string.Empty;
        }

        static bool IsPlayerFormalArmy(SimulationWorld world, string armyId, string playerFaction)
        {
            if (string.IsNullOrEmpty(armyId) || string.IsNullOrEmpty(playerFaction))
                return false;
            if (!world.Strategic.FormalArmies.TryGet(armyId, out var army) || army == null)
                return false;
            return string.Equals(army.FactionId, playerFaction, StringComparison.Ordinal);
        }
    }
}
