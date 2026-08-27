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
}
