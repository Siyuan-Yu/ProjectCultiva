using XianXia.Core.Simulation;

namespace XianXia.Core.World
{
    /// <summary>
    /// Read-only scale adapter for movement budgets that still preserve the legacy Hex metric.
    /// It does not make the legacy grid a gameplay authority.
    /// </summary>
    public static class ContinuousWorldMovementScale
    {
        public static float Resolve(SimulationWorld world)
        {
            var value = world.LegacyHexWorld.HexSize;
            return value > 0f ? value : 1f;
        }
    }
}
