using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Multi-Hex WorldSite Footprint 与正式 WorldLocation 的 canonicalization 规则。
    /// Presentation 可在 Footprint 内连续移动；Authority 在真正跨 Boundary 前保持 AtWorldSite。
    /// </summary>
    public static class WorldSiteFootprintLocationAuthority
    {
        public static bool TryGetSiteAtHex(SimulationWorld world, HexCoord hex, out WorldSite site)
        {
            site = null;
            return world?.Strategic?.Sites != null &&
                   world.Strategic.Sites.TryGetAtHex(hex, out site) &&
                   site != null;
        }

        public static bool IsInsideSiteFootprint(WorldSite site, HexCoord hex) =>
            site != null && site.OccupiesHex(hex);

        public static bool TryDetectDestinationSiteIngress(
            SimulationWorld world,
            HexCoord previousHex,
            HexCoord newHex,
            string destinationSiteId,
            out WorldSite site)
        {
            site = null;
            if (world == null ||
                string.IsNullOrEmpty(destinationSiteId) ||
                previousHex.Equals(newHex))
                return false;

            if (!world.Strategic.Sites.TryGet(destinationSiteId, out site) || site == null)
                return false;

            if (site.OccupiesHex(previousHex) || !site.OccupiesHex(newHex))
                return false;

            return true;
        }
    }

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
            if (world?.HexWorld == null || world.Strategic?.Sites == null)
                return false;
            var size = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var hex = HexMath.WorldToHex(worldPosition.X, worldPosition.Y, size);
            if (!world.Strategic.Sites.TryGetAtHex(hex, out site) || site == null ||
                !WorldSiteOutdoorMigrationPolicy.UsesContinuousOutdoorSurface(site))
            {
                site = null;
                return false;
            }
            return true;
        }

        public static string ResolveSiteIdOrEmpty(
            SimulationWorld world,
            WorldVec2 worldPosition) =>
            TryResolve(world, worldPosition, out var site) ? site.SiteId : string.Empty;
    }

    /// <summary>Single compatibility gate for the Outdoor WorldSite migration.  It deliberately
    /// lives beside WorldSite rather than presentation code: callers must not infer the physical
    /// mode from LocalMapId, because that id remains a legacy authoring source.</summary>
    public static class WorldSiteOutdoorMigrationPolicy
    {
        public static bool UsesContinuousOutdoorSurface(WorldSite site) =>
            site != null && site.UsesContinuousOutdoorSurface;
    }
}
