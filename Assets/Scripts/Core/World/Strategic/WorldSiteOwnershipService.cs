using System;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Pure Hex 政治归属真源：<see cref="WorldSite.OwnerFactionId"/> 读写与 LocalMap 会话解析。</summary>
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
        }

        public static bool TryResolveSiteForLocalMapSession(
            SimulationWorld world,
            string localMapId,
            out WorldSite site)
        {
            site = null;
            if (world?.Strategic?.Sites == null || string.IsNullOrEmpty(localMapId))
                return false;

            var partySiteId = world.PartyWorld?.SiteId;
            if (!string.IsNullOrEmpty(partySiteId) &&
                world.Strategic.Sites.TryGet(partySiteId, out site) &&
                site != null &&
                string.Equals(site.LocalMapId, localMapId, StringComparison.Ordinal))
                return true;

            foreach (var kv in world.Strategic.Sites.Sites)
            {
                var candidate = kv.Value;
                if (candidate == null ||
                    !string.Equals(candidate.LocalMapId, localMapId, StringComparison.Ordinal))
                    continue;
                site = candidate;
                return true;
            }

            return false;
        }
    }
}
