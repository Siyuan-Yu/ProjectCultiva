using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Events;
using XianXia.Core.Labor;
using XianXia.Core.Simulation;

namespace XianXia.Core.Social
{
    /// <summary>
    /// Reference：主管日终压力 — 若有配额偏差／训斥标记则写 Story Flag 并尝试触发内容事件。
    /// </summary>
    public sealed class SupervisorPressureHandler : IDayBoundaryHandler
    {
        public const string PressureFlag = "story:supervisor_pressure";
        public const string PressureEventId = "base:event_ch01_ref_supervisor_pressure";

        readonly ContentEventService _events = new ContentEventService();

        public void OnDayStarted(SimulationWorld world, ulong startedDayIndex)
        {
        }

        public void OnDayEnded(SimulationWorld world, ulong endedDayIndex)
        {
            if (world == null)
                return;

            var pressure = false;
            EntityId subject = EntityId.None;
            foreach (var entity in world.Entities.All)
            {
                if ((entity.Tags & Entities.EntityTag.Npc) != 0)
                    continue;
                if (subject.IsNone)
                    subject = entity.Id;
                if (!entity.TryGet<DailyTaskComponent>(out var daily))
                    continue;
                if (daily.PendingReprimand || daily.LastSettledDeviation > 0 ||
                    daily.CompletedAmount < daily.RequiredAmount)
                {
                    pressure = true;
                    break;
                }
            }

            if (!pressure)
                return;

            StoryFlagService.Set(world, PressureFlag, subject);
            world.Events.Publish(
                EventType.QuotaConsequenceApplied,
                world.Tick,
                target: subject,
                payload: "supervisor_pressure;day=" + endedDayIndex);

            if (!subject.IsNone)
                _events.TryPresentById(world, subject, PressureEventId, force: false);
        }
    }
}
