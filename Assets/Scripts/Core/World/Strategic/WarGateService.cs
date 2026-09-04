using System;
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

            var war = new War
            {
                WarId = world.Strategic.Wars.AllocateWarId(),
                Active = true
            };
            war.AddAttacker(factionA);
            war.AddDefender(factionB);
            ExpandAllianceWarBinding(world, war, factionA, factionB);
            world.Strategic.Wars.Register(war);
            world.Strategic.Diplomacy.SetStance(factionA, factionB, FactionStance.War);
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

        static void ExpandAllianceWarBinding(
            SimulationWorld world,
            War war,
            string declarerFaction,
            string targetFaction)
        {
            if (world?.Strategic?.Alliances == null)
                return;

            var declarerAllies = world.Strategic.Alliances.GetAllianceMembers(declarerFaction);
            for (var i = 0; i < declarerAllies.Count; i++)
            {
                var ally = declarerAllies[i];
                if (string.IsNullOrEmpty(ally) ||
                    string.Equals(ally, declarerFaction, StringComparison.Ordinal))
                    continue;
                war.AddAttacker(ally);
                world.Strategic.Diplomacy.SetStance(ally, targetFaction, FactionStance.War);
            }
        }
    }
}
