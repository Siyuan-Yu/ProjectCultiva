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

            if (!string.IsNullOrEmpty(army.NodeId) &&
                world.Strategic.Sites.TryGet(army.NodeId, out var site) &&
                site != null)
            {
                hex = site.AnchorHex;
                return world.HexWorld.Contains(hex);
            }

            return false;
        }
    }
}
