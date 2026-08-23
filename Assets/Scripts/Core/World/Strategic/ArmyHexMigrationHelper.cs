using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>将 FormalArmy 从 legacy Node 引用迁移到 Hex 位置（会话引导）。</summary>
    public static class ArmyHexMigrationHelper
    {
        public static void MigrateFormalArmies(SimulationWorld world)
        {
            if (world?.Strategic?.FormalArmies == null || !world.HexWorld.HasGrid)
                return;

            foreach (var kv in world.Strategic.FormalArmies.Armies)
            {
                var army = kv.Value;
                if (army == null || army.UsesHexStrategicPosition)
                    continue;

                if (TryResolveHexForArmy(world, army, out var hex))
                    ArmyHexTravelService.InitializeArmyAtHex(army, hex);
            }
        }

        public static bool TryResolveHexForArmy(SimulationWorld world, FormalArmy army, out HexCoord hex)
        {
            hex = default;
            if (army == null)
                return false;

            foreach (var siteKv in world.Strategic.Sites.Sites)
            {
                var site = siteKv.Value;
                if (site == null || string.IsNullOrEmpty(site.LegacyNodeId))
                    continue;
                if (!string.Equals(site.LegacyNodeId, army.NodeId, System.StringComparison.Ordinal))
                    continue;
                hex = site.AnchorHex;
                return true;
            }

            if (world.WorldGraph.TryGetNode(army.NodeId, out var node) && node != null && node.HasHexCoord)
            {
                hex = new HexCoord(node.HexQ, node.HexR);
                return world.HexWorld.Contains(hex);
            }

            return false;
        }
    }
}
