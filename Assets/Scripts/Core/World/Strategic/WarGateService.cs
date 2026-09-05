using System;
using System.Collections.Generic;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Phase G/I：正规 Faction 军事攻击／占点门槛。</summary>
    public static class WarGateService
    {
        /// <summary>无副作用的宣战资格检查，供需要先准备多步事务的 Scenario 使用。</summary>
        public static Result CanDeclareWar(SimulationWorld world, string factionA, string factionB)
        {
            if (world?.Strategic?.Wars == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld incomplete.");
            if (string.IsNullOrEmpty(factionA) || string.IsNullOrEmpty(factionB))
                return Result.Failure(ErrorCode.InvalidArgument, "Both factions required.");
            if (string.Equals(factionA, factionB, StringComparison.Ordinal))
                return Result.Failure(ErrorCode.InvalidOperation, "Cannot declare war on self.");
            return Result.Success();
        }

        public static Result DeclareWar(SimulationWorld world, string factionA, string factionB)
        {
            var canDeclare = CanDeclareWar(world, factionA, factionB);
            if (canDeclare.IsFailure)
                return canDeclare;

            if (IsAtWar(world, factionA, factionB))
                return Result.Success();

            if (!TryResolveWarSides(world, factionA, factionB, out var attackers, out var defenders, out var reason))
                return Result.Failure(ErrorCode.InvalidOperation, reason);

            var war = new War
            {
                WarId = world.Strategic.Wars.AllocateWarId(),
                Active = true
            };
            foreach (var attacker in attackers)
                war.AddAttacker(attacker);
            foreach (var defender in defenders)
                war.AddDefender(defender);
            world.Strategic.Wars.Register(war);
            foreach (var attacker in attackers)
                foreach (var defender in defenders)
                    world.Strategic.Diplomacy.SetStance(attacker, defender, FactionStance.War);
            return Result.Success();
        }

        public static bool IsAtWar(SimulationWorld world, string factionA, string factionB)
        {
            if (world?.Strategic?.Wars == null ||
                string.IsNullOrEmpty(factionA) ||
                string.IsNullOrEmpty(factionB))
                return false;
            if (string.Equals(factionA, factionB, StringComparison.Ordinal))
                return false;

            foreach (var war in world.Strategic.Wars.EnumerateActive())
            {
                var aSide = war.IsAttacker(factionA) || war.IsDefender(factionA);
                var bSide = war.IsAttacker(factionB) || war.IsDefender(factionB);
                if (aSide && bSide && war.IsAttacker(factionA) != war.IsAttacker(factionB))
                    return true;
            }

            return false;
        }

        public static bool CanAttack(SimulationWorld world, string attackerFaction, string defenderFaction)
        {
            if (string.IsNullOrEmpty(attackerFaction) || string.IsNullOrEmpty(defenderFaction))
                return false;
            if (string.Equals(attackerFaction, defenderFaction, StringComparison.Ordinal))
                return false;
            return IsAtWar(world, attackerFaction, defenderFaction);
        }

        public static bool CanMilitaryCapture(
            SimulationWorld world,
            string attackerFaction,
            string defenderOwnerFaction)
        {
            if (string.IsNullOrEmpty(attackerFaction) || string.IsNullOrEmpty(defenderOwnerFaction))
                return false;
            if (string.Equals(attackerFaction, defenderOwnerFaction, StringComparison.Ordinal))
                return false;
            return CanAttack(world, attackerFaction, defenderOwnerFaction);
        }

        /// <summary>
        /// 战争两侧的正式闭包：联盟成员与直属附庸都会随所属一侧加入，直到没有新成员。
        /// 冲突数据绝不允许同一势力静默落在两侧。
        /// </summary>
        static bool TryResolveWarSides(
            SimulationWorld world,
            string attackerSeed,
            string defenderSeed,
            out HashSet<string> attackers,
            out HashSet<string> defenders,
            out string reason)
        {
            attackers = new HashSet<string>(StringComparer.Ordinal) { attackerSeed };
            defenders = new HashSet<string>(StringComparer.Ordinal) { defenderSeed };
            reason = string.Empty;
            var alliances = world?.Strategic?.Alliances;
            var vassalages = world?.Strategic?.Vassalages;
            var changed = true;
            while (changed)
            {
                changed = false;
                if (!ExpandSide(attackers, defenders, alliances, vassalages, ref changed, out reason) ||
                    !ExpandSide(defenders, attackers, alliances, vassalages, ref changed, out reason))
                    return false;
            }
            return true;
        }

        static bool ExpandSide(
            HashSet<string> side,
            HashSet<string> otherSide,
            AllianceBoard alliances,
            VassalageBoard vassalages,
            ref bool changed,
            out string reason)
        {
            reason = string.Empty;
            var additions = new List<string>();
            foreach (var faction in side)
            {
                if (alliances != null)
                    additions.AddRange(alliances.GetAllianceMembers(faction));

                if (vassalages != null)
                {
                    if (vassalages.TryGetOverlord(faction, out var overlord))
                        additions.Add(overlord);
                    foreach (var relation in vassalages.All)
                        if (string.Equals(relation.Value, faction, StringComparison.Ordinal))
                            additions.Add(relation.Key);
                }
            }

            for (var i = 0; i < additions.Count; i++)
            {
                var candidate = additions[i];
                if (string.IsNullOrEmpty(candidate) || side.Contains(candidate))
                    continue;
                if (otherSide.Contains(candidate))
                {
                    reason = "War participant conflict: " + candidate + " belongs to both sides.";
                    return false;
                }
                side.Add(candidate);
                changed = true;
            }
            return true;
        }
    }
}
