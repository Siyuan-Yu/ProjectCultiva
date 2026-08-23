using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>FormalArmy Hex 战略位置 → 大地图世界坐标（唯一真源）。</summary>
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
