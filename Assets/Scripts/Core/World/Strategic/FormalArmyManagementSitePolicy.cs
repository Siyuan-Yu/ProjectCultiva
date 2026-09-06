using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Legacy／LevelTester Site adapter。正式玩家 Create／roster management 已改用
    /// FormalArmyManagementTerritoryPolicy；Garrison 仍直接使用 Site owner gate。
    /// </summary>
    public static class FormalArmyManagementSitePolicy
    {
        public static bool CanManageFormalArmyAtSite(
            SimulationWorld world,
            string siteId,
            string factionId) =>
            TryValidateManageSite(world, siteId, factionId, out _);

        public static bool CanManageFormalArmyAtSite(
            SimulationWorld world,
            WorldSite site,
            string factionId) =>
            TryValidateManageSite(world, site, factionId, out _);

        public static bool TryValidateManageSite(
            SimulationWorld world,
            string siteId,
            string factionId,
            out GameError error) =>
            ArmyFormationSitePolicy.TryValidateFriendlySiteForSiteId(world, factionId, siteId, out error);

        public static bool TryValidateManageSite(
            SimulationWorld world,
            WorldSite site,
            string factionId,
            out GameError error) =>
            ArmyFormationSitePolicy.TryValidateFriendlySite(world, factionId, site, out error);
    }
}
