using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Hex 移动抵达后进入敌方残留战场（非 Pursuit；MoveToHex + OnArrival EnterLingering）。
    /// </summary>
    public static class ArmyHexLingeringArrivalService
    {
        static readonly List<EntityId> PartyScratch = new List<EntityId>(8);

        public static Result BeginMoveToAttackLingering(
            SimulationWorld world,
            string attackerArmyId,
            HexCoord targetHex,
            string enemyStackId)
        {
            if (!ArmyHexCommandService.IsHexStrategicActive(world))
                return Result.Failure(ErrorCode.InvalidOperation, "Hex strategic map is not active.");
            if (string.IsNullOrEmpty(attackerArmyId) || string.IsNullOrEmpty(enemyStackId))
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid lingering attack order.");
            if (!LingeringBattlefieldQueryService.TryGetLingeringBattlefieldAtHex(
                    world, targetHex, out var ctx) ||
                !string.Equals(ctx.EnemyStackId, enemyStackId, StringComparison.Ordinal))
                return Result.Failure(ErrorCode.InvalidOperation, "目标残留战场已不存在。");

            if (!world.Strategic.FormalArmies.TryGet(attackerArmyId, out var attacker) || attacker == null)
                return Result.Failure(ErrorCode.NotFound, "Attacker army not found.", attackerArmyId);
            if (attacker.State == FormalArmyState.Garrisoned)
                return Result.Failure(ErrorCode.InvalidOperation, "驻扎中的军团无法攻击。");
            if (!ArmyPostBattleSyncService.HasMacroOrderLivingMember(world, attacker))
                return Result.Failure(ErrorCode.InvalidOperation, "该军团已无可用成员。");

            if (!world.Strategic.Armies.TryGet(enemyStackId, out var stack) || stack == null)
                return Result.Failure(ErrorCode.NotFound, "Enemy stack not found.", enemyStackId);
            if (!ValidateAttackGate(world, attacker, stack, out var gateError))
                return Result.Failure(gateError);

            ArmyHexCommandService.EnsureArmyOnHex(world, attacker);
            ArmyHexPursuitService.CancelPursuitForAttacker(world, attackerArmyId);

            var rt = world.Strategic.Encounter;
            rt.PendingLingeringAttackArmyId = attackerArmyId;
            rt.PendingLingeringAttackStackId = enemyStackId;
            rt.PendingLingeringAttackHexQ = targetHex.Q;
            rt.PendingLingeringAttackHexR = targetHex.R;

            if (attacker.CurrentHex.Equals(targetHex) && attacker.State != FormalArmyState.Moving)
                return TryEnterAtHex(world, attackerArmyId, targetHex, enemyStackId, out _);

            return ArmyHexTravelService.MoveArmyToHex(world, attackerArmyId, targetHex);
        }

        public static void AfterTravelTick(SimulationWorld world)
        {
            if (!ArmyHexCommandService.IsHexStrategicActive(world) || world?.Strategic?.Encounter == null)
                return;

            var rt = world.Strategic.Encounter;
            if (!rt.HasPendingLingeringAttack)
                return;

            var targetHex = new HexCoord(rt.PendingLingeringAttackHexQ, rt.PendingLingeringAttackHexR);
            if (!LingeringBattlefieldQueryService.TryGetLingeringBattlefieldAtHex(world, targetHex, out var ctx) ||
                !string.Equals(ctx.EnemyStackId, rt.PendingLingeringAttackStackId, StringComparison.Ordinal))
            {
                rt.ClearPendingLingeringAttack();
                return;
            }

            if (!world.Strategic.FormalArmies.TryGet(rt.PendingLingeringAttackArmyId, out var army) ||
                army == null)
            {
                rt.ClearPendingLingeringAttack();
                return;
            }

            ArmyHexCommandService.EnsureArmyOnHex(world, army);
            if (army.State == FormalArmyState.Moving || !army.CurrentHex.Equals(targetHex))
                return;

            TryEnterAtHex(world, rt.PendingLingeringAttackArmyId, targetHex, rt.PendingLingeringAttackStackId, out _);
        }

        public static Result TryEnterAtHex(
            SimulationWorld world,
            string attackerArmyId,
            HexCoord targetHex,
            string enemyStackId,
            out string statusHint)
        {
            statusHint = string.Empty;
            if (!world.Strategic.FormalArmies.TryGet(attackerArmyId, out var army) || army == null)
                return Result.Failure(ErrorCode.NotFound, "Attacker army not found.", attackerArmyId);

            PartyScratch.Clear();
            PartyScratch.AddRange(ArmyStackAdapter.CollectLivingMemberIds(world, army));
            if (PartyScratch.Count == 0)
            {
                world.Strategic.Encounter.ClearPendingLingeringAttack();
                return Result.Failure(ErrorCode.InvalidOperation, "该军团已无可用成员。");
            }

            if (BattleOfferService.TryBuildOfferForEnemyRemnantReentry(
                    world, PartyScratch, enemyStackId, "残留战场", targetHex))
            {
                world.Strategic.Encounter.ClearPendingLingeringAttack();
                statusHint = "接战弹窗已打开";
                return Result.Success();
            }

            world.Strategic.Encounter.ClearPendingLingeringAttack();
            statusHint = "无法进入残留战场（接战点已失效）";
            return Result.Failure(ErrorCode.InvalidOperation, statusHint);
        }

        static bool ValidateAttackGate(
            SimulationWorld world,
            FormalArmy attacker,
            ArmyStack stack,
            out GameError error)
        {
            error = default;
            if (world == null || attacker == null || stack == null)
                return true;
            if (string.IsNullOrEmpty(attacker.FactionId) || string.IsNullOrEmpty(stack.FactionId))
                return true;
            if (string.Equals(attacker.FactionId, stack.FactionId, StringComparison.Ordinal))
                return true;
            if (WarGateService.CanAttack(world, attacker.FactionId, stack.FactionId))
                return true;
            error = new GameError(ErrorCode.InvalidOperation, "未宣战：无法军事攻击该势力军队");
            return false;
        }
    }
}
