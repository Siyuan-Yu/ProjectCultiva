using XianXia.Core.Simulation;

namespace XianXia.Core.World
{
    /// <summary>
    /// Current Continuous World movement-budget scale.
    /// </summary>
    public static class ContinuousWorldMovementScale
    {
        public static float Resolve(SimulationWorld world)
        {
            var value = world?.ContinuousWorldMovementScale ?? 1f;
            return value > 0f ? value : 1f;
        }
    }
}
