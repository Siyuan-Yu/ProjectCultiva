using System;
using System.Collections.Generic;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase 5D: WorldSite 战略中转策略 —— PlayerParty / FormalArmy 共享的唯一真源。
    /// 规则：
    ///  - 目标 Site（destinationSiteId）：footprint / ingress 可进入，不阻塞。
    ///  - 非目标 Site：仅当【TransitMode == Gateway】且【被本次 Route 显式选入
    ///    allowedTransitSiteIds】才允许作为战略中间节点；普通 Site 即使被调用方
    ///    错误塞入 allowedTransitSiteIds，也必须保持 blocked（白名单不能升格普通 Site）。
    ///  - 其它 Gateway 仍 blocked（未选中 = 不可作非目标中转）。
    /// 5D-A 阶段尚无 Gateway route selection —— 正常 Travel 应传空列表，
    /// 保证与 5C 封板行为一致（所有非目标 Site 均 blocked）。
    /// </summary>
    public static class WorldSiteTransitPolicy
    {
        public static bool IsGateway(WorldSite site) =>
            site != null && site.TransitMode == WorldSiteTransitMode.Gateway;

        /// <summary>
        /// 收集“不可作为非目标中转”的 footprint hex 集合。
        /// 目标 Site 与本次 Route 显式允许的 Transit Site（allowedTransitSiteIds）不在集合内。
        /// allowedTransitSiteIds 为 null/空 = 除目标外全部 blocked（5D-A 默认）。
        /// </summary>
        public static HashSet<HexCoord> BuildBlockedFootprintHexes(
            SimulationWorld world,
            string destinationSiteId,
            IReadOnlyCollection<string> allowedTransitSiteIds)
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
                if (IsGateway(site) &&
                    IsExplicitlyAllowed(site.SiteId, allowedTransitSiteIds))
                    continue; // 显式选中的 Mandatory Gateway（非 Gateway 即使在白名单也 blocked）。
                foreach (var hex in site.EnumerateFootprintHexes())
                    blocked.Add(hex);
            }

            return blocked;
        }

        /// <summary>
        /// 该 Site 是否可作为本次旅行的战略中转：
        ///  - 是目标 Site → 允许（到达 ingress）；
        ///  - 否则仅当 TransitMode == Gateway 且被显式选入 allowedTransitSiteIds → 允许；
        ///  - 普通非目标 Site 即使被塞入白名单 → 不允许（白名单不升格）。
        /// </summary>
        public static bool IsTransitAllowed(
            WorldSite site,
            string destinationSiteId,
            IReadOnlyCollection<string> allowedTransitSiteIds) =>
            site != null &&
            (string.Equals(site.SiteId, destinationSiteId, StringComparison.Ordinal) ||
             (IsGateway(site) &&
              IsExplicitlyAllowed(site.SiteId, allowedTransitSiteIds)));

        static bool IsExplicitlyAllowed(
            string siteId,
            IReadOnlyCollection<string> allowedTransitSiteIds)
        {
            if (allowedTransitSiteIds == null || allowedTransitSiteIds.Count == 0)
                return false;
            foreach (var allowed in allowedTransitSiteIds)
            {
                if (!string.IsNullOrEmpty(allowed) &&
                    string.Equals(siteId, allowed, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
