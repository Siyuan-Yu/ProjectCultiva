using XianXia.Core.Actions;
using XianXia.Core.Concealment;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Labor;
using XianXia.Core.Orders;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;

namespace XianXia.Core.Social
{
    /// <summary>
    /// Demo [49]§4.5: during Labor schedule block, not working, and near supervisor → anger +.
    /// </summary>
    public sealed class SupervisorAngerDriver
    {
        int _lastHour = -1;

        public void Tick(SimulationWorld world)
        {
            if (world == null)
                return;

            var hour = DayClock.FromWorldTick(world.Tick).HourOfDay;
            if (hour == _lastHour)
                return;
            _lastHour = hour;

            foreach (var entity in world.Entities.All)
            {
                if (!entity.TryGet<ScheduleComponent>(out var sched) ||
                    !world.TryGetSchedule(sched.DefinitionId, out var def) ||
                    !def.TryResolve(world.Tick, out var block) ||
                    block.Activity != ScheduleActivity.Labor)
                    continue;

                if (!entity.TryGet<DailyTaskComponent>(out _))
                    continue;

                var working = entity.TryGet<ActionStateComponent>(out var st) &&
                              st.HasActiveAction &&
                              world.ActiveActions.TryGetValue(st.ActiveActionId, out var action) &&
                              action is LaborAction;
                if (working)
                    continue;

                if (!ConcealmentExposureRules.IsNearSupervisor(world, entity.Id))
                    continue;

                world.SupervisorAnger.Add(1);
            }
        }
    }
}
