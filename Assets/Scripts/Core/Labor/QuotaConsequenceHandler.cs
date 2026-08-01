using XianXia.Core.Events;
using XianXia.Core.Simulation;

namespace XianXia.Core.Labor
{
    /// <summary>
    /// Phase D: on DayEnded, consume Deviation／shortfall → thin mark + event; then reset day counters.
    /// </summary>
    public sealed class QuotaConsequenceHandler : IDayBoundaryHandler
    {
        public void OnDayEnded(SimulationWorld world, ulong endedDayIndex)
        {
            if (world == null)
                return;

            foreach (var entity in world.Entities.All)
            {
                if (!entity.TryGet<DailyTaskComponent>(out var daily))
                    continue;

                var shortfall = daily.RequiredAmount > daily.CompletedAmount
                    ? daily.RequiredAmount - daily.CompletedAmount
                    : 0;
                var hasConsequence = daily.Deviation > 0 || shortfall > 0;

                if (hasConsequence)
                {
                    daily.PendingReprimand = true;
                    daily.LastSettledDeviation = daily.Deviation;
                    world.Events.Publish(
                        EventType.QuotaConsequenceApplied,
                        world.Tick,
                        actor: entity.Id,
                        target: entity.Id,
                        payload: "dayIndex=" + endedDayIndex +
                                 ";deviation=" + daily.Deviation +
                                 ";shortfall=" + shortfall +
                                 ";completed=" + daily.CompletedAmount +
                                 ";required=" + daily.RequiredAmount);
                }
                else
                {
                    daily.PendingReprimand = false;
                    daily.LastSettledDeviation = 0;
                }

                daily.CompletedAmount = 0;
                daily.Deviation = 0;
            }
        }

        public void OnDayStarted(SimulationWorld world, ulong startedDayIndex)
        {
            // Phase D: settlement is on DayEnded only.
        }
    }
}
