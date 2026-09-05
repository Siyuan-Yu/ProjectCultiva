using System;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    public enum WorldSiteSiegeStartKind { BattleOffer, DirectControlCoreAssault }

    /// <summary>
    /// 议政厅攻城的窄领域编排：ControlCore 是战略目标，只有真实可战守军才建立 BattleOffer。
    /// 不保存“战后继续拆楼”状态；每次点击都重新冻结 SupportArea 并判断守军。
    /// </summary>
    public static class WorldSiteSiegeService
    {
        public static Result<WorldSiteSiegeStartKind> TryBegin(
            SimulationWorld world,
            PlayerPartyRuntime party,
            string controlCoreWorkAreaId)
        {
            if (world?.Strategic == null || party == null || !party.HasActive ||
                string.IsNullOrEmpty(controlCoreWorkAreaId) ||
                !world.ControlCores.TryGet(controlCoreWorkAreaId, out var core))
                return Result.Fail<WorldSiteSiegeStartKind>(ErrorCode.InvalidArgument, "议政厅攻城参数无效。");
            if (!CaptureObjectiveService.TryResolveControlCoreSite(world, core, out var siteId) ||
                !world.Strategic.Sites.TryGet(siteId, out var site) || site == null)
                return Result.Fail<WorldSiteSiegeStartKind>(ErrorCode.NotFound, "无法解析议政厅所属 WorldSite。");

            var attackerFaction = world.Strategic.PlayerFactionId ?? string.Empty;
            var defenderFaction = site.OwnerFactionId ?? string.Empty;
            var gate = CaptureObjectiveService.TryBeginMilitaryAssault(world, attackerFaction, controlCoreWorkAreaId);
            if (gate.IsFailure)
                return Result.Fail<WorldSiteSiegeStartKind>(gate.Error);

            var supportArea = BattleEngagementSupportArea.ResolveAndFreezeForWorldSite(world, siteId);
            if (!TryFindDefenderStack(world, supportArea, attackerFaction, defenderFaction, out var defenderStack))
                return Result.Ok(WorldSiteSiegeStartKind.DirectControlCoreAssault);

            var built = PlayerPartyStrategicCombatCommandService
                .TryPrepareLocalPlayerPartyMilitaryAttackOffer(world, party, defenderStack.FormalArmyId);
            if (built.IsFailure)
                return Result.Fail<WorldSiteSiegeStartKind>(built.Error);

            world.Strategic.BattleOffer.StrategicObjectiveKind = "ControlCore";
            world.Strategic.BattleOffer.StrategicObjectiveId = controlCoreWorkAreaId;
            return Result.Ok(WorldSiteSiegeStartKind.BattleOffer);
        }

        static bool TryFindDefenderStack(
            SimulationWorld world,
            BattleEngagementSupportArea supportArea,
            string attackerFaction,
            string defenderFaction,
            out ArmyStack stack)
        {
            stack = null;
            if (supportArea == null || !supportArea.HasValue)
                return false;
            foreach (var pair in world.Strategic.FormalArmies.Armies)
            {
                var army = pair.Value;
                if (army == null || !ArmyPostBattleSyncService.HasMacroOrderLivingMember(world, army) ||
                    !IsDefenderSide(world, army.FactionId, attackerFaction, defenderFaction) ||
                    !BattleEngagementSpatialQuery.TryGetCommittedArmyHex(world, army, out var armyHex) ||
                    !supportArea.Contains(armyHex))
                    continue;
                foreach (var stackPair in world.Strategic.Armies.Stacks)
                {
                    var candidate = stackPair.Value;
                    if (candidate != null && string.Equals(candidate.FormalArmyId, army.ArmyId, StringComparison.Ordinal))
                    {
                        stack = candidate;
                        return true;
                    }
                }
            }
            return false;
        }

        static bool IsDefenderSide(SimulationWorld world, string factionId, string attackerFaction, string defenderFaction)
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
