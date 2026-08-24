using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// FormalArmy StrategicPosition ? ?? WorldAgentPresence ?????Pure Hex??
    /// </summary>
    public static class ArmyPresenceAdapter
    {
        public static void SyncFromArmy(SimulationWorld world, FormalArmy army)
        {
            if (world == null || army == null)
                return;

            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var memberId = new EntityId(army.MemberCharacterIds[i]);
                if (memberId.IsNone)
                    continue;
                if (!LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, memberId))
                    continue;
                if (!world.WorldPresence.TryGet(memberId, out var presence) || presence == null)
                    continue;

                ProjectMemberPresence(world, army, presence);
            }
        }

        public static void SyncAll(SimulationWorld world)
        {
            if (world?.Strategic?.FormalArmies == null)
                return;
            foreach (var kv in world.Strategic.FormalArmies.Armies)
                SyncFromArmy(world, kv.Value);
        }

        static void ProjectMemberPresence(
            SimulationWorld world,
            FormalArmy army,
            WorldAgentPresence presence)
        {
            var pursueStackId = ResolvePursuitStackId(world, army);
            if (string.IsNullOrEmpty(pursueStackId))
                presence.ClearCombatPursuit();
            else
                presence.CombatPursuitStackId = pursueStackId;

            if (world.Strategic.Sites.TryGetAtHex(army.CurrentHex, out var site) &&
                site != null &&
                !string.IsNullOrEmpty(site.SiteId))
            {
                presence.Mode = PartyWorldPresenceMode.AtSite;
                presence.SiteId = site.SiteId;
                presence.ClearHexPresence();
                return;
            }

            presence.SetAtHex(army.CurrentHex);
        }

        static string ResolvePursuitStackId(SimulationWorld world, FormalArmy army)
        {
            var rt = world?.Strategic?.Encounter;
            if (rt == null || army == null || string.IsNullOrEmpty(army.ArmyId))
                return string.Empty;
            if (!string.Equals(rt.PursueAttackerArmyId, army.ArmyId, System.StringComparison.Ordinal))
                return string.Empty;
            return rt.PursueStackId ?? string.Empty;
        }
    }
}
