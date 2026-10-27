using XianXia.Core.Simulation;

namespace XianXia.Core.World
{
    /// <summary>
    /// Current Continuous World movement-budget scale.
    /// </summary>
    public static class ContinuousWorldMovementScale
    {
        public static float MovementMultiplier(SimulationWorld world, bool airborne) =>
            (world?.Strategic?.SpatialRules?.WalkSpeedMultiplier ?? 1f) *
            (airborne ? world?.Strategic?.SpatialRules?.FlightSpeedMultiplier ?? 2f : 1f);

        public static float CharacterMultiplier(SimulationWorld world, Domain.Ids.EntityId id) =>
            MovementMultiplier(world, Combat.FlightService.IsAirborne(world, id));

        public static float Resolve(SimulationWorld world)
        {
            var value = world?.ContinuousWorldMovementScale ?? 1f;
            return value > 0f ? value : 1f;
        }
    }
}
