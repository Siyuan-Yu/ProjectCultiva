using System;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase H：地点防御力统计（Pure Hex SiteId）。
    /// 完整攻防公式 DEFER；Capture 门禁已接 WarGate。
    /// </summary>
    public static class SiteDefenseService
    {
        public static int CountResidents(SimulationWorld world, string siteId)
        {
            if (world?.WorldPresence == null || string.IsNullOrEmpty(siteId))
                return 0;

            return StrategicSiteAccessService.CountPartyMembersAtSite(world, siteId);
        }

        public static int CountGarrisonedArmies(SimulationWorld world, string siteId, string ownerFactionId)
        {
            if (world?.Strategic?.FormalArmies == null || string.IsNullOrEmpty(siteId))
                return 0;

            if (!world.Strategic.Sites.TryGet(siteId, out var site) || site == null)
                return 0;

            return CountGarrisonedArmiesAtSite(world, site, ownerFactionId);
        }

        static int CountGarrisonedArmiesAtSite(
            SimulationWorld world,
            WorldSite site,
            string ownerFactionId)
        {
            var count = 0;
            foreach (var kv in world.Strategic.FormalArmies.Armies)
            {
                var army = kv.Value;
                if (army == null || !army.UsesHexStrategicPosition)
                    continue;
                if (!site.OccupiesHex(army.CurrentHex))
                    continue;
                if (army.State != FormalArmyState.Idle &&
                    army.State != FormalArmyState.Moving &&
                    army.State != FormalArmyState.Garrisoned)
                    continue;
                if (!string.IsNullOrEmpty(ownerFactionId) &&
                    !string.Equals(army.FactionId, ownerFactionId, StringComparison.Ordinal))
                    continue;
                count++;
            }

            return count;
        }

        public static int EstimateDefenseStrength(
            SimulationWorld world,
            string siteId,
            string ownerFactionId)
        {
            var residents = CountResidents(world, siteId);
            var garrisons = CountGarrisonedArmies(world, siteId, ownerFactionId);
            return residents + garrisons * SiteDefenseConfig.GarrisonWeightPlaceholder;
        }
    }

    /// <summary>占位配置：完整 Site Defense 公式 DEFER。</summary>
    public static class SiteDefenseConfig
    {
        public static int GarrisonWeightPlaceholder { get; set; } = 4;
    }
}
