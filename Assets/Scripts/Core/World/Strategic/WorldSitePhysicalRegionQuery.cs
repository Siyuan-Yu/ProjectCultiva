using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>V1 Outdoor WorldSite physical-region query. The current baked region is the
    /// authored strategic footprint; it provides context only and never changes location authority.</summary>
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
            // MAP-03 normal authority: registered continuous Site geometry/control wins. The
            // Hex footprint lookup below is intentionally legacy-only fallback.
            if (ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world))
                return WorldSiteAdministrativeControlResolver.TryResolveOnRegisteredSurface(
                           world, worldPosition.X, worldPosition.Y, out _, out site, out _) &&
                       site != null;
            if (world.LegacyHexWorld == null)
                return false;
            var size = world.LegacyHexWorld.HexSize > 0f ? world.LegacyHexWorld.HexSize : 1f;
            var hex = HexMath.WorldToHex(worldPosition.X, worldPosition.Y, size);
            var siteIds = world.Strategic.Sites.GetSiteIdsAtLegacyHex(hex);
            for (var i = 0; i < siteIds.Count; i++)
            {
                if (!world.Strategic.Sites.TryGet(siteIds[i], out var candidate) || candidate == null ||
                    candidate.IsRuntimeCreated ||
                    !WorldSiteOutdoorMigrationPolicy.UsesContinuousOutdoorSurface(candidate))
                    continue;
                site = candidate;
                return true;
            }
            return false;
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
