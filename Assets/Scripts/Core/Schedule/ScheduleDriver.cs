using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Orders;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Schedule
{
    /// <summary>
    /// Injects Schedule Orders when entity is idle and has no pending Player Orders.
    /// Does not implement NPC AI. Player Orders always outrank Schedule.
    /// </summary>
    public sealed class ScheduleDriver
    {
        readonly ScheduleOrderFactory _factory;

        public ScheduleDriver(ScheduleOrderFactory factory = null)
        {
            _factory = factory ?? new ScheduleOrderFactory();
        }

        public void Drive(SimulationWorld world, SimulationLoop loop)
        {
            if (world == null || loop == null)
                return;

            foreach (var entity in world.Entities.All)
            {
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

            var expectedType = block.Activity == ScheduleActivity.Labor ? OrderType.Labor : OrderType.Rest;
            if (queue.HasMatching(OrderSource.Schedule, expectedType))
                return;

            queue.RemoveWhere(o => o.Source == OrderSource.Schedule);

            var tickInDay = (int)(world.Tick.Value % (ulong)WorldTick.TicksPerDay);
            var remainingInBlock = (ulong)(block.EndTickInDay - tickInDay);
            var duration = block.OrderDurationTicks;
            if (remainingInBlock < duration)
                duration = remainingInBlock;
            if (duration == 0)
                return;

            var created = _factory.Create(loop.AllocateOrderId(), entity.Id, block, duration);
            if (created.IsFailure)
                return;

            loop.EnqueueOrder(created.Value);
        }
    }
}
