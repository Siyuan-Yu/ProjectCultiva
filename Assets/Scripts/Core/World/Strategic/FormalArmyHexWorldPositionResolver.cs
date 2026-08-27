using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>FormalArmy 连续世界位置 → 大地图坐标。</summary>
    public static class FormalArmyHexWorldPositionResolver
    {
        public static bool TryResolve(
            SimulationWorld world,
            FormalArmy army,
            out float worldX,
            out float worldY)
        {
            worldX = worldY = 0f;
            if (world?.HexWorld == null || army == null || !army.UsesHexStrategicPosition)
                return false;

            var motion = army.WorldMotion;
            if (motion.HasPosition)
            {
                worldX = motion.WorldPosition.X;
                worldY = motion.WorldPosition.Y;
                return world.HexWorld.Contains(motion.CurrentHex);
            }

            var hexSize = world.HexWorld.HexSize;
            if (army.State == FormalArmyState.Moving &&
                army.TryGetActiveStepHexes(out var from, out var to))
            {
                HexMath.ToWorldPosition(from, to, army.StepProgress, hexSize, out worldX, out worldY);
                return true;
            }

            HexMath.ToWorldPosition(army.CurrentHex, hexSize, out worldX, out worldY);
            return world.HexWorld.Contains(army.CurrentHex);
        }
    }
}
