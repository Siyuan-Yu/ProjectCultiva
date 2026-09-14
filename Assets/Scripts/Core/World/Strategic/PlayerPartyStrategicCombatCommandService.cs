using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// CW-U4.1 保留的 Legacy PlayerParty AttackArmy API。产品 WorldMap 不再调用，
    /// gate 恒拒绝；真实人物冲突由 Character/Squad hostile action 创建 CharacterEncounter。
    /// </summary>
    public static class PlayerPartyStrategicCombatCommandService
    {
        /// <summary>
        /// Legacy gate：恒拒绝，防止旧回调恢复玩家大地图攻击。
        /// </summary>
        public static bool CanIssueAttackOrder(
            SimulationWorld world,
            PlayerPartyRuntime party,
            string targetArmyId,
            out GameError error)
        {
            error = new GameError(ErrorCode.InvalidOperation, "玩家大地图攻击已退役；请在连续世界与实际人物接战。");
            return false;
        }

        /// <summary>
        /// 是否可立即接战：在 CanIssueAttackOrder 基础上要求 PlayerParty committed Hex
        /// 已进入 Defender 的 frozen/current SupportArea（BattleEngagementTriggerService
        /// 单一空间权威，不用 float 距离 / PresenceHex fallback）。
        /// </summary>
        public static bool CanEngageArmyNow(
            SimulationWorld world,
            PlayerPartyRuntime party,
            string targetArmyId,
            out GameError error)
        {
            error = default;
            if (!CanIssueAttackOrder(world, party, targetArmyId, out error))
                return false;

            if (!BattleEngagementTriggerService.CanTriggerPlayerPartyEngagement(
                    world,
                    party,
                    targetArmyId,
                    out var triggerReason))
            {
                error = new GameError(
                    ErrorCode.InvalidOperation,
                    "PlayerParty must enter the defender battle support area before attacking.",
                    triggerReason);
                return false;
            }

            return true;
        }

        /// <summary>兼容别名：是否可立即接战（= CanEngageArmyNow）。</summary>
        public static bool CanAttackArmyNow(
            SimulationWorld world,
            PlayerPartyRuntime party,
            string targetArmyId,
            out GameError error) =>
            CanEngageArmyNow(world, party, targetArmyId, out error);

        /// <summary>
        /// Compatibility-only old PlayerParty AttackArmy command. No product Host/UI caller may use it.
        /// 流程：CanIssueAttackOrder（先 validate，后覆盖命令）→ 已进入 SupportArea 则立即
        /// 建立 BattleOffer；否则由 PlayerPartyHexPursuitService 开始追击 target 当前战略位置。
        /// 正在普通旅行时若立即接战，先 CancelTravel 终止（此时其它合法条件已成立）；
        /// 追击路径自身不依赖 Host UI flag，pursuit 内部 retarget 不会清掉 pursuit intent。
        /// </summary>
        public static Result AttackArmy(
            SimulationWorld world,
            PlayerPartyRuntime party,
            string targetArmyId)
        {
            if (world?.Strategic == null || party == null || !party.HasActive)
                return Result.Failure(ErrorCode.InvalidOperation, "PlayerParty 无法发起攻击。");

            if (!CanIssueAttackOrder(world, party, targetArmyId, out var error))
                return Result.Failure(error.Code, error.Message, error.Detail);

            if (CanEngageArmyNow(world, party, targetArmyId, out _))
                return EngageNow(world, party, targetArmyId);

            // 尚未进入 SupportArea：追击。非法目标已在 CanIssueAttackOrder 拦截，
            // 不会先取消旧旅行再失败（用户十四：先 validate 后覆盖）。
            return PlayerPartyHexPursuitService.BeginAttackArmy(world, party, targetArmyId);
        }

        /// <summary>兼容别名：= AttackArmy（立即接战 or pursuit）。</summary>
        public static Result AttackArmyNow(
            SimulationWorld world,
            PlayerPartyRuntime party,
            string targetArmyId) =>
            AttackArmy(world, party, targetArmyId);

        /// <summary>
        /// Legacy site/flag siege and old-session LocalCombat handoff compatibility gate（不要求已 War）。
        /// 只验证「可以建立 Local-origin BattleOffer」；真正 DeclareWar 的 commitment point
        /// 在玩家确认「手动战斗」时（HostStrategicInterruptPresenter → StrategicMilitaryAggressionService）。
        /// 允许 War / Hostile / Neutral；拒绝：同阵营、Friendly、无 living member、不在 SupportArea、
        /// 已有 modal。绝对不能调用 CanIssueAttackOrder —— 它要求 WarGate.CanAttack（WorldMap 语义）。
        /// </summary>
        public static Result TryPrepareLocalPlayerPartyMilitaryAttackOffer(
            SimulationWorld world,
            PlayerPartyRuntime party,
            string targetArmyId)
        {
            if (world?.Strategic == null || party == null || !party.HasActive)
                return Result.Failure(ErrorCode.InvalidOperation, "PlayerParty 无 Active 角色。");

            if (ArmyService.TryGetArmyForCharacter(world, party.ActiveCharacterId, out _))
                return Result.Failure(ErrorCode.InvalidOperation, "Active 角色隶属军团，不能由 PlayerParty 发起攻击。");

            if (world.Strategic.IsModalEncounter || world.Strategic.HasBattleOffer)
                return Result.Failure(ErrorCode.InvalidOperation, "已有接战/战斗进行中。");

            if (string.IsNullOrEmpty(targetArmyId) ||
                !world.Strategic.FormalArmies.TryGet(targetArmyId, out var defender) ||
                defender == null)
                return Result.Failure(ErrorCode.NotFound, "目标军团不存在。");

            if (!ArmyPostBattleSyncService.HasMacroOrderLivingMember(world, defender))
                return Result.Failure(ErrorCode.InvalidOperation, "目标军团没有可战成员。");

            if (!TryResolveLinkedStack(world, targetArmyId, out var stack) || stack == null)
                return Result.Failure(ErrorCode.NotFound, "目标军团未链接 ArmyStack。");

            var playerFaction = world.Strategic.PlayerFactionId ?? string.Empty;
            var enemyFaction = defender.FactionId ?? string.Empty;
            if (string.IsNullOrEmpty(playerFaction) || string.IsNullOrEmpty(enemyFaction))
                return Result.Failure(ErrorCode.InvalidOperation, "阵营信息缺失。");
            if (string.Equals(playerFaction, enemyFaction, System.StringComparison.Ordinal))
                return Result.Failure(ErrorCode.InvalidOperation, "不能攻击同阵营单位。");

            var stance = world.Strategic.Diplomacy?.GetStance(playerFaction, enemyFaction) ?? FactionStance.Neutral;
            if (stance == FactionStance.Friendly)
                return Result.Failure(ErrorCode.InvalidOperation, "不能攻击友好阵营单位。");

            if (!BattleEngagementTriggerService.CanTriggerPlayerPartyEngagement(
                    world,
                    party,
                    targetArmyId,
                    out var triggerReason))
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "PlayerParty must enter the defender battle support area before attacking.",
                    triggerReason);

            return BattleOfferService.TryBuildOfferForLocalPlayerPartyMilitaryAttack(
                    world, party, stack)
                ? Result.Success()
                : Result.Failure(ErrorCode.InvalidOperation, "无法建立 PlayerParty 军事接战 Offer。");
        }

        static Result EngageNow(
            SimulationWorld world,
            PlayerPartyRuntime party,
            string targetArmyId)
        {
            if (world.PlayerPartyTravel != null && world.PlayerPartyTravel.IsMoving)
            {
                var cancel = PlayerPartyHexTravelService.CancelTravel(world, party);
                if (cancel.IsFailure)
                    return cancel;
            }

            if (!world.Strategic.FormalArmies.TryGet(targetArmyId, out var defender) || defender == null)
                return Result.Failure(ErrorCode.NotFound, "目标军团不存在。");
            if (!TryResolveLinkedStack(world, targetArmyId, out var stack) || stack == null)
                return Result.Failure(ErrorCode.NotFound, "目标军团未链接 ArmyStack。");

            var ok = BattleOfferService.TryBuildOfferForPlayerPartyAttack(world, party, stack);
            if (!ok)
                return Result.Failure(ErrorCode.InvalidOperation, "无法建立 PlayerParty 接战 Offer。");

            return Result.Success();
        }

        /// <summary>PlayerPartyHexPursuitService 复用：按 FormalArmyId 找 linked ArmyStack。</summary>
        public static bool TryResolveLinkedStack(
            SimulationWorld world,
            string formalArmyId,
            out ArmyStack stack)
        {
            stack = null;
            if (world?.Strategic?.Armies == null || string.IsNullOrEmpty(formalArmyId))
                return false;

            foreach (var kv in world.Strategic.Armies.Stacks)
            {
                var candidate = kv.Value;
                if (candidate == null)
                    continue;
                if (string.Equals(candidate.FormalArmyId, formalArmyId, System.StringComparison.Ordinal))
                {
                    stack = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
