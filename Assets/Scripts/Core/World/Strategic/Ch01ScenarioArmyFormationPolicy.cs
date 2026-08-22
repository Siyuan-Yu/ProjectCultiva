using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase H：Ch01 / LevelTester 场景 Adapter。
    /// 保留 presence-based friendly node；通用 Domain 层已移除该规则。
    /// </summary>
    public static class Ch01ScenarioArmyFormationPolicy
    {
        public static bool IsFriendlyNodeForFormation(SimulationWorld world, string nodeId, string factionId)
        {
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
            if (IsFriendlyNodeForFormation(world, nodeId, factionId))
            {
                error = default;
                return true;
            }

            return ArmyFormationNodePolicy.TryValidateFriendlyNode(world, factionId, nodeId, out error);
        }
    }
}
