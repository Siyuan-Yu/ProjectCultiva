using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    public static class ArmyStackService
    {
        public static void AdvanceAll(SimulationWorld world, int ticks)
        {
            if (world?.Strategic == null || ticks < 1)
                return;

            foreach (var kv in world.Strategic.Armies.Stacks)
            {
                var stack = kv.Value;
                if (stack == null || !stack.IsTraveling)
                    continue;
                stack.RemainingTravelTicks -= ticks;
                if (stack.RemainingTravelTicks > 0)
                    continue;

                if (!string.IsNullOrEmpty(stack.DestNodeId))
                    stack.NodeId = stack.DestNodeId;
                stack.ClearTravel();
            }
        }
    }
}
