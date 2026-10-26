using System;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Storage use, not Site management: direct vassals share their overlord's stock network.</summary>
    public static class WorldSiteStorageAccessPolicy
    {
        public static bool CanFactionUse(SimulationWorld world, string actingFactionId, WorldSite site)
        {
            if (world == null || site == null || !site.IsCoreActive ||
                string.IsNullOrEmpty(actingFactionId) || string.IsNullOrEmpty(site.OwnerFactionId))
                return false;
            if (string.Equals(actingFactionId, site.OwnerFactionId, StringComparison.Ordinal))
                return true;
            if (world.HuangcunSurvival.Enabled && HuangcunOpeningAccess.IsLocked(world) &&
                actingFactionId == world.Strategic.PlayerFactionId && site.SiteId == Ch01ScenarioProgressionHooks.HuangcunSiteId)
                return false; // Rations are issued explicitly, not unlimited access to the overlord's store.
            return FactionDiplomacyRelationQuery.GetRelation(world, actingFactionId,
                       site.OwnerFactionId) == FactionDiplomacyRelation.Overlord;
        }
    }
}
