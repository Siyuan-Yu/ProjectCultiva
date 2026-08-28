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
            var engagement = world.Strategic.PendingEngagement;
            engagement.Clear();
            engagement.EngagementId = string.IsNullOrEmpty(offerId)
                ? "eng:" + world.Tick.Value
                : offerId;
            engagement.SetSupportArea(supportArea);
            if (!supportArea.HasValue)
            {
                engagement.SetBattleLocation(BattleEngagementHexDistance.ResolveBattleLocationHex(
                    world, attackerArmyId, defenderArmyId));
            }

            engagement.SetTriggerSpatialSnapshot(
                triggerReason,
                initiatorCommittedHex,
                defenderCommittedHex);
            engagement.AttackerFormalArmyId = attackerArmyId ?? string.Empty;
            engagement.DefenderFormalArmyId = defenderArmyId ?? string.Empty;
            engagement.PrimaryPlayerFactionId = playerFaction;
            engagement.PrimaryEnemyFactionId = ResolvePrimaryEnemyFaction(
                world, attackerArmyId, defenderArmyId, primaryEnemyStack, attackerIsPlayer);
            engagement.InvolvesPlayerSide = involvesPlayer;
            engagement.RequiresPlayerDecision = involvesPlayer;

            engagement.InitiatorKind = BattleInitiatorKind.FormalArmy;
            engagement.InitiatorFormalArmyId = attackerArmyId ?? string.Empty;
            engagement.InitiatorIsPlayerSide = attackerIsPlayer;
            engagement.SetInitiatorEngagementLocation(
                BattleEngagementHexDistance.ResolveInitiatorEngagementLocation(
                    world, engagement.InitiatorFormalArmyId));

            ResolveDecisionSubject(engagement, attackerIsPlayer, defenderIsPlayer, attackerArmyId, defenderArmyId);
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
            bool attackerIsPlayer,
            bool defenderIsPlayer,
            string attackerArmyId,
            string defenderArmyId)
        {
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
