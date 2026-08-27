using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase H：Ch01 / LevelTester 场景 Adapter。
    /// Hex 模式下使用 WorldSite 足迹。
    /// </summary>
    public static class Ch01ScenarioArmyFormationPolicy
    {
        public static bool IsFriendlyNodeForFormation(SimulationWorld world, string siteId, string factionId) =>
            FormalArmyManagementSitePolicy.CanManageFormalArmyAtSite(world, siteId, factionId);

        public static bool TryValidateFriendlyNode(
            SimulationWorld world,
            string factionId,
            string siteId,
            out GameError error) =>
            FormalArmyManagementSitePolicy.TryValidateManageSite(world, siteId, factionId, out error);
    }
}
