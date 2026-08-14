using System.Collections.Generic;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Orders;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;
using XianXia.Core.Social;

namespace XianXia.Core.Npc
{
    /// <summary>
    /// Scheduled NPCs: activity → WorkArea (Move／Work). Not profession-bound.
    /// Full areas fall back by activityPriorities; last resort Idle (发呆).
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

            // Soft slot claim before enqueue so peers see occupied capacity this tick.
            if (!TryClaimResolvedSlot(world, entity, ref resolved))
            {
                if (resolved.Activity != ScheduleActivity.Idle &&
                    ActivityResolver.TryResolve(world, entity, ScheduleActivity.Idle, duration, out resolved) &&
                    TryClaimResolvedSlot(world, entity, ref resolved))
                {
                    // claimed idle yard
                }
                else
                {
                    ForceInPlaceIdle(entity, duration, out resolved);
                }
            }

            OrderType orderType;
            if (resolved.Activity == ScheduleActivity.Idle && string.IsNullOrEmpty(resolved.WorkAreaId))
                orderType = OrderType.Wait;
            else if (resolved.NeedsMove && !string.IsNullOrEmpty(resolved.WorkAreaId))
                orderType = OrderType.Move;
            else if (!string.IsNullOrEmpty(resolved.WorkAreaId))
                orderType = OrderType.Work;
            else
                orderType = OrderType.Wait;

            if (queue.HasMatching(OrderSource.Schedule, orderType, resolved.WorkAreaId ?? string.Empty))
                return;

            queue.RemoveWhere(o => o.Source == OrderSource.Schedule);

            var moveDuration = resolved.NeedsMove
                ? (duration < 24UL ? duration : 24UL)
                : duration;
            var wait = (orderType == OrderType.Move) ? moveDuration : duration;
            if (wait == 0)
                wait = 1;

            var order = new Order(
                loop.AllocateOrderId(),
                entity.Id,
                orderType,
                OrderSource.Schedule,
                waitTicks: wait,
                targetRef: resolved.WorkAreaId,
                activity: resolved.Activity,
                slotIndex: resolved.SlotIndex);
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

            if (entity.TryGet<ActivityTendencyComponent>(out var tendency))
            {
                tendency.CopyPrioritiesTo(_priorityScratch);
                for (var i = 0; i < _priorityScratch.Count; i++)
                {
                    var next = _priorityScratch[i].Activity;
                    if (next == primary)
                        continue;
                    if (next == ScheduleActivity.Idle)
                        continue; // idle last
                    if (ActivityResolver.TryResolve(world, entity, next, duration, out resolved))
                        return true;
                }
            }

            // Last resort: 发呆
            return ActivityResolver.TryResolve(world, entity, ScheduleActivity.Idle, duration, out resolved);
        }

        static bool TryClaimResolvedSlot(SimulationWorld world, Entity entity, ref ResolvedActivity resolved)
        {
            if (resolved == null)
                return false;
            if (string.IsNullOrEmpty(resolved.WorkAreaId))
                return true;
            if (!world.TryGetWorkArea(resolved.WorkAreaId, out var area))
                return false;
            var cap = area.Capacity > 0 ? area.Capacity : 4;
            if (!world.WorkAreaOccupancy.TryReserve(resolved.WorkAreaId, entity.Id, cap, out var slot))
                return false;
            resolved.SlotIndex = slot;
            return true;
        }

        static void ForceInPlaceIdle(Entity entity, ulong duration, out ResolvedActivity resolved)
        {
            var locationId = string.Empty;
            if (entity.TryGet<EntityLocationComponent>(out var cur) && cur.HasLocation)
                locationId = cur.LocationId;
            resolved = new ResolvedActivity
            {
                Activity = ScheduleActivity.Idle,
                WorkAreaId = string.Empty,
                LocationId = locationId,
                NeedsMove = false,
                DurationTicks = duration,
                SlotIndex = -1
            };
        }
    }
}
