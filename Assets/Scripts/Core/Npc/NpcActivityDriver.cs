using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Orders;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;
using XianXia.Core.Social;

namespace XianXia.Core.Npc
{
    /// <summary>
    /// Job-bound NPCs: Schedule Block → ActivityResolver → Move／Work orders.
    /// Characters under direct control are skipped.
    /// </summary>
    public sealed class NpcActivityDriver
    {
        public void Drive(SimulationWorld world, SimulationLoop loop)
        {
            if (world == null || loop == null)
                return;

            foreach (var entity in world.Entities.All)
            {
                if ((entity.Tags & EntityTag.Character) != 0)
                    continue;
                if (!entity.TryGet<JobComponent>(out var job) || !job.HasJob)
                    continue;
                if (!entity.TryGet<ScheduleComponent>(out var binding) ||
                    string.IsNullOrEmpty(binding.DefinitionId))
                    continue;
                if (!world.TryGetSchedule(binding.DefinitionId, out var definition))
                    continue;

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

            if (!ActivityResolver.TryResolve(world, entity, choice.Activity, duration, out var resolved))
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
                activity: choice.Activity);
            loop.EnqueueOrder(order);
        }
    }
}
