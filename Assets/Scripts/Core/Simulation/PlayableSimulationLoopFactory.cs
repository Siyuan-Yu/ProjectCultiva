using XianXia.Core.Content;
using XianXia.Core.Labor;
using XianXia.Core.Social;

namespace XianXia.Core.Simulation
{
    public static class PlayableSimulationLoopFactory
    {
        public static SimulationLoop Create(SimulationWorld world, bool enableSocialTick = false) =>
            new SimulationLoop(world, dayBoundaryHandlers: new IDayBoundaryHandler[]
            {
                new QuotaConsequenceHandler(),
                new ChapterDayHandler(),
                new QuestDeadlineDayHandler(),
                new SupervisorPressureHandler()
            }, enableSocialTick: enableSocialTick);
    }
}
