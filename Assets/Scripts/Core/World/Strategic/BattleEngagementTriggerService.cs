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

            return CanTriggerFromCommittedHex(world, initiatorHex, defenderFormalArmyId, out triggerReason);
        }

        /// <summary>
        /// Phase 5S-B2-3.4：PlayerParty Initiator 入口（BattleInitiatorKind.PlayerParty）。
        /// 空间资格与 FormalArmy 完全一致：PlayerParty committed Hex（PlayerPartyWorldMotion 权威）
        /// 必须落入 Defender SupportAreaHexes。不复制 SupportArea 判断，直接走共享 helper。
        /// </summary>
        public static bool CanTriggerPlayerPartyEngagement(
            SimulationWorld world,
            PlayerPartyRuntime party,
            string defenderFormalArmyId,
            out string triggerReason)
        {
            triggerReason = string.Empty;
            if (world?.Strategic == null)
                return false;
            if (party == null || !party.HasActive)
            {
                triggerReason = ReasonMissingInitiatorHex;
                return false;
            }

            if (!BattleEngagementSpatialQuery.TryGetCommittedPartyHex(world, party, out var playerHex))
            {
                triggerReason = ReasonMissingInitiatorHex;
                return false;
            }

            return CanTriggerFromCommittedHex(world, playerHex, defenderFormalArmyId, out triggerReason);
        }

        /// <summary>
        /// 共享内部逻辑：Initiator 已提交 Hex 落入 Defender frozen/current SupportAreaHexes。
        /// 禁止 WorldPosition float 距离 / ReinforcementWorldRadius / PresenceHex fallback。
        /// </summary>
        public static bool CanTriggerFromCommittedHex(
            SimulationWorld world,
            HexCoord initiatorHex,
            string defenderFormalArmyId,
            out string triggerReason)
        {
            triggerReason = string.Empty;
            if (world?.Strategic == null)
                return false;

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
