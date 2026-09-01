using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase 5S-B2-3.5：PlayerParty 追击 Enemy FormalArmy 的薄 Hex pursuit adapter。
    /// 不是第二套 Battle 系统 —— 它只是 PlayerPartyWorldMotion ↔ target FormalArmy 的
    /// movement adapter；Battle trigger / Offer / participant gathering / Manual / Auto
    /// 全部继续共享既有 WORLD_COMBAT 主链（PlayerParty Strategic Combat Command V1）。
    ///
    /// 真源模型（与 FormalArmy ArmyHexPursuitService 一致）：
    ///  - targetArmyId 是 pursuit intent authority（PlayerPartyWorldMotion.AttackOrderTargetArmyId，
    ///    仅 strategic order metadata，不是第二份 position authority）；
    ///  - 目标当前 committed Hex 每 tick 从 FormalArmy.WorldMotion 解析，不记忆点击时的旧 Hex；
    ///  - 进入 Defender SupportArea（CanTriggerPlayerPartyEngagement）即接战，不要求走到 target
    ///    exact Hex —— 保持既有 BattleArea / reinforcement radius 模型。
    ///
    /// 生命周期：BeginAttackArmy 设置 target；普通 Move / Gateway 旅行 / 条件失效 / 接战成功
    /// 由 CancelPursuit 清除。CompleteMove 不清 target（contact 流程 CancelTravel 不误清）。
    /// Save→Load 后 Movement 恢复 Idle、pursuit target 清空（与普通 PlayerParty travel 同契约，
    /// 不单独引入更强的 persistence）。
    /// </summary>
    public static class PlayerPartyHexPursuitService
    {
        public static bool HasPursuit(SimulationWorld world) =>
            world?.PlayerPartyTravel != null &&
            !string.IsNullOrEmpty(world.PlayerPartyTravel.AttackOrderTargetArmyId);

        public static string GetPursuitTargetArmyId(SimulationWorld world) =>
            world?.PlayerPartyTravel?.AttackOrderTargetArmyId ?? string.Empty;

        /// <summary>
        /// 开始追击 target 当前战略位置。先完成全部 legality validation（CanIssueAttackOrder），
        /// 通过后才允许覆盖 PlayerParty 当前普通 Travel；非法 target 不先取消旧旅行再失败。
        /// 若当前已进入 SupportArea 则不进入 pursuit（由 command service 直接立即接战）；
        /// 本方法防御性再检查一次。
        /// </summary>
        public static Result BeginAttackArmy(
            SimulationWorld world,
            PlayerPartyRuntime party,
            string targetArmyId)
        {
            if (world?.Strategic == null || party == null || !party.HasActive)
                return Result.Failure(ErrorCode.InvalidOperation, "PlayerParty 无法发起追击。");

            // 先 validate（用户十四：先 validate 后覆盖 command），绝不先 CancelTravel 再失败。
            if (!PlayerPartyStrategicCombatCommandService.CanIssueAttackOrder(
                    world, party, targetArmyId, out var error))
                return Result.Failure(error.Code, error.Message, error.Detail);

            if (BattleEngagementTriggerService.CanTriggerPlayerPartyEngagement(
                    world, party, targetArmyId, out _))
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "PlayerParty 已在支援范围，应直接接战（AttackArmy 不会走到 pursuit）。");

            if (!world.Strategic.FormalArmies.TryGet(targetArmyId, out var target) || target == null)
                return Result.Failure(ErrorCode.NotFound, "目标军团不存在。");

            var motion = world.PlayerPartyTravel;
            if (motion == null)
                return Result.Failure(ErrorCode.InvalidOperation, "PlayerParty 旅行状态缺失。");

            // 记录 pursuit intent，再走第一条 leg；BeginTravel 失败（无路径等）立即回滚 target，
            // 不留下半个 TargetArmyId（用户十五：Initial no-route 不留半状态）。
            motion.SetAttackOrder(targetArmyId);
            var move = BeginPursuitTravelLeg(world, party, target);
            if (move.IsFailure)
            {
                motion.ClearAttackOrder();
                return move;
            }

            return Result.Success();
        }

        /// <summary>
        /// 取消 PlayerParty pursuit：清 target，并终止当前旅行（保留 canonical position）。
        /// 普通 Move / Gateway 旅行接受新命令前、pursuit 条件失效、接战失败时调用。
        /// </summary>
        public static void CancelPursuit(SimulationWorld world, PlayerPartyRuntime party)
        {
            var motion = world?.PlayerPartyTravel;
            if (motion == null)
                return;

            motion.ClearAttackOrder();
            if (party != null && motion.IsMoving)
                PlayerPartyHexTravelService.CancelTravel(world, party);
        }

        /// <summary>
        /// 每个世界 travel tick 后调用（Host StepTick：TickOnce 内 ArmyHexTravelService.AdvanceAll
        /// 与 PlayerPartyHexTravelService.AdvanceAll 均已推进，target.CurrentHex 为最新）。
        /// 顺序：条件校验 → 先检查 contact（进入 SupportArea 立即停 + 建 Offer）→ 未接触则
        /// target 移动/Player 停下时 retarget。pursuit 内部 retarget 用 BeginPursuitTravelLeg，
        /// 不经过 Host UI flag、不清自身 pursuit intent。
        /// </summary>
        public static void AfterTravelTick(SimulationWorld world, PlayerPartyRuntime party)
        {
            if (world?.Strategic == null || party == null)
                return;

            var motion = world.PlayerPartyTravel;
            if (motion == null || string.IsNullOrEmpty(motion.AttackOrderTargetArmyId))
                return;

            // LocalVisible：Local 层不推进 World pursuit（路线继续走；关图回 World 后继续追击）。
            if (motion.ExecutionMode == PlayerPartyTravelExecutionMode.LocalVisible)
                return;

            var targetArmyId = motion.AttackOrderTargetArmyId;

            // 任一条件失效 → 取消 pursuit，不创建 BattleOffer（用户十）。
            if (!PlayerPartyStrategicCombatCommandService.CanIssueAttackOrder(
                    world, party, targetArmyId, out _))
            {
                CancelPursuit(world, party);
                return;
            }

            if (!world.Strategic.FormalArmies.TryGet(targetArmyId, out var target) || target == null)
            {
                CancelPursuit(world, party);
                return;
            }

            // 首先检查 contact：进入 Defender SupportArea 即接战（不要求走到 target exact Hex）。
            if (BattleEngagementTriggerService.CanTriggerPlayerPartyEngagement(
                    world, party, targetArmyId, out _))
            {
                // 立即停止 travel（保留 canonical position）；CompleteMove 不清 pursuit target。
                if (motion.IsMoving)
                    PlayerPartyHexTravelService.CancelTravel(world, party);

                if (!PlayerPartyStrategicCombatCommandService.TryResolveLinkedStack(
                        world, targetArmyId, out var stack) ||
                    stack == null)
                {
                    CancelPursuit(world, party);
                    return;
                }

                var ok = BattleOfferService.TryBuildOfferForPlayerPartyAttack(world, party, stack);
                if (ok)
                {
                    // Offer 接管；pursuit 完成。
                    motion.ClearAttackOrder();
                    return;
                }

                // 无法建立 Offer：保留位置，清除 pursuit（不留半状态）。
                CancelPursuit(world, party);
                return;
            }

            // 未接触：target 当前 committed Hex 改变或 Player 已停下 → retarget。
            if (!BattleEngagementSpatialQuery.TryGetCommittedArmyHex(world, target, out var targetHex))
            {
                CancelPursuit(world, party);
                return;
            }

            if (!motion.IsMoving || !motion.DestinationHex.Equals(targetHex))
            {
                var move = BeginPursuitTravelLeg(world, party, target);
                if (move.IsFailure)
                {
                    // 目标移动后无路：保留当前 canonical position，清除 pursuit order（用户十五）。
                    CancelPursuit(world, party);
                }
            }
        }

        /// <summary>
        /// pursuit 内部专用 leg：从 target 当前 committed Hex 重新 BeginTravel。
        /// BeginTravel 不触碰 AttackOrderTargetArmyId，因此 retarget 不会清掉自己的 pursuit intent。
        /// </summary>
        static Result BeginPursuitTravelLeg(
            SimulationWorld world,
            PlayerPartyRuntime party,
            FormalArmy target)
        {
            if (!BattleEngagementSpatialQuery.TryGetCommittedArmyHex(world, target, out var targetHex))
                return Result.Failure(ErrorCode.InvalidOperation, "目标军团当前无提交 Hex。");
            return PlayerPartyHexTravelService.BeginTravel(world, party, targetHex);
        }
    }
}
