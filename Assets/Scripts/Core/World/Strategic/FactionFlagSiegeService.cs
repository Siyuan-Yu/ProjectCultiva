using System;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    public enum FactionFlagAttackStartKind
    {
        BattleOffer,
        DirectFlagAssault
    }

    /// <summary>
    /// 阵营旗攻击编排。SupportArea 永远是 anchor + full one ring（Nominal geometry），
    /// 只把范围内真实可战 FormalArmy 纳入接战；旗本身不是 Character participant。
    /// </summary>
    public static class FactionFlagSiegeService
    {
        public static Result<FactionFlagAttackStartKind> TryBegin(
            SimulationWorld world,
            PlayerPartyRuntime party,
            string flagId)
        {
            if (world?.Strategic == null || party == null || !party.HasActive ||
                !world.Strategic.FactionFlags.Flags.TryGetValue(flagId ?? string.Empty, out var flag) ||
                flag == null)
                return Result.Fail<FactionFlagAttackStartKind>(ErrorCode.NotFound, "阵营旗不存在。");

            var military = StrategicMilitaryRules.ValidatePlayerPartyCanInitiateStrategicMilitaryAction(world, party);
            if (military.IsFailure)
                return Result.Fail<FactionFlagAttackStartKind>(military.Error);
            var attackerFaction = world.Strategic.PlayerFactionId ?? string.Empty;
            if (string.Equals(attackerFaction, flag.FactionId, StringComparison.Ordinal))
                return Result.Fail<FactionFlagAttackStartKind>(ErrorCode.InvalidOperation, "不能攻击己方阵营旗。");
            if (!WarGateService.CanAttack(world, attackerFaction, flag.FactionId))
                return Result.Fail<FactionFlagAttackStartKind>(ErrorCode.InvalidOperation, "攻击阵营旗需要有效战争状态。");

            var supportArea = BattleEngagementSupportArea.FromFrozenLists(
                new[] { flag.AnchorHex }, null, flag.AnchorHex);
            if (!TryFindDefenderStack(
                    world, supportArea, attackerFaction, flag.FactionId, out var defenderStack))
                return Result.Ok(FactionFlagAttackStartKind.DirectFlagAssault);

            if (!BattleOfferService.TryBuildOfferForLocalPlayerPartyObjectiveAttack(
                    world, party, defenderStack, supportArea, "攻击阵营旗"))
                return Result.Fail<FactionFlagAttackStartKind>(
                    ErrorCode.InvalidOperation, "无法建立阵营旗守军接战。");

            world.Strategic.BattleOffer.StrategicObjectiveKind = StrategicObjectiveKind.FactionFlag;
            world.Strategic.BattleOffer.StrategicObjectiveId = flag.FlagId;
            return Result.Ok(FactionFlagAttackStartKind.BattleOffer);
        }

        static bool TryFindDefenderStack(
            SimulationWorld world,
            BattleEngagementSupportArea supportArea,
            string attackerFaction,
            string defenderFaction,
            out ArmyStack stack)
        {
            stack = null;
            foreach (var pair in world.Strategic.FormalArmies.Armies)
            {
                var army = pair.Value;
                if (army == null || !ArmyPostBattleSyncService.HasMacroOrderLivingMember(world, army) ||
                    !IsDefenderSide(world, army.FactionId, attackerFaction, defenderFaction) ||
                    !BattleEngagementSpatialQuery.TryGetCommittedArmyHex(world, army, out var armyHex) ||
                    !supportArea.Contains(armyHex))
                    continue;
                if (StrategicMilitaryRules.ValidateFormalArmyCanParticipate(world, army).IsFailure)
                    continue;
                if (PlayerPartyStrategicCombatCommandService.TryResolveLinkedStack(
                        world, army.ArmyId, out stack) && stack != null)
                    return true;
            }
            return false;
        }

        static bool IsDefenderSide(
            SimulationWorld world,
            string factionId,
            string attackerFaction,
            string defenderFaction)
        {
            foreach (var war in world.Strategic.Wars.EnumerateActive())
            {
                if (war.IsAttacker(attackerFaction) && war.IsDefender(defenderFaction))
                    return war.IsDefender(factionId);
                if (war.IsDefender(attackerFaction) && war.IsAttacker(defenderFaction))
                    return war.IsAttacker(factionId);
            }
            return string.Equals(factionId, defenderFaction, StringComparison.Ordinal);
        }
    }
}
