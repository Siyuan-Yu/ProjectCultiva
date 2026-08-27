using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// FormalArmy 管理操作（Create / Add / Remove / Disband / Leader）共用的 Site 判定。
    /// Player-controlled = WorldSite.OwnerFactionId 与军团 faction 一致；presence 不算 friendly。
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
