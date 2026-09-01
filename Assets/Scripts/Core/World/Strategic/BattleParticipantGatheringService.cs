using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 以冻结的 SupportAreaHexes 为唯一空间 Authority、单次扫描锁定 Participants。
    /// Initiator 与 Defender 无条件加入；禁止援军连锁。
    /// </summary>
    public static class BattleParticipantGatheringService
    {
        public static void GatherAndLock(
            SimulationWorld world,
            PendingEngagementRuntime engagement,
            PlayerPartyRuntime party,
            IReadOnlyList<EntityId> partyRosterFallback = null)
        {
            if (world?.Strategic == null || engagement == null || !engagement.HasSupportArea)
                return;

            engagement.PlayerInclusionTrace.PlayerIncludedBeforeGathering = engagement.PlayerPartyIncluded;
            engagement.ClearLockedParticipants();

            var supportArea = engagement.SupportArea;
            var initiatorId = engagement.InitiatorFormalArmyId ?? string.Empty;
            var defenderId = engagement.DefenderFormalArmyId ?? string.Empty;
            var playerFaction = engagement.PrimaryPlayerFactionId ?? string.Empty;
            var enemyFaction = engagement.PrimaryEnemyFactionId ?? string.Empty;

            TryAddMandatoryArmy(
                world, engagement, initiatorId, playerFaction, enemyFaction,
                BattleParticipantInclusionReason.DirectInitiator);
            TryAddMandatoryArmy(
                world, engagement, defenderId, playerFaction, enemyFaction,
                BattleParticipantInclusionReason.DirectDefender);

            foreach (var kv in world.Strategic.FormalArmies.Armies)
            {
                var army = kv.Value;
                if (army == null || string.IsNullOrEmpty(army.ArmyId))
                    continue;
                if (IsDirectCombatant(army.ArmyId, initiatorId, defenderId))
                    continue;
                if (!ArmyPostBattleSyncService.HasMacroOrderLivingMember(world, army))
                    continue;
                if (!IsCombatSideFaction(army.FactionId, playerFaction, enemyFaction))
                    continue;
                if (IsArmyLockedInAnotherBattle(world, army.ArmyId, engagement.EngagementId))
                    continue;

                if (!BattleEngagementSpatialQuery.TryGetCommittedArmyHex(
                        world, army, out var armyHex) ||
                    !supportArea.Contains(armyHex))
                    continue;

                AddArmyByFaction(
                    engagement, army, playerFaction, enemyFaction,
                    BattleParticipantInclusionReason.SupportAreaArmy);
            }

            if (engagement.InitiatorKind == BattleInitiatorKind.PlayerParty)
                TryGatherDirectPlayerPartyInitiator(
                    world, engagement, party, partyRosterFallback, supportArea);
            else
                TryGatherPlayerParty(world, engagement, party, partyRosterFallback, supportArea);
            engagement.PlayerInclusionTrace.PlayerIncludedAfterGathering = engagement.PlayerPartyIncluded;
            BattleParticipantSpatialGuard.ValidateAfterGathering(world, engagement, party);
        }

        /// <summary>
        /// Phase 5S-B2-3.4：PlayerParty 是 DirectInitiator（不是普通 SupportAreaPlayer）。
        /// 成员资格与 Support 路径一致（living、非 FormalArmy、committed Hex ∈ frozen SupportArea），
        /// 但 IncludedReason = DirectInitiator。Active Character 必须在 SupportArea 内；
        /// 若 Active 都不在 SupportArea，由调用方（TryBeginPlayerPartyEngagement）拒绝整个 engagement。
        /// </summary>
        static void TryGatherDirectPlayerPartyInitiator(
            SimulationWorld world,
            PendingEngagementRuntime engagement,
            PlayerPartyRuntime party,
            IReadOnlyList<EntityId> partyRosterFallback,
            BattleEngagementSupportArea supportArea)
        {
            if (party != null && party.Members != null && party.Members.Count > 0)
            {
                TryIncludeEligiblePartyMembers(
                    world,
                    engagement,
                    party.Members,
                    party,
                    supportArea,
                    BattleParticipantInclusionReason.DirectInitiator,
                    "GatherAndLock.DirectPlayerPartyInitiator");
                return;
            }

            if (partyRosterFallback == null || partyRosterFallback.Count == 0)
                return;

            TryIncludeEligiblePartyMembers(
                world,
                engagement,
                partyRosterFallback,
                world.Strategic.PlayerPartyContext,
                supportArea,
                BattleParticipantInclusionReason.DirectInitiator,
                "GatherAndLock.DirectPlayerPartyInitiator");
        }

        static void TryGatherPlayerParty(
            SimulationWorld world,
            PendingEngagementRuntime engagement,
            PlayerPartyRuntime party,
            IReadOnlyList<EntityId> partyRosterFallback,
            BattleEngagementSupportArea supportArea)
        {
            if (party != null && party.Members != null && party.Members.Count > 0)
            {
                TryIncludeEligiblePartyMembers(
                    world,
                    engagement,
                    party.Members,
                    party,
                    supportArea,
                    BattleParticipantInclusionReason.SupportAreaPlayer,
                    "GatherAndLock.PlayerParty");
                return;
            }

            if (partyRosterFallback == null || partyRosterFallback.Count == 0)
                return;

            TryIncludeEligiblePartyMembers(
                world,
                engagement,
                partyRosterFallback,
                world.Strategic.PlayerPartyContext,
                supportArea,
                BattleParticipantInclusionReason.SupportAreaPlayer,
                "GatherAndLock.PartyRosterFallback");
        }

        static void TryIncludeEligiblePartyMembers(
            SimulationWorld world,
            PendingEngagementRuntime engagement,
            IReadOnlyList<EntityId> candidates,
            PlayerPartyRuntime partyContext,
            BattleEngagementSupportArea supportArea,
            string inclusionReason,
            string writeSource)
        {
            var members = new List<EntityId>(candidates.Count);
            for (var i = 0; i < candidates.Count; i++)
            {
                var id = candidates[i];
                if (id.IsNone ||
                    ArmyService.TryGetArmyForCharacter(world, id, out _) ||
                    !LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, id))
                    continue;

                if (!TryGetPartyMemberBattleHex(world, partyContext, id, out var memberHex) ||
                    !supportArea.Contains(memberHex))
                    continue;

                members.Add(id);
            }

            if (members.Count > 0)
            {
                engagement.SetPlayerPartyMembers(
                    members,
                    inclusionReason,
                    writeSource);
            }
        }

        static bool TryGetPartyMemberBattleHex(
            SimulationWorld world,
            PlayerPartyRuntime party,
            EntityId memberId,
            out HexCoord hex)
        {
            hex = default;
            if (party != null &&
                party.HasActive &&
                party.ActiveCharacterId == memberId &&
                !ArmyService.TryGetArmyForCharacter(world, memberId, out _))
                return BattleEngagementSpatialQuery.TryGetCommittedPartyHex(world, party, out hex);

            return BattleEngagementSpatialQuery.TryGetCommittedCharacterHex(world, memberId, out hex);
        }

        static bool IsDirectCombatant(string armyId, string initiatorId, string defenderId) =>
            string.Equals(armyId, initiatorId, StringComparison.Ordinal) ||
            string.Equals(armyId, defenderId, StringComparison.Ordinal);

        static bool IsCombatSideFaction(string factionId, string playerFaction, string enemyFaction) =>
            string.Equals(factionId, playerFaction, StringComparison.Ordinal) ||
            string.Equals(factionId, enemyFaction, StringComparison.Ordinal);

        static bool IsArmyLockedInAnotherBattle(
            SimulationWorld world,
            string armyId,
            string currentEngagementId)
        {
            if (string.IsNullOrEmpty(armyId) || world?.Strategic == null)
                return false;

            var pending = world.Strategic.PendingEngagement;
            if (pending != null &&
                pending.IsActive &&
                !string.Equals(pending.EngagementId, currentEngagementId, StringComparison.Ordinal) &&
                pending.ContainsFormalArmy(armyId))
                return true;

            if (!world.Strategic.IsModalEncounter || world.Strategic.Participants == null)
                return false;

            var records = world.Strategic.Participants.Records;
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                if (record != null &&
                    string.Equals(record.FormalArmyId, armyId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        static void TryAddMandatoryArmy(
            SimulationWorld world,
            PendingEngagementRuntime engagement,
            string armyId,
            string playerFaction,
            string enemyFaction,
            string inclusionReason)
        {
            if (string.IsNullOrEmpty(armyId) ||
                !world.Strategic.FormalArmies.TryGet(armyId, out var army) ||
                army == null)
                return;

            if (!ArmyPostBattleSyncService.HasMacroOrderLivingMember(world, army))
                return;

            AddArmyByFaction(engagement, army, playerFaction, enemyFaction, inclusionReason);
        }

        static void AddArmyByFaction(
            PendingEngagementRuntime engagement,
            FormalArmy army,
            string playerFaction,
            string enemyFaction,
            string inclusionReason)
        {
            if (string.Equals(army.FactionId, playerFaction, StringComparison.Ordinal))
                engagement.AddPlayerFormalArmy(army.ArmyId);
            else if (string.Equals(army.FactionId, enemyFaction, StringComparison.Ordinal))
                engagement.AddEnemyFormalArmy(army.ArmyId);
        }

        public static void ApplyLockedParticipantsToSnapshot(
            SimulationWorld world,
            PendingEngagementRuntime engagement,
            BattleParticipantSnapshot snap,
            ArmyStack primaryEnemyStack,
            IReadOnlyList<EntityId> seedMandatoryAttackers)
        {
            if (world == null || engagement == null || snap == null)
                return;

            snap.Clear();

            AddFormalArmiesAsMandatory(
                world, snap, engagement, engagement.LockedPlayerFormalArmyIds);
            AddPlayerPartyMandatory(world, snap, engagement);

            if (primaryEnemyStack != null)
            {
                AddPrimaryEnemy(world, snap, primaryEnemyStack);
                AddFormalArmiesAsEnemy(
                    world, snap, engagement.LockedEnemyFormalArmyIds, primaryEnemyStack);
            }

            ArmyHexBattleAnchorService.SetBattleAnchorHex(snap, engagement.BattleLocation);
            BattleParticipantSpatialGuard.ValidateAfterSnapshot(
                world,
                engagement,
                world.Strategic.PlayerPartyContext,
                snap);
        }

        static void AddFormalArmiesAsMandatory(
            SimulationWorld world,
            BattleParticipantSnapshot snap,
            PendingEngagementRuntime engagement,
            IReadOnlyList<string> armyIds)
        {
            if (armyIds == null)
                return;

            var initiatorId = engagement.InitiatorFormalArmyId ?? string.Empty;
            var defenderId = engagement.DefenderFormalArmyId ?? string.Empty;

            for (var a = 0; a < armyIds.Count; a++)
            {
                if (!world.Strategic.FormalArmies.TryGet(armyIds[a], out var army) || army == null)
                    continue;

                var reason = string.Equals(army.ArmyId, initiatorId, StringComparison.Ordinal)
                    ? BattleParticipantInclusionReason.DirectInitiator
                    : string.Equals(army.ArmyId, defenderId, StringComparison.Ordinal)
                        ? BattleParticipantInclusionReason.DirectDefender
                        : BattleParticipantInclusionReason.SupportAreaArmy;

                for (var i = 0; i < army.MemberCharacterIds.Count; i++)
                {
                    var id = new EntityId(army.MemberCharacterIds[i]);
                    if (id.IsNone || snap.FindByEntity(id) != null)
                        continue;
                    if (!LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, id))
                        continue;
                    if (!world.Entities.TryGet(id, out var ent) || ent == null)
                        continue;
                    if ((ent.Tags & EntityTag.Npc) != 0)
                        continue;
                    if (!world.WorldPresence.TryGet(id, out var wp) || wp == null)
                        continue;

                    snap.Add(new BattleParticipantRecord
                    {
                        Kind = BattleParticipantKind.MandatoryFriendly,
                        EntityId = id,
                        FormalArmyId = army.ArmyId,
                        DisplayLabel = string.IsNullOrEmpty(ent.DisplayName) ? id.ToString() : ent.DisplayName,
                        CombatPower = CombatPowerCalculator.ForEntity(world, id),
                        Selected = true,
                        PreBattle = PreBattleWorldPresence.Capture(wp),
                        IncludedReason = reason
                    });
                }
            }
        }

        static void AddPlayerPartyMandatory(
            SimulationWorld world,
            BattleParticipantSnapshot snap,
            PendingEngagementRuntime engagement)
        {
            if (!engagement.PlayerPartyIncluded || !engagement.HasSupportArea)
                return;

            var party = world.Strategic.PlayerPartyContext;
            for (var i = 0; i < engagement.LockedPlayerPartyMemberIds.Count; i++)
            {
                var id = new EntityId(engagement.LockedPlayerPartyMemberIds[i]);
                if (id.IsNone || snap.FindByEntity(id) != null)
                    continue;
                if (!world.Entities.TryGet(id, out var ent) || ent == null)
                    continue;
                if ((ent.Tags & EntityTag.Npc) != 0)
                    continue;
                if (!world.WorldPresence.TryGet(id, out var wp) || wp == null)
                    continue;
                if (ArmyService.TryGetArmyForCharacter(world, id, out _))
                    continue;

                if (!TryGetPartyMemberBattleHex(world, party, id, out var memberHex) ||
                    !engagement.SupportArea.Contains(memberHex))
                    continue;

                snap.Add(new BattleParticipantRecord
                {
                    Kind = BattleParticipantKind.MandatoryFriendly,
                    EntityId = id,
                    FormalArmyId = string.Empty,
                    DisplayLabel = string.IsNullOrEmpty(ent.DisplayName) ? id.ToString() : ent.DisplayName,
                    CombatPower = CombatPowerCalculator.ForEntity(world, id),
                    Selected = true,
                    PreBattle = PreBattleWorldPresence.Capture(wp),
                    IncludedReason = engagement.PlayerInclusionReason
                });
            }
        }

        static void AddPrimaryEnemy(
            SimulationWorld world,
            BattleParticipantSnapshot snap,
            ArmyStack primaryEnemy)
        {
            if (ArmyStackAdapter.TryGetFormalArmy(world, primaryEnemy, out var formalArmy) &&
                formalArmy != null)
            {
                for (var i = 0; i < formalArmy.MemberCharacterIds.Count; i++)
                {
                    var id = new EntityId(formalArmy.MemberCharacterIds[i]);
                    if (id.IsNone || !world.Entities.TryGet(id, out var ent) || ent == null)
                        continue;
                    // living FormalArmy 战斗：纳入 IsLivingForMacroOrder 成员；
                    // 不因 linked stack 的历史 casualty（HasDownedRemnant）把 living member 排除。
                    // 弥留／尸体仅在 legacy residual reentry 兼容时纳入。
                    if (primaryEnemy.HasDownedRemnant &&
                        !LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, id) &&
                        !LingeringBattlefieldPartyService.IsLingeringDowned(world, id))
                        continue;
                    snap.Add(new BattleParticipantRecord
                    {
                        Kind = BattleParticipantKind.EnemyPrimary,
                        EntityId = id,
                        ArmyStackId = primaryEnemy.Id,
                        FormalArmyId = formalArmy.ArmyId,
                        DisplayLabel = string.IsNullOrEmpty(ent.DisplayName) ? id.ToString() : ent.DisplayName,
                        CombatPower = CombatPowerCalculator.ForEntity(world, id),
                        Selected = true,
                        IncludedReason = BattleParticipantInclusionReason.DirectDefender
                    });
                }
            }
            else
            {
                snap.Add(new BattleParticipantRecord
                {
                    Kind = BattleParticipantKind.EnemyPrimary,
                    ArmyStackId = primaryEnemy.Id,
                    DisplayLabel = string.IsNullOrEmpty(primaryEnemy.DisplayName)
                        ? primaryEnemy.Id
                        : primaryEnemy.DisplayName,
                    CombatPower = CombatPowerCalculator.ForArmyStack(world, primaryEnemy),
                    Selected = true,
                    IncludedReason = BattleParticipantInclusionReason.DirectDefender
                });
            }
        }

        static void AddFormalArmiesAsEnemy(
            SimulationWorld world,
            BattleParticipantSnapshot snap,
            IReadOnlyList<string> armyIds,
            ArmyStack primaryEnemy)
        {
            if (armyIds == null)
                return;

            for (var a = 0; a < armyIds.Count; a++)
            {
                var armyId = armyIds[a];
                if (string.IsNullOrEmpty(armyId))
                    continue;

                ArmyStack linkedStack = null;
                if (ArmyStackAdapter.TryGetFormalArmy(world, primaryEnemy, out var primaryFormal) &&
                    primaryFormal != null &&
                    string.Equals(primaryFormal.ArmyId, armyId, StringComparison.Ordinal))
                    continue;

                if (!world.Strategic.FormalArmies.TryGet(armyId, out var army) || army == null)
                    continue;

                TryResolveLinkedStack(world, armyId, out linkedStack);
                var stackId = linkedStack?.Id ?? string.Empty;

                for (var i = 0; i < army.MemberCharacterIds.Count; i++)
                {
                    var id = new EntityId(army.MemberCharacterIds[i]);
                    if (id.IsNone || snap.FindByEntity(id) != null)
                        continue;
                    if (!world.Entities.TryGet(id, out var ent) || ent == null)
                        continue;

                    snap.Add(new BattleParticipantRecord
                    {
                        Kind = BattleParticipantKind.EnemyReinforcement,
                        EntityId = id,
                        ArmyStackId = stackId,
                        FormalArmyId = army.ArmyId,
                        DisplayLabel = string.IsNullOrEmpty(ent.DisplayName) ? id.ToString() : ent.DisplayName,
                        CombatPower = CombatPowerCalculator.ForEntity(world, id),
                        Selected = true,
                        IncludedReason = BattleParticipantInclusionReason.SupportAreaArmy
                    });
                }
            }
        }

        static bool TryResolveLinkedStack(SimulationWorld world, string armyId, out ArmyStack stack)
        {
            stack = null;
            if (world?.Strategic?.Armies == null)
                return false;
            foreach (var kv in world.Strategic.Armies.Stacks)
            {
                var candidate = kv.Value;
                if (candidate == null)
                    continue;
                if (string.Equals(candidate.FormalArmyId, armyId, StringComparison.Ordinal))
                {
                    stack = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
