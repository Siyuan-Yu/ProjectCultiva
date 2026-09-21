using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 旧 Multi-Hex WorldSite footprint 与正式 WorldLocation 之间的兼容查询。
    /// 这些 Hex footprint 规则不适用于 Continuous Surface 或 Actual Administrative Control。
    /// </summary>
    public static class LegacyWorldSiteHexLocationCompatibility
    {
        public static bool TryGetSiteAtHex(SimulationWorld world, HexCoord hex, out WorldSite site)
        {
            site = null;
            return world?.Strategic?.Sites != null &&
                   world.Strategic.Sites.TryGetAtLegacyHex(hex, out site) &&
                   site != null;
        }

        public static bool IsInsideSiteFootprint(WorldSite site, HexCoord hex) =>
            site != null && site.OccupiesLegacyHex(hex);

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

            if (site.OccupiesLegacyHex(previousHex) || !site.OccupiesLegacyHex(newHex))
                return false;

            return true;
        }
    }
}
