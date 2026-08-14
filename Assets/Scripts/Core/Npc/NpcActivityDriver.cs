using System.Collections.Generic;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Orders;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;
using XianXia.Core.Social;

namespace XianXia.Core.Npc
{
    /// <summary>
    /// Scheduled NPCs: activity → WorkArea (Move／Work). Not profession-bound.
    /// If current activity has no available place, falls back by activityPriorities.
    /// </summary>
    public sealed class NpcActivityDriver
    {
        readonly List<(ScheduleActivity Activity, int Priority)> _priorityScratch =
            new List<(ScheduleActivity, int)>(8);

        public void Drive(SimulationWorld world, SimulationLoop loop)
        {
            if (world == null || loop == null)
                return;

            foreach (var entity in world.Entities.All)
            {
                if ((entity.Tags & EntityTag.Character) != 0)
                    continue;
                if (!entity.TryGet<ScheduleComponent>(out var binding) ||
                    string.IsNullOrEmpty(binding.DefinitionId))
                    continue;
                if (!world.TryGetSchedule(binding.DefinitionId, out var definition))
                    continue;

                if (!entity.TryGet<JobComponent>(out _))
                    entity.AddComponent(new JobComponent());

                DriveEntity(world, loop, entity, definition);
            }
        }

        void DriveEntity(
            SimulationWorld world,
            SimulationLoop loop,
            Entity entity,
            ScheduleDefinition definition)
        {
            if (!entity.TryGet<ActionStateComponent>(out var actionState))
                return;
            if (actionState.HasActiveAction)
                return;

            var queue = world.GetOrCreateOrderQueue(entity.Id);
            if (queue.HasSource(OrderSource.Player))
                return;

            if (!definition.TryResolve(world.Tick, out var block))
            {
                queue.RemoveWhere(o => o.Source == OrderSource.Schedule);
                return;
            }

            entity.TryGet<PersonalityProfileComponent>(out var profile);
            var choice = PersonalityScheduleBias.Apply(block, profile);

            var tickInDay = (int)(world.Tick.Value % (ulong)WorldTick.TicksPerDay);
            var remainingInBlock = (ulong)(block.EndTickInDay - tickInDay);
            var duration = choice.DurationTicks;
            if (remainingInBlock < duration)
                duration = remainingInBlock;
            if (duration == 0)
                return;

            if (!TryResolveWithFallback(world, entity, choice.Activity, duration, out var resolved))
                return;

            var orderType = resolved.NeedsMove ? OrderType.Move : OrderType.Work;
            if (queue.HasMatching(OrderSource.Schedule, orderType, resolved.WorkAreaId))
                return;

            queue.RemoveWhere(o => o.Source == OrderSource.Schedule);

            var moveDuration = resolved.NeedsMove
                ? (duration < 24UL ? duration : 24UL)
                : duration;
            var wait = resolved.NeedsMove ? moveDuration : duration;

            var order = new Order(
                loop.AllocateOrderId(),
                entity.Id,
                orderType,
                OrderSource.Schedule,
                waitTicks: wait,
                targetRef: resolved.WorkAreaId,
                activity: resolved.Activity);
            loop.EnqueueOrder(order);
        }

        bool TryResolveWithFallback(
            SimulationWorld world,
            Entity entity,
            ScheduleActivity primary,
            ulong duration,
            out ResolvedActivity resolved)
        {
            if (ActivityResolver.TryResolve(world, entity, primary, duration, out resolved))
                return true;

            if (!entity.TryGet<ActivityTendencyComponent>(out var tendency))
                return false;

            tendency.CopyPrioritiesTo(_priorityScratch);
            for (var i = 0; i < _priorityScratch.Count; i++)
            {
                var next = _priorityScratch[i].Activity;
                if (next == primary)
                    continue;
                if (ActivityResolver.TryResolve(world, entity, next, duration, out resolved))
                    return true;
            }

            resolved = null;
            return false;
        }
    }
}
