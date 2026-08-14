using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Labor;
using XianXia.Core.Npc;
using XianXia.Core.Orders;
using XianXia.Core.Results;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;

namespace XianXia.Core.Actions
{
    /// <summary>
    /// On-site schedule work after MoveAction. Labor increments DailyTask when present;
    /// other activities are timed presence (patrol／inspect／rest).
    /// </summary>
    public sealed class WorkAction : IAction
    {
        public WorkAction(
            ActionId id,
            EntityId subject,
            OrderId sourceOrderId,
            ulong durationTicks,
            ScheduleActivity activity,
            string targetWorkAreaId,
            int slotIndex = -1)
        {
            Id = id;
            Subject = subject;
            SourceOrderId = sourceOrderId;
            Activity = activity;
            TargetWorkAreaId = targetWorkAreaId ?? string.Empty;
            SlotIndex = slotIndex;
            Clock = ActionClock.Start(durationTicks == 0 ? 1UL : durationTicks);
            Status = ActionStatus.Pending;
        }

        public ActionId Id { get; }
        public EntityId Subject { get; }
        public OrderId SourceOrderId { get; }
        public ScheduleActivity Activity { get; }
        public string TargetWorkAreaId { get; }
        public int SlotIndex { get; private set; }
        public ActionStatus Status { get; private set; }
        public ActionClock Clock { get; private set; }

        public Result CanStart(SimulationWorld world)
        {
            if (!world.Entities.TryGet(Subject, out var entity))
                return Result.Failure(ErrorCode.EntityNotFound, "Subject missing.");
            if (!entity.TryGet<LifecycleComponent>(out var life))
                return Result.Failure(ErrorCode.ComponentMissing, "Lifecycle missing.");
            if (life.IsDead || life.IsRemoved || life.IsIncapacitated)
                return Result.Failure(ErrorCode.ActionCannotStart, "Subject cannot work.", life.State.ToString());
            return Result.Success();
        }

        public Result Start(SimulationWorld world)
        {
            var can = CanStart(world);
            if (can.IsFailure) return can;

            if (!string.IsNullOrEmpty(TargetWorkAreaId) &&
                world.TryGetWorkArea(TargetWorkAreaId, out var area) &&
                !string.IsNullOrEmpty(area.LocationId) &&
                world.Entities.TryGet(Subject, out var entity))
            {
                if (!entity.TryGet<EntityLocationComponent>(out var loc))
                {
                    loc = new EntityLocationComponent();
                    entity.AddComponent(loc);
                }

                if (!loc.HasLocation ||
                    !string.Equals(loc.LocationId, area.LocationId, System.StringComparison.Ordinal))
                    loc.LocationId = area.LocationId;

                var cap = area.Capacity > 0 ? area.Capacity : 4;
                if (world.WorkAreaOccupancy.TryReserve(TargetWorkAreaId, Subject, cap, out var slot))
                    SlotIndex = slot;
            }

            Status = ActionStatus.Running;
            return Result.Success();
        }

        public Result Advance(SimulationWorld world)
        {
            if (Status != ActionStatus.Running)
                return Result.Failure(ErrorCode.InvalidOperation, "Action not running.");

            if (world.Entities.TryGet(Subject, out var entity) &&
                Activity == ScheduleActivity.Labor &&
                entity.TryGet<DailyTaskComponent>(out var daily))
            {
                daily.CompletedAmount += 1;
            }

            Clock = Clock.Consume(1);
            if (Clock.IsComplete)
            {
                AdvanceRoute(world);
                Status = ActionStatus.Completed;
            }

            return Result.Success();
        }

        void AdvanceRoute(SimulationWorld world)
        {
            if (!world.Entities.TryGet(Subject, out var entity))
                return;
            if (Activity != ScheduleActivity.Patrol &&
                Activity != ScheduleActivity.Inspect &&
                Activity != ScheduleActivity.Explore)
                return;

            var candidates = ActivityResolver.RouteCandidates(world, entity, Activity);
            if (candidates == null || candidates.Count <= 1)
                return;
            if (!entity.TryGet<JobComponent>(out var job))
            {
                job = new JobComponent();
                entity.AddComponent(job);
            }

            job.RouteIndex = (job.RouteIndex + 1) % candidates.Count;
        }

        public void Cancel()
        {
            if (Status == ActionStatus.Pending || Status == ActionStatus.Running)
                Status = ActionStatus.Cancelled;
        }

        public void Restore(ActionStatus status, ActionClock clock)
        {
            Status = status;
            Clock = clock;
        }
    }
}
