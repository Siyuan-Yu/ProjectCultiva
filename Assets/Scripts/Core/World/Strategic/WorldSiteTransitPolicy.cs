using System;
using System.Collections.Generic;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Legacy FormalArmy WorldSite routing policy。
    /// Phase 5R-B7A 起 PlayerParty 不再消费本 Policy：PlayerParty 的 WorldSite footprint
    /// 与普通 Surface Hex 同 passability。FormalArmy 的既有战略行为留待其独立阶段处理。
    /// </summary>
    public static class WorldSiteTransitPolicy
    {
        /// <summary>
        /// 收集"不可作为非目标中转"的 footprint hex 集合：目标 Site 不在集合内，
        /// 其余所有非目标 WorldSite（普通城市 / 关隘 / 营地）全部 blocked。
        /// </summary>
        public static HashSet<HexCoord> BuildBlockedFootprintHexes(
            SimulationWorld world,
            string destinationSiteId)
        {
            var blocked = new HashSet<HexCoord>();
            if (world?.Strategic?.Sites == null)
                return blocked;

            foreach (var kv in world.Strategic.Sites.Sites)
            {
                var site = kv.Value;
                if (site == null)
                    continue;
                if (string.Equals(site.SiteId, destinationSiteId, StringComparison.Ordinal))
                    continue; // 目标 Site：允许到达其正式 ingress。
                foreach (var hex in site.EnumerateFootprintHexes())
                    blocked.Add(hex);
            }

            return blocked;
        }

    }
}
