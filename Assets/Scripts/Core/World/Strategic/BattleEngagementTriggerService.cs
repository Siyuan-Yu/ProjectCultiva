using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase 4 Battle Trigger（A）：Initiator 已提交 Hex 必须落入 Defender BattleArea 的 SupportAreaHexes。
    /// 禁止 ContinuousWorldPosition / 派生 CurrentHex 提前触发。
    /// </summary>
    public static class BattleEngagementTriggerService
    {
        public const string ReasonInitiatorNotAdjacentToBattleArea = "InitiatorNotAdjacentToBattleArea";
        public const string ReasonMissingInitiatorHex = "MissingInitiatorHex";
        public const string ReasonMissingDefenderBattleArea = "MissingDefenderBattleArea";
        public const string ReasonAdjacentToBattleArea = "AdjacentToBattleArea";

        public static bool CanTriggerEngagement(
            SimulationWorld world,
            string initiatorFormalArmyId,
            string defenderFormalArmyId,
            out string triggerReason)
        {
            triggerReason = string.Empty;
            if (world?.Strategic == null)
                return false;

            if (!BattleEngagementSpatialQuery.TryGetCommittedArmyHex(
                    world, initiatorFormalArmyId, out var initiatorHex))
            {
                triggerReason = ReasonMissingInitiatorHex;
                return false;
            }

            var supportArea = BattleEngagementSupportArea.ResolveAndFreeze(world, defenderFormalArmyId);
            if (!supportArea.HasValue)
            {
                triggerReason = ReasonMissingDefenderBattleArea;
                return false;
            }

            if (!supportArea.Contains(initiatorHex))
            {
                triggerReason = ReasonInitiatorNotAdjacentToBattleArea;
                return false;
            }

            triggerReason = ReasonAdjacentToBattleArea;
            return true;
        }

        public static bool IsActuallyAdjacentToBattleArea(
            SimulationWorld world,
            string initiatorFormalArmyId,
            string defenderFormalArmyId) =>
            CanTriggerEngagement(world, initiatorFormalArmyId, defenderFormalArmyId, out _);

        public static bool TryDetectEngagementContact(
            SimulationWorld world,
            FormalArmy initiator,
            FormalArmy defender)
        {
            if (initiator == null || defender == null)
                return false;

            return CanTriggerEngagement(
                world,
                initiator.ArmyId,
                defender.ArmyId,
                out _);
        }
    }
}
