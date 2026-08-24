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
        public static bool IsFriendlyNodeForFormation(SimulationWorld world, string siteId, string factionId)
        {
            if (!world.Strategic.Sites.TryGet(siteId, out var site) || site == null)
                return false;
            if (ArmyFormationSitePolicy.IsFriendlySiteForFaction(site, factionId))
                return true;
            return ArmyFormationSitePolicy.HasFactionMemberAtSite(world, site, factionId);
        }

        public static bool TryValidateFriendlyNode(
            SimulationWorld world,
            string factionId,
            string siteId,
            out GameError error)
        {
            if (!world.Strategic.Sites.TryGet(siteId, out var site) || site == null)
            {
                error = new GameError(ErrorCode.NotFound, "WorldSite not found.", siteId);
                return false;
            }

            if (ArmyFormationSitePolicy.TryValidateFriendlySite(world, factionId, site, out error))
                return true;

            if (ArmyFormationSitePolicy.HasFactionMemberAtSite(world, site, factionId))
            {
                error = default;
                return true;
            }

            error = new GameError(
                ErrorCode.InvalidOperation,
                "Army operations require friendly WorldSite.",
                siteId + ";faction=" + factionId);
            return false;
        }
    }
}
