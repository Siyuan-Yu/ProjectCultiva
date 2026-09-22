using XianXia.Core.World.Strategic;

namespace XianXia.Core.World
{
    /// <summary>Neutral WorldSite metadata helpers.</summary>
    public static class WorldTravelService
    {
        public static string ResolveWorldSiteLocalMapId(WorldSite site)
        {
            if (site == null || string.IsNullOrWhiteSpace(site.LocalMapId))
                return string.Empty;
            return site.LocalMapId.Trim();
        }

    }
}
