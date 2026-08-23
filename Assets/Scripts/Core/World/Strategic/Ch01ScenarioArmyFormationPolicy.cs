using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase H：Ch01 / LevelTester 场景 Adapter。
    /// Hex 模式下使用 WorldSite 足迹；legacy Node 规则仅作无 Hex 地图时的测试后备。
    /// </summary>
    public static class Ch01ScenarioArmyFormationPolicy
    {
        public static bool IsFriendlyNodeForFormation(SimulationWorld world, string nodeId, string factionId)
        {
            if (HexStrategicRuntime.IsActive(world))
            {
                if (ArmyFormationSitePolicy.TryGetSiteForLegacyNode(world, nodeId, out var site) &&
                    site != null &&
                    ArmyFormationSitePolicy.IsFriendlySiteForFaction(site, factionId))
                    return true;
                if (ArmyFormationSitePolicy.TryGetSiteForLegacyNode(world, nodeId, out site) &&
                    site != null &&
                    ArmyFormationSitePolicy.HasFactionMemberAtSite(world, site, factionId))
                    return true;
                return false;
            }

            if (ArmyFormationNodePolicy.IsFriendlyNodeForFaction(world, nodeId, factionId))
                return true;
            return ArmyFormationNodePolicy.HasFactionMemberAtNode(world, nodeId, factionId);
        }

        public static bool TryValidateFriendlyNode(
            SimulationWorld world,
            string factionId,
            string nodeId,
            out GameError error)
        {
            if (HexStrategicRuntime.IsActive(world))
            {
                if (ArmyFormationSitePolicy.TryGetSiteForLegacyNode(world, nodeId, out var site) &&
                    site != null &&
                    ArmyFormationSitePolicy.TryValidateFriendlySite(world, factionId, site, out error))
                    return true;
                if (ArmyFormationSitePolicy.TryGetSiteForLegacyNode(world, nodeId, out site) &&
                    site != null &&
                    ArmyFormationSitePolicy.HasFactionMemberAtSite(world, site, factionId))
                {
                    error = default;
                    return true;
                }

                error = new GameError(
                    ErrorCode.InvalidOperation,
                    "Army operations require friendly WorldSite.",
                    nodeId + ";faction=" + factionId);
                return false;
            }

            if (IsFriendlyNodeForFormation(world, nodeId, factionId))
            {
                error = default;
                return true;
            }

            return ArmyFormationNodePolicy.TryValidateFriendlyNode(world, factionId, nodeId, out error);
        }
    }
}
