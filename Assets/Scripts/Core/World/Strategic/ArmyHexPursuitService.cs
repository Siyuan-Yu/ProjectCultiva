using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Hex 追击：TargetArmyId 为真源；禁止 Route / RouteProgress pursuit。</summary>
    public static class ArmyHexPursuitService
    {
        static readonly List<EntityId> PartyScratch = new List<EntityId>(8);

        public static Result BeginAttackArmy(
            SimulationWorld world,
            string attackerArmyId,
            string targetArmyId)
        {
            if (!ArmyHexCommandService.IsHexStrategicActive(world))
                return Result.Failure(ErrorCode.InvalidOperation, "Hex strategic map is not active.");
            if (string.IsNullOrEmpty(attackerArmyId) || string.IsNullOrEmpty(targetArmyId))
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid attack order.");
            if (!world.Strategic.FormalArmies.TryGet(attackerArmyId, out var attacker) || attacker == null)
                return Result.Failure(ErrorCode.NotFound, "Attacker army not found.", attackerArmyId);
            if (!world.Strategic.FormalArmies.TryGet(targetArmyId, out var target) || target == null)
                return Result.Failure(ErrorCode.NotFound, "Target army not found.", targetArmyId);
            if (attacker.State == FormalArmyState.Garrisoned)
                return Result.Failure(ErrorCode.InvalidOperation, "Garrisoned army cannot attack.");

            ArmyHexCommandService.EnsureArmyOnHex(world, attacker);
            ArmyHexCommandService.EnsureArmyOnHex(world, target);

            if (!TryResolveLinkedStack(world, targetArmyId, out var stack) || stack == null)
                return Result.Failure(ErrorCode.InvalidOperation, "Target army has no linked stack for battle offer.");

            if (!ValidateAttackGate(world, attacker, stack, out var gateError))
                return Result.Failure(gateError);

            BeginPursuitInternal(world, attackerArmyId, targetArmyId, stack);
            return ArmyHexTravelService.MoveArmyToHex(world, attackerArmyId, target.CurrentHex);
        }

        public static Result BeginAttackStack(
            SimulationWorld world,
            string attackerArmyId,
            ArmyStack stack)
        {
            if (stack == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid attack target.");
            if (ArmyStackAdapter.TryGetFormalArmy(world, stack, out var defender) && defender != null)
                return BeginAttackArmy(world, attackerArmyId, defender.ArmyId);
            return Result.Failure(ErrorCode.InvalidOperation, "Stack has no formal army for hex pursuit.");
        }

        static void BeginPursuitInternal(
            SimulationWorld world,
            string attackerArmyId,
            string targetArmyId,
            ArmyStack stack)
        {
            world.Strategic.ClearArrivalNotice();
            var rt = world.Strategic.Encounter;
            rt.PursueAttackerArmyId = attackerArmyId ?? string.Empty;
            rt.PursueDefenderArmyId = targetArmyId ?? string.Empty;
            rt.PursueStackId = stack?.Id ?? string.Empty;

            if (!world.Strategic.FormalArmies.TryGet(attackerArmyId, out var attacker) || attacker == null)
                return;

            PartyScratch.Clear();
            for (var i = 0; i < attacker.MemberCharacterIds.Count; i++)
            {
                var id = new EntityId(attacker.MemberCharacterIds[i]);
                if (!id.IsNone)
                    PartyScratch.Add(id);
            }

            rt.SetPursueParty(PartyScratch);
            for (var i = 0; i < PartyScratch.Count; i++)
            {
                var id = PartyScratch[i];
                if (world.WorldPresence.TryGet(id, out var presence) && presence != null)
                {
                    presence.ClearFollow();
                    presence.CombatPursuitStackId = stack?.Id ?? string.Empty;
                }
            }
        }

        public static void CancelPursuitForAttacker(SimulationWorld world, string attackerArmyId)
        {
            if (world?.Strategic == null || string.IsNullOrEmpty(attackerArmyId))
                return;

            var rt = world.Strategic.Encounter;
            if (!string.Equals(rt.PursueAttackerArmyId, attackerArmyId, StringComparison.Ordinal))
                return;

            StrategicPursuitService.ClearPursuit(world);
        }

        public static void AfterTravelTick(SimulationWorld world)
        {
            if (!ArmyHexCommandService.IsHexStrategicActive(world) || world?.Strategic == null)
                return;

            var rt = world.Strategic.Encounter;
            if (string.IsNullOrEmpty(rt.PursueAttackerArmyId) ||
                string.IsNullOrEmpty(rt.PursueDefenderArmyId))
                return;

            if (!world.Strategic.FormalArmies.TryGet(rt.PursueAttackerArmyId, out var pursuer) ||
                pursuer == null ||
                !world.Strategic.FormalArmies.TryGet(rt.PursueDefenderArmyId, out var target) ||
                target == null)
            {
                StrategicPursuitService.ClearPursuit(world);
                return;
            }

            ArmyHexCommandService.EnsureArmyOnHex(world, pursuer);
            ArmyHexCommandService.EnsureArmyOnHex(world, target);

            if (!ArmyPostBattleSyncService.HasMacroOrderLivingMember(world, pursuer) ||
                !ArmyPostBattleSyncService.HasMacroOrderLivingMember(world, target))
            {
                StrategicPursuitService.ClearPursuit(world);
                return;
            }

            if (!world.Strategic.Armies.TryGet(rt.PursueStackId, out var stack) || stack == null)
            {
                StrategicPursuitService.ClearPursuit(world);
                return;
            }

            ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, stack);
            ArmyPresenceAdapter.SyncFromArmy(world, pursuer);

            if (ArmyHexBattleAnchorService.TryDetectHexContact(pursuer, target))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                SecondBattleAnchorTrace.EmitArmyHex(
                    "Contact.HexPursuit",
                    world,
                    pursuer,
                    target.CurrentHex);
#endif
                var pursue = CollectLivingParty(world, pursuer);
                var ready = new List<EntityId>(pursue.Count);
                StrategicEngageRules.CollectPartyReadyToEngageStack(world, pursue, stack, ready);
                if (ready.Count > 0 &&
                    BattleOfferService.TryBuildOfferForArmy(world, ready, stack, "追击接战"))
                {
                    world.Strategic.ClearArrivalNotice();
                    return;
                }
            }

            if (target.CurrentHex != pursuer.DestinationHex ||
                pursuer.State != FormalArmyState.Moving)
            {
                ArmyHexTravelService.MoveArmyToHex(world, pursuer.ArmyId, target.CurrentHex);
            }
        }

        public static bool TryDetectHexContact(FormalArmy pursuer, FormalArmy target) =>
            ArmyHexBattleAnchorService.TryDetectHexContact(pursuer, target);

        static List<EntityId> CollectLivingParty(SimulationWorld world, FormalArmy army)
        {
            var list = new List<EntityId>(army?.MemberCharacterIds.Count ?? 0);
            if (army == null)
                return list;
            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var id = new EntityId(army.MemberCharacterIds[i]);
                if (!id.IsNone &&
                    LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, id))
                    list.Add(id);
            }

            return list;
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
