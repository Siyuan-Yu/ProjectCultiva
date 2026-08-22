using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>大地图 Formal Army 宏观队伍查询（Host 与 BattleOffer 共用）。</summary>
    public static class ArmyMacroPartyQueries
    {
        public static bool TryResolvePlayerArmyId(
            SimulationWorld world,
            string selectedFormalArmyId,
            IReadOnlyList<EntityId> fallbackParty,
            out string armyId)
        {
            armyId = string.Empty;
            if (world?.Strategic?.FormalArmies == null)
                return false;

            if (!string.IsNullOrEmpty(selectedFormalArmyId) &&
                world.Strategic.FormalArmies.TryGet(selectedFormalArmyId, out _))
            {
                armyId = selectedFormalArmyId;
                return true;
            }

            if (fallbackParty != null &&
                ArmyStackAdapter.TryResolveAttackerArmyId(world, fallbackParty, out var resolved) &&
                !string.IsNullOrEmpty(resolved))
            {
                armyId = resolved;
                return true;
            }

            return false;
        }

        public static void CollectLivingMembers(
            SimulationWorld world,
            string armyId,
            List<EntityId> into)
        {
            if (into == null)
                return;
            if (world?.Strategic?.FormalArmies == null || string.IsNullOrEmpty(armyId))
                return;
            if (!world.Strategic.FormalArmies.TryGet(armyId, out var army) || army == null)
                return;

            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var id = new EntityId(army.MemberCharacterIds[i]);
                if (id.IsNone || !LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, id))
                    continue;
                for (var j = 0; j < into.Count; j++)
                {
                    if (into[j] == id)
                        goto nextMember;
                }

                into.Add(id);
                nextMember: ;
            }
        }

        public static void ExpandMandatoryLivingToFormalArmies(
            SimulationWorld world,
            List<EntityId> party)
        {
            if (world == null || party == null || party.Count == 0)
                return;

            var armyIds = new List<string>(2);
            for (var i = 0; i < party.Count; i++)
            {
                if (!ArmyService.TryGetArmyForCharacter(world, party[i], out var army) ||
                    army == null ||
                    string.IsNullOrEmpty(army.ArmyId))
                    continue;
                var found = false;
                for (var j = 0; j < armyIds.Count; j++)
                {
                    if (string.Equals(armyIds[j], army.ArmyId, System.StringComparison.Ordinal))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                    armyIds.Add(army.ArmyId);
            }

            for (var i = 0; i < armyIds.Count; i++)
                CollectLivingMembers(world, armyIds[i], party);
        }

        public static bool IsPlayerFactionArmy(SimulationWorld world, FormalArmy army)
        {
            if (world == null || army == null || string.IsNullOrEmpty(army.FactionId))
                return false;
            var playerFaction = world.Strategic?.PlayerFactionId ?? string.Empty;
            return !string.IsNullOrEmpty(playerFaction) &&
                   string.Equals(army.FactionId, playerFaction, System.StringComparison.Ordinal);
        }
    }
}
