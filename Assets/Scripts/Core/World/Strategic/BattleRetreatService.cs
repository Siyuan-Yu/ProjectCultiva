using System;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Retreat 只作用于 PlayerDecisionSubject；取消 Pending Engagement。</summary>
    public static class BattleRetreatService
    {
        public static Result ExecuteRetreat(SimulationWorld world, PlayerPartyRuntime party)
        {
            if (world?.Strategic == null)
                return Result.Failure(ErrorCode.InvalidOperation, "No strategic board.");

            var engagement = world.Strategic.PendingEngagement;
            if (engagement == null || !engagement.IsActive)
                return Result.Failure(ErrorCode.InvalidOperation, "No pending engagement.");

            switch (engagement.DecisionSubjectKind)
            {
                case BattleDecisionSubjectKind.FormalArmy:
                    RetreatFormalArmySubject(world, engagement);
                    break;
                case BattleDecisionSubjectKind.PlayerParty:
                    RetreatPlayerPartySubject(world, party, engagement);
                    break;
                default:
                    return Result.Failure(ErrorCode.InvalidOperation, "No player decision subject.");
            }

            CancelEngagementOrders(world, engagement);
            StrategicPursuitService.ClearPursuit(world);
            world.Strategic.PendingEngagement.Clear();
            world.Strategic.ClearBattleOffer();
            StrategicClockFreezeService.EndFreeze(world);
            return Result.Success();
        }

        static void RetreatFormalArmySubject(SimulationWorld world, PendingEngagementRuntime engagement)
        {
            var armyId = engagement.DecisionSubjectFormalArmyId;
            if (string.IsNullOrEmpty(armyId) ||
                !world.Strategic.FormalArmies.TryGet(armyId, out var army) ||
                army == null)
                return;

            ArmyHexPursuitService.CancelPursuitForAttacker(world, armyId);
            engagement.DecisionSubjectRetreatLocation?.ApplyRetreatToFormalArmy(world, army);
        }

        static void RetreatPlayerPartySubject(
            SimulationWorld world,
            PlayerPartyRuntime party,
            PendingEngagementRuntime engagement)
        {
            engagement.DecisionSubjectRetreatLocation?.ApplyRetreatToPlayerParty(world, party);
        }

        static void CancelEngagementOrders(SimulationWorld world, PendingEngagementRuntime engagement)
        {
            if (!string.IsNullOrEmpty(engagement.AttackerFormalArmyId))
                ArmyHexPursuitService.CancelPursuitForAttacker(world, engagement.AttackerFormalArmyId);
        }
    }
}
