using System.Collections.Generic;
using XianXia.Core.Actions;
using XianXia.Core.Construction;
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
        readonly Dictionary<XianXia.Core.Domain.Ids.EntityId, ScheduleActivity> _lastDuty =
            new Dictionary<XianXia.Core.Domain.Ids.EntityId, ScheduleActivity>();
        SimulationWorld _dutyWorld;
        readonly SettlementPatrolRouteDriver _patrolRoutes = new SettlementPatrolRouteDriver();

        public void Drive(SimulationWorld world, SimulationLoop loop)
        {
            if (world == null || loop == null)
                return;
            if (!ReferenceEquals(world, _dutyWorld)) { _lastDuty.Clear(); _dutyWorld = world; }

            foreach (var entity in world.Entities.All)
            {
                if (XianXia.Core.Social.SupervisorAngerDriver.OwnsPursuit(world, entity.Id)) continue;
                if (!World.Strategic.HuangcunOpeningAccess.IsSimulated(world, entity)) continue;
                if ((SettlementPatrolService.Shift(entity) != 0 || SupervisorRoutineDriver.Applies(entity)) &&
                    !World.Strategic.CharacterEncounterService.OwnsParticipantSpatialState(world, entity.Id) &&
                    entity.TryGet<ScheduleComponent>(out var dutyBinding) &&
                    world.TryGetSchedule(dutyBinding.DefinitionId, out var dutyDef) && dutyDef.TryResolve(world.Tick, out var dutyBlock))
                {
                    if (!_lastDuty.TryGetValue(entity.Id, out var previous) || previous != dutyBlock.Activity)
                    {
                        loop.CancelOrdersFromSource(entity.Id, OrderSource.Schedule);
                        world.WorkAreaOccupancy.Release(entity.Id);
                        SettlementPatrolRouteDriver.ClearOwnedIntent(entity);
                        if (entity.TryGet<MovementIntentComponent>(out var oldMove) && oldMove.Active &&
                            (oldMove.DestinationKey?.StartsWith("schedule-home-bed:") == true ||
                             oldMove.DestinationKey?.StartsWith("personal-leisure") == true)) oldMove.Clear();
                        if (SupervisorRoutineDriver.Applies(entity) && dutyBlock.Activity != ScheduleActivity.Idle)
                        {
                            ChessRecreationService.Interrupt(world, loop, entity.Id);
                            if (entity.TryGet<PersonalLifeComponent>(out var personal) && personal.Energy > 30 && personal.Satiety > 35)
                                loop.CancelOrdersFromSource(entity.Id, OrderSource.Ai);
                        }
                        _lastDuty[entity.Id] = dutyBlock.Activity;
                    }
                }
                if (SettlementPatrolService.Shift(entity) != 0 && !SettlementPatrolService.CanRunSchedule(world, entity))
                {
                    loop.CancelOrdersFromSource(entity.Id, OrderSource.Schedule);
                    continue;
                }
                if (MortalCivilianQuery.IsManagedCivilian(world, entity))
                {
                    // Rest/Labor/Idle and needs are owned by MortalCivilianService. A specialized
                    // bound block (Patrol/Inspect/Cultivate/...) may still use the existing generic
                    // schedule action chain, unless a Squad or higher authority owns the character.
                    if (!world.Civilians.TryGet(entity.Id, out var civilian) ||
                        civilian.Activity != MortalActivity.DedicatedDuty ||
                        !MortalCivilianMovementAuthority.CanCivilianOwnLocalMovement(world, entity.Id, civilian) ||
                        !AutonomousActionContinuationService.CanContinue(world, entity.Id) ||
                        !entity.TryGet<ScheduleComponent>(out var managedBinding) ||
                        string.IsNullOrEmpty(managedBinding.DefinitionId) ||
                        !world.TryGetSchedule(managedBinding.DefinitionId, out var managedDefinition))
                    {
                        loop.CancelOrdersFromSource(entity.Id, OrderSource.Schedule);
                        continue;
                    }
                    if (!entity.TryGet<JobComponent>(out _))
                        entity.AddComponent(new JobComponent());
                    DriveEntity(world, loop, entity, managedDefinition, allowFallback: false);
                    continue;
                }
                if (World.Strategic.SquadCommandService.OwnsIndividualSchedule(world, entity.Id))
                {
                    loop.CancelOrdersFromSource(entity.Id, OrderSource.Schedule);
                    continue;
                }
                if ((entity.Tags & EntityTag.Character) != 0 && !SettlementPatrolService.CanRunSchedule(world, entity))
                    continue;
                if (!entity.TryGet<ScheduleComponent>(out var binding) ||
                    string.IsNullOrEmpty(binding.DefinitionId))
                    continue;
                if (!AutonomousActionContinuationService.CanContinue(world, entity.Id))
                {
                    loop.CancelOrdersFromSource(entity.Id, OrderSource.Schedule);
                    world.WorkAreaOccupancy.Release(entity.Id);
                    continue;
                }
                if (!world.TryGetSchedule(binding.DefinitionId, out var definition))
                    continue;

                if (!entity.TryGet<JobComponent>(out _))
                    entity.AddComponent(new JobComponent());

                DriveEntity(world, loop, entity, definition, allowFallback: SettlementPatrolService.Shift(entity) == 0);
            }
        }

        void DriveEntity(
            SimulationWorld world,
            SimulationLoop loop,
            Entity entity,
            ScheduleDefinition definition,
            bool allowFallback = true)
        {
            if (!entity.TryGet<ActionStateComponent>(out var actionState))
                return;
            if (definition.TryResolve(world.Tick, out var routeBlock) &&
                _patrolRoutes.Drive(world, loop, entity, routeBlock.Activity)) return;
            if (SupervisorRoutineDriver.IsLeisure(world, entity)) return;
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
            var choice = PersonalityScheduleBias.Apply(block,
                SettlementPatrolService.Shift(entity) != 0 || SupervisorRoutineDriver.Applies(entity) ? null : profile);

            var tickInDay = (int)(world.Tick.Value % (ulong)WorldTick.TicksPerDay);
            var remainingInBlock = (ulong)(block.EndTickInDay - tickInDay);
            var duration = choice.DurationTicks;
            if (remainingInBlock < duration)
                duration = remainingInBlock;
            if (duration == 0)
                return;

            if ((choice.Activity == ScheduleActivity.Rest || choice.Activity == ScheduleActivity.Idle) &&
                TryDriveAssignedHome(world, loop, entity, queue, choice.Activity, duration))
                return;
            if (choice.Activity == ScheduleActivity.Rest && RoomBedFacilityService.TryGetAssignedBed(world, entity.Id, out _))
                return; // A broken/unavailable home is not an outdoor sleeping station.

            var resolvedOk = allowFallback
                ? TryResolveWithFallback(world, entity, choice.Activity, duration, out var resolved)
                : ActivityResolver.TryResolve(world, entity, choice.Activity, duration, out resolved);
            if (!resolvedOk)
            {
                if (!allowFallback && world.Civilians.TryGet(entity.Id, out var civilian))
                    civilian.ScheduleStatus = "专职日程受阻：没有对应执行目标 · " + choice.Activity;
                return;
            }

            // Soft slot claim before enqueue so peers see occupied capacity this tick.
            if (!TryClaimResolvedSlot(world, entity, ref resolved))
            {
                if (!allowFallback)
                {
                    if (world.Civilians.TryGet(entity.Id, out var civilian))
                        civilian.ScheduleStatus = "专职日程受阻：没有可用职责工位 · " + choice.Activity;
                    return;
                }
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
            else if (resolved.Activity == ScheduleActivity.Cultivate && !string.IsNullOrEmpty(resolved.WorkAreaId))
                orderType = OrderType.Cultivate;
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

        static bool TryDriveAssignedHome(
            SimulationWorld world,
            SimulationLoop loop,
            Entity entity,
            OrderQueue queue,
            ScheduleActivity activity,
            ulong duration)
        {
            if (!RoomBedFacilityService.TryGetAssignedHome(world, entity.Id, out var bed, out _, out var access))
            {
                if (entity.TryGet<PersonalLifeComponent>(out var blockedPersonal) &&
                    RoomBedFacilityService.TryGetAssignedBed(world, entity.Id, out _))
                    blockedPersonal.Status = "返床受阻：床边没有合法使用格";
                return false;
            }
            if (!RoomBedFacilityService.CanUseBed(world, bed, entity.Id, out var bedReason))
            {
                if (entity.TryGet<PersonalLifeComponent>(out var blockedPersonal)) blockedPersonal.Status = "返床受阻：" + bedReason;
                return true; // an invalid assigned bed is reported by the shared query; never fall back to another home
            }

            if (!RoomBedFacilityService.IsAtAccessPoint(world, bed, entity.Id))
            {
                if (entity.TryGet<PersonalLifeComponent>(out var returning))
                { returning.Activity = PersonalLifeActivity.None; returning.Status = "前往自己的床铺休息"; }
                queue.RemoveWhere(o => o.Source == OrderSource.Schedule);
                if (!entity.TryGet<MovementIntentComponent>(out var intent))
                { intent = new MovementIntentComponent(); entity.AddComponent(intent); }
                if (entity.TryGet<PersonalLifeComponent>(out var restState) &&
                    world.Tick.Value < restState.BedRetryAfterTick) return true;
                if (intent.Active && intent.DestinationKey?.StartsWith("schedule-home-bed:") == true &&
                    (intent.HostArrived || intent.HostPathState == MovementHostPathState.PermanentFailure ||
                     intent.HostPathState == MovementHostPathState.RetryablePathUnavailable))
                {
                    intent.Clear();
                    if (entity.TryGet<PersonalLifeComponent>(out var retryState))
                    { retryState.BedRetryAfterTick = world.Tick.Value + 8; retryState.Status = "返床受阻：等待重新寻路"; }
                    return true;
                }
                var radius = world.SurfaceSpatial.TryGet(bed.SurfaceId, out var metric) && metric != null
                    ? metric.CellSize * .75f : 0f;
                var key = "schedule-home-bed:" + bed.StableAssetId;
                if (!intent.Active || !intent.HasWorldTarget || intent.TargetSurfaceId != bed.SurfaceId ||
                    intent.DestinationKey != key)
                    intent.BeginWorldPoint(bed.SurfaceId, access.X, access.Y,
                        destinationKey: key, arrivalRadiusWorld: radius);
                return true;
            }

            if (entity.TryGet<MovementIntentComponent>(out var arrivedIntent)) arrivedIntent.Clear();
            var type = activity == ScheduleActivity.Rest ? OrderType.RestAtBed : OrderType.Wait;
            var target = activity == ScheduleActivity.Rest ? bed.StableAssetId : string.Empty;
            if (queue.HasMatching(OrderSource.Schedule, type, target)) return true;
            queue.RemoveWhere(o => o.Source == OrderSource.Schedule);
            loop.EnqueueOrder(new Order(loop.AllocateOrderId(), entity.Id, type, OrderSource.Schedule,
                waitTicks: duration == 0 ? 1UL : duration, targetRef: target, activity: activity));
            return true;
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
