using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Npc;
using XianXia.Core.Orders;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Actions
{
    /// <summary>
    /// Unified NPC／schedule travel: sets MovementIntent for Host pathfinding;
    /// completes on HostArrived or duration timeout, then commits EntityLocation.
    /// </summary>
    public sealed class MoveAction : IAction
    {
        public MoveAction(
            ActionId id,
            EntityId subject,
            OrderId sourceOrderId,
            ulong durationTicks,
            string targetWorkAreaId,
            int slotIndex = -1)
        {
            Id = id;
            Subject = subject;
            SourceOrderId = sourceOrderId;
            TargetWorkAreaId = targetWorkAreaId ?? string.Empty;
            SlotIndex = slotIndex;
            Clock = ActionClock.Start(durationTicks == 0 ? 1UL : durationTicks);
            Status = ActionStatus.Pending;
        }

        public ActionId Id { get; }
        public EntityId Subject { get; }
        public OrderId SourceOrderId { get; }
        public string TargetWorkAreaId { get; }
        public int SlotIndex { get; private set; }
        public string TargetLocationId { get; private set; } = string.Empty;
        public ActionStatus Status { get; private set; }
        public ActionClock Clock { get; private set; }

        public Result CanStart(SimulationWorld world)
        {
            if (!world.Entities.TryGet(Subject, out var entity))
                return Result.Failure(ErrorCode.EntityNotFound, "Subject missing.");
            if (!entity.TryGet<LifecycleComponent>(out var life))
                return Result.Failure(ErrorCode.ComponentMissing, "Lifecycle missing.");
            if (life.IsDead || life.IsRemoved || life.IsIncapacitated)
                return Result.Failure(ErrorCode.ActionCannotStart, "Subject cannot move.", life.State.ToString());
            if (string.IsNullOrEmpty(TargetWorkAreaId) ||
                !world.TryGetWorkArea(TargetWorkAreaId, out var area) ||
                string.IsNullOrEmpty(area.LocationId))
                return Result.Failure(ErrorCode.InvalidArgument, "Move target WorkArea missing.");
            if (!world.WorldRegion.TryGet(area.LocationId, out _))
                return Result.Failure(ErrorCode.NotFound, "Move target Location missing.", area.LocationId);
            return Result.Success();
        }

        public Result Start(SimulationWorld world)
        {
            var can = CanStart(world);
            if (can.IsFailure) return can;

            world.TryGetWorkArea(TargetWorkAreaId, out var area);
            TargetLocationId = area.LocationId;

            if (!world.Entities.TryGet(Subject, out var entity))
                return Result.Failure(ErrorCode.EntityNotFound, "Subject missing.");

            if (!entity.TryGet<MovementIntentComponent>(out var intent))
            {
                intent = new MovementIntentComponent();
                var added = entity.AddComponent(intent);
                if (added.IsFailure)
                    return added;
            }

            intent.Begin(TargetLocationId, TargetWorkAreaId, SlotIndex);
            if (!string.IsNullOrEmpty(TargetWorkAreaId) &&
                world.TryGetWorkArea(TargetWorkAreaId, out var claimArea))
            {
                var cap = claimArea.Capacity > 0 ? claimArea.Capacity : 4;
                if (world.WorkAreaOccupancy.TryReserve(TargetWorkAreaId, Subject, cap, out var slot))
                {
                    SlotIndex = slot;
                    intent.SlotIndex = slot;
                }
            }

            Status = ActionStatus.Running;
            return Result.Success();
        }

        public Result Advance(SimulationWorld world)
        {
            if (Status != ActionStatus.Running)
                return Result.Failure(ErrorCode.InvalidOperation, "Action not running.");

            Clock = Clock.Consume(1);

            var arrived = false;
            if (world.Entities.TryGet(Subject, out var entity) &&
                entity.TryGet<MovementIntentComponent>(out var intent) &&
                intent.Active)
            {
                arrived = intent.HostArrived;
            }

            if (arrived || Clock.IsComplete)
            {
                CommitArrival(world);
                Status = ActionStatus.Completed;
            }

            return Result.Success();
        }

        void CommitArrival(SimulationWorld world)
        {
            if (!world.Entities.TryGet(Subject, out var entity))
                return;

            if (!entity.TryGet<EntityLocationComponent>(out var loc))
            {
                loc = new EntityLocationComponent();
                entity.AddComponent(loc);
            }

            if (!string.IsNullOrEmpty(TargetLocationId))
                loc.LocationId = TargetLocationId;

            if (entity.TryGet<MovementIntentComponent>(out var intent))
                intent.Clear();
        }

        public void Cancel()
        {
            if (Status == ActionStatus.Pending || Status == ActionStatus.Running)
                Status = ActionStatus.Cancelled;
        }

        public void Restore(ActionStatus status, ActionClock clock, string targetLocationId = null)
        {
            Status = status;
            Clock = clock;
            if (!string.IsNullOrEmpty(targetLocationId))
                TargetLocationId = targetLocationId;
        }
    }
}
