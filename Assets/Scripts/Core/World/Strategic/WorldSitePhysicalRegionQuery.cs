using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Continuous Outdoor WorldSite context query.</summary>
    public static class WorldSitePhysicalRegionQuery
    {
        public static bool TryResolve(
            SimulationWorld world,
            WorldVec2 worldPosition,
            out WorldSite site)
        {
            site = null;
            if (world?.Strategic?.Sites == null)
                return false;
            return WorldSiteAdministrativeControlResolver.TryResolveOnRegisteredSurface(
                       world, worldPosition.X, worldPosition.Y, out _, out site, out _) &&
                   site != null;
        }

        public static string ResolveSiteIdOrEmpty(
            SimulationWorld world,
            WorldVec2 worldPosition) =>
            TryResolve(world, worldPosition, out var site) ? site.SiteId : string.Empty;
    }

    /// <summary>Single compatibility gate for the Outdoor WorldSite migration. It deliberately
    /// lives beside WorldSite rather than presentation code: callers must not infer the physical
    /// mode from LocalMapId, because that id remains a legacy authoring source.</summary>
    public static class WorldSiteOutdoorMigrationPolicy
    {
        public static bool UsesContinuousOutdoorSurface(WorldSite site) =>
            site != null && site.UsesContinuousOutdoorSurface;
    }
}
