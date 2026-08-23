using System;
using System.Collections.Generic;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>WorldSite 注册与 Hex 格标注；不负责 Node/Route 拓扑。</summary>
    public static class WorldSiteRegistrationService
    {
        public static WorldSite CreateSiteFromNode(WorldNodeState node, HexCoord anchorHex)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            return new WorldSite
            {
                SiteId = ResolveSiteId(node),
                DisplayName = string.IsNullOrEmpty(node.Name) ? node.Id : node.Name,
                SiteType = node.Kind ?? string.Empty,
                AnchorHex = anchorHex,
                OwnerFactionId = node.OwnerId ?? string.Empty,
                LocalMapId = node.LocalMapId ?? string.Empty,
                LegacyNodeId = node.Id ?? string.Empty,
            };
        }

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

        public static void LinkLegacyNodeToHex(WorldNodeState node, HexCoord hexCoord)
        {
            if (node == null)
                return;
            node.HexQ = hexCoord.Q;
            node.HexR = hexCoord.R;
        }

        static string ResolveSiteId(WorldNodeState node)
        {
            if (!string.IsNullOrEmpty(node.Id) && node.Id.StartsWith("base:node_", StringComparison.Ordinal))
                return "base:site_" + node.Id.Substring("base:node_".Length);
            return string.IsNullOrEmpty(node.Id) ? Guid.NewGuid().ToString("N") : node.Id;
        }
    }
}
