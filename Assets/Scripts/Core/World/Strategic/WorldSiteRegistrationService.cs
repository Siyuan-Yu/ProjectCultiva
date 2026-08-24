using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>WorldSite 注册与 Hex 格标注。</summary>
    public static class WorldSiteRegistrationService
    {
        public static void RegisterSiteOnGrid(SimulationWorld world, WorldSite site)
        {
            if (world == null || site == null)
                return;

            if (site.OccupiedHexes.Count == 0 && !site.AnchorHex.Equals(default))
                site.SetFootprint(new[] { site.AnchorHex });

            world.Strategic.Sites.Register(site);
            foreach (var hex in site.EnumerateFootprintHexes())
            {
                var tile = world.HexWorld.GetOrCreate(hex);
                tile.WorldSiteId = site.SiteId;
            }
        }
    }
}
