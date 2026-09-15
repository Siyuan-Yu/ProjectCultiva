using System;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary><see cref="WorldSite.OwnerFactionId"/> 的政治 ownership 读写入口。</summary>
    public static class WorldSiteOwnershipService
    {
        public static string GetOwner(SimulationWorld world, string siteId)
        {
            if (world?.Strategic?.Sites == null || string.IsNullOrEmpty(siteId))
                return string.Empty;
            if (!world.Strategic.Sites.TryGet(siteId, out var site) || site == null)
                return string.Empty;
            return site.OwnerFactionId ?? string.Empty;
        }

        public static void SetOwner(SimulationWorld world, string siteId, string factionId)
        {
            if (world?.Strategic?.Sites == null || string.IsNullOrEmpty(siteId))
                return;
            if (!world.Strategic.Sites.TryGet(siteId, out var site) || site == null)
                return;
            site.OwnerFactionId = factionId ?? string.Empty;
            if (!string.IsNullOrEmpty(site.CoreAssetId) &&
                world.Strategic.FactionFlags.Flags.TryGetValue(site.CoreAssetId, out var flag) && flag != null)
                flag.FactionId = site.OwnerFactionId;
        }
    }
}
