using System;
using System.Collections.Generic;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Hex H 上 Active Enemy Army 查询（真源 = FormalArmy.CurrentHex + linked Stack）。</summary>
    public static class HexActiveEnemyArmyQuery
    {
        public static void CollectAtHex(
            SimulationWorld world,
            HexCoord hex,
            string friendlyFactionId,
            List<HexActiveEnemyArmyTarget> into)
        {
            into?.Clear();
            if (world?.Strategic?.FormalArmies == null || into == null)
                return;

            var seenStackIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in world.Strategic.FormalArmies.Armies)
            {
                var army = kv.Value;
                if (army == null || string.IsNullOrEmpty(army.ArmyId))
                    continue;
                if (IsFriendlyFaction(army.FactionId, friendlyFactionId))
                    continue;
                if (!ArmyPostBattleSyncService.HasMacroOrderLivingMember(world, army))
                    continue;
                if (!TryGetOccupyingHexes(world, army, out var occupied))
                    continue;
                if (!occupied.Contains(hex))
                    continue;
                if (!TryResolveLinkedStack(world, army.ArmyId, out var stack) || stack == null)
                    continue;
                if (stack.HasDownedRemnant)
                    continue;
                if (!seenStackIds.Add(stack.Id))
                    continue;

                into.Add(BuildTarget(world, army, stack, friendlyFactionId));
            }

            if (world.Strategic.Armies == null)
                return;

            foreach (var kv in world.Strategic.Armies.Stacks)
            {
                var stack = kv.Value;
                if (stack == null || string.IsNullOrEmpty(stack.Id) || seenStackIds.Contains(stack.Id))
                    continue;
                if (stack.HasDownedRemnant)
                    continue;
                if (IsFriendlyFaction(stack.FactionId, friendlyFactionId))
                    continue;
                if (!TryResolveStackOccupyingHex(world, stack, hex))
                    continue;

                FormalArmy army = null;
                if (!string.IsNullOrEmpty(stack.FormalArmyId))
                    world.Strategic.FormalArmies.TryGet(stack.FormalArmyId, out army);
                if (army != null &&
                    !ArmyPostBattleSyncService.HasMacroOrderLivingMember(world, army))
                    continue;

                seenStackIds.Add(stack.Id);
                into.Add(BuildTarget(world, army, stack, friendlyFactionId));
            }
        }

        public static bool TryGetPrimaryAtHex(
            SimulationWorld world,
            HexCoord hex,
            string friendlyFactionId,
            out HexActiveEnemyArmyTarget target)
        {
            target = null;
            var scratch = new List<HexActiveEnemyArmyTarget>(2);
            CollectAtHex(world, hex, friendlyFactionId, scratch);
            if (scratch.Count == 0)
                return false;
            target = scratch[0];
            return true;
        }

        static bool IsFriendlyFaction(string armyFactionId, string friendlyFactionId)
        {
            return !string.IsNullOrEmpty(friendlyFactionId) &&
                   string.Equals(armyFactionId, friendlyFactionId, StringComparison.Ordinal);
        }

        static bool TryGetOccupyingHexes(
            SimulationWorld world,
            FormalArmy army,
            out HashSet<HexCoord> occupied)
        {
            occupied = new HashSet<HexCoord>();
            if (world == null || army == null)
                return false;

            ArmyHexCommandService.EnsureArmyOnHex(world, army);
            if (!army.UsesHexStrategicPosition)
                return false;

            occupied.Add(army.CurrentHex);
            if (army.State == FormalArmyState.Moving &&
                army.TryGetActiveStepHexes(out var from, out var to))
            {
                occupied.Add(from);
                occupied.Add(to);
            }

            return occupied.Count > 0;
        }

        static bool TryResolveStackOccupyingHex(
            SimulationWorld world,
            ArmyStack stack,
            HexCoord targetHex)
        {
            if (world == null || stack == null)
                return false;

            if (ArmyStackAdapter.TryGetFormalArmy(world, stack, out var army) &&
                army != null &&
                TryGetOccupyingHexes(world, army, out var occupied) &&
                occupied.Contains(targetHex))
                return true;

            if (ArmyHexBattleAnchorService.TryResolveHexForNode(world, stack.NodeId, out var nodeHex) &&
                nodeHex.Equals(targetHex))
                return true;

            return false;
        }

        static HexActiveEnemyArmyTarget BuildTarget(
            SimulationWorld world,
            FormalArmy army,
            ArmyStack stack,
            string friendlyFactionId)
        {
            var target = new HexActiveEnemyArmyTarget
            {
                FormalArmyId = army?.ArmyId ?? stack.FormalArmyId ?? string.Empty,
                StackId = stack.Id,
                DisplayName = ResolveDisplayName(stack, army?.ArmyId ?? stack.Id),
                CanAttack = true
            };
            ApplyWarGate(world, friendlyFactionId, army?.FactionId ?? stack.FactionId, target);
            return target;
        }

        static void ApplyWarGate(
            SimulationWorld world,
            string friendlyFactionId,
            string defenderFactionId,
            HexActiveEnemyArmyTarget target)
        {
            if (target == null ||
                string.IsNullOrEmpty(friendlyFactionId) ||
                string.IsNullOrEmpty(defenderFactionId) ||
                string.Equals(friendlyFactionId, defenderFactionId, StringComparison.Ordinal))
                return;

            if (WarGateService.CanAttack(world, friendlyFactionId, defenderFactionId))
                return;

            target.CanAttack = false;
            target.BlockReason = "未宣战：无法军事攻击该势力军队";
        }

        static string ResolveDisplayName(ArmyStack stack, string fallback)
        {
            if (stack != null && !string.IsNullOrEmpty(stack.DisplayName))
                return stack.DisplayName;
            return fallback ?? string.Empty;
        }

        static bool TryResolveLinkedStack(SimulationWorld world, string armyId, out ArmyStack stack)
        {
            stack = null;
            if (world?.Strategic?.Armies == null || string.IsNullOrEmpty(armyId))
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
