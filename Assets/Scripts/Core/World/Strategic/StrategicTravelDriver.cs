using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Core travel tick boundary. SquadWorldMotion and background Surface travel advance here.
    /// Modern PlayerParty SurfaceVisible movement is Host-driven. The final branch advances only
    /// an explicitly gated legacy World-mode Hex travel plan with no SurfaceId.
    /// </summary>
    public static class StrategicTravelDriver
    {
        public static void AfterTravelTick(SimulationWorld world, int ticks = 1)
        {
            if (world?.Strategic == null || ticks < 1)
                return;

            SquadWorldMotionService.AdvanceAll(world, ticks);
            BackgroundSimulationScheduler.AfterSimulationTick(world, ticks);

            if (!world.HexWorld.HasGrid)
                return;

            var motion = world.PlayerPartyTravel;
            if (motion != null && motion.IsMoving &&
                motion.ExecutionMode == PlayerPartyTravelExecutionMode.World &&
                motion.HexPathCount > 0 && string.IsNullOrEmpty(motion.SurfaceId))
                LegacyPlayerPartyHexTravelCompatibility.AdvanceAll(world, ticks);
        }
    }
}
