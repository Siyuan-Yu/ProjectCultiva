using System.Collections.Generic;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Territory 加载后 invariant 校验（2J §6 / §14）：
    /// ① Site↔Region 双向绑定；② Owner==Controller；③ Region hex 存在、无重复、无跨 Region overlap；
    /// ④ Site 全部 footprint hex ⊆ 自身 Region。返回问题文本；不静默猜测谁覆盖谁。
    /// </summary>
    public static class TerritoryInvariantValidator
    {
        public static List<string> Validate(SimulationWorld world)
        {
            var errors = new List<string>();
            if (world?.Strategic?.Sites == null || world?.Strategic?.TerritoryRegions == null)
                return errors;

            var sites = world.Strategic.Sites;
            var regions = world.Strategic.TerritoryRegions;

            // ① Site → Region 绑定
            foreach (var kv in sites.Sites)
            {
                var site = kv.Value;
                if (site == null || string.IsNullOrEmpty(site.SiteId))
                    continue;
                if (string.IsNullOrEmpty(site.TerritoryRegionId))
                    continue;

                if (!regions.TryGet(site.TerritoryRegionId, out var region) || region == null)
                {
                    errors.Add("WorldSite '" + site.SiteId + "' TerritoryRegionId '" +
                               site.TerritoryRegionId + "' missing.");
                    continue;
                }

                if (!string.Equals(region.PrimaryWorldSiteId, site.SiteId, System.StringComparison.Ordinal))
                    errors.Add("Region '" + region.RegionId + "' PrimaryWorldSiteId='" +
                               region.PrimaryWorldSiteId + "' != WorldSite '" + site.SiteId + "'.");

                // ② Owner == Controller
                var siteOwner = site.OwnerFactionId ?? string.Empty;
                var controller = region.ControlFactionId ?? string.Empty;
                if (!string.Equals(siteOwner, controller, System.StringComparison.Ordinal))
                    errors.Add("Owner invariant broken for WorldSite '" + site.SiteId +
                               "': OwnerFactionId='" + siteOwner + "' != Region.ControlFactionId='" +
                               controller + "'.");
            }

            // ③ Region hex 合法 + 无重复 + 无跨 Region overlap
            var hexToRegion = new Dictionary<HexCoord, string>();
            foreach (var regionKv in regions.Regions)
            {
                var region = regionKv.Value;
                if (region == null)
                    continue;
                var seen = new HashSet<HexCoord>();
                for (var i = 0; i < region.Hexes.Count; i++)
                {
                    var hex = region.Hexes[i];
                    if (!world.HexWorld.IsInBounds(hex))
                    {
                        errors.Add("Region '" + region.RegionId + "' hex out of bounds: " + hex + ".");
                        continue;
                    }
                    if (!seen.Add(hex))
                    {
                        errors.Add("Region '" + region.RegionId + "' duplicate hex: " + hex + ".");
                        continue;
                    }
                    if (hexToRegion.TryGetValue(hex, out var other) &&
                        !string.Equals(other, region.RegionId, System.StringComparison.Ordinal))
                        errors.Add("Hex " + hex + " belongs to both Region '" + other +
                                   "' and '" + region.RegionId + "'.");
                    else
                        hexToRegion[hex] = region.RegionId;
                }
            }

            // ④ Footprint ⊆ 自身 Region
            foreach (var kv in sites.Sites)
            {
                var site = kv.Value;
                if (site == null || string.IsNullOrEmpty(site.TerritoryRegionId))
                    continue;
                if (!regions.TryGet(site.TerritoryRegionId, out var region) || region == null)
                    continue;
                foreach (var hex in site.EnumerateFootprintHexes())
                {
                    if (!region.Contains(hex))
                        errors.Add("WorldSite '" + site.SiteId + "' footprint hex " + hex +
                                   " not inside its TerritoryRegion '" + region.RegionId + "'.");
                }
            }

            return errors;
        }
    }
}
