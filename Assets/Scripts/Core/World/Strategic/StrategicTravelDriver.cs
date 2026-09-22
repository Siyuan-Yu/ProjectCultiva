using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Core travel tick boundary. SquadWorldMotion and background Surface travel advance here.
    /// PlayerParty SurfaceVisible movement is Host-driven.
    /// </summary>
    public static class StrategicTravelDriver
    {
        public static void AfterTravelTick(SimulationWorld world, int ticks = 1)
        {
            if (world?.Strategic == null || ticks < 1)
                return;

            SquadWorldMotionService.AdvanceAll(world, ticks);
            BackgroundSimulationScheduler.AfterSimulationTick(world, ticks);
        }
    }
}
