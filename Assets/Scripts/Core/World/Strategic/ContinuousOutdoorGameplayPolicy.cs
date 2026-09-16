using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Small, single normal-vs-legacy gate for gameplay callers.  A normal continuous outdoor
    /// session is defined by registered Surface navigation, never by a map id or HexWorld state.
    /// </summary>
    public static class ContinuousOutdoorGameplayPolicy
    {
        public static bool IsNormalContinuousOutdoor(SimulationWorld world) =>
            world?.SurfaceGround != null && world.SurfaceGround.IsReady;
    }
}
