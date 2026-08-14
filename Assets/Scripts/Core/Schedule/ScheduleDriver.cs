using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Npc;
using XianXia.Core.Orders;
using XianXia.Core.Simulation;
using XianXia.Core.Social;

namespace XianXia.Core.Schedule
{
    /// <summary>
    /// Injects Schedule Orders for NPCs when idle and no pending Player Orders.
    /// DirectControl Characters are never auto-driven (RTS：默认只听玩家；日后「自动行动」再开)。
    /// Player Orders always outrank Schedule. VS0.5-E: PersonalityScheduleBias on activity／duration.
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
                // Character = 玩家可直接控制单位：不跟课表自动走。
                if ((entity.Tags & EntityTag.Character) != 0)
                    continue;

                // NPCs with schedules use NpcActivityDriver (WorkArea resolve + Move／Work).
                if ((entity.Tags & EntityTag.Npc) != 0)
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

            var expectedType = ScheduleActivityMapping.ToOrderType(choice.Activity);
            if (queue.HasMatching(OrderSource.Schedule, expectedType))
                return;

            queue.RemoveWhere(o => o.Source == OrderSource.Schedule);

            var tickInDay = (int)(world.Tick.Value % (ulong)WorldTick.TicksPerDay);
            var remainingInBlock = (ulong)(block.EndTickInDay - tickInDay);
            var duration = choice.DurationTicks;
            if (remainingInBlock < duration)
                duration = remainingInBlock;
            if (duration == 0)
                return;

            var created = _factory.Create(
                loop.AllocateOrderId(),
                entity.Id,
                choice.Activity,
                duration);
            if (created.IsFailure)
                return;

            loop.EnqueueOrder(created.Value);
        }
    }
}
