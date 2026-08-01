using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Exploration;
using XianXia.Core.Labor;
using XianXia.Core.Orders;
using XianXia.Core.Results;
using XianXia.Core.Settlement;
using XianXia.Core.Simulation;

namespace XianXia.Core.Actions
{
    /// <summary>
    /// Labor session: Start → ActionClock → +LaborProgress per tick → Complete.
    /// </summary>
    public sealed class LaborAction : IAction
    {
        public LaborAction(ActionId id, EntityId subject, OrderId sourceOrderId, ulong durationTicks)
        {
            Id = id;
            Subject = subject;
            SourceOrderId = sourceOrderId;
            Clock = ActionClock.Start(durationTicks);
            Status = ActionStatus.Pending;
        }

        public ActionId Id { get; }
        public EntityId Subject { get; }
        public OrderId SourceOrderId { get; }
        public ActionStatus Status { get; private set; }
        public ActionClock Clock { get; private set; }

        public Result CanStart(SimulationWorld world)
        {
            if (!world.Entities.TryGet(Subject, out var entity))
                return Result.Failure(ErrorCode.EntityNotFound, "Subject missing.");
            if (!entity.TryGet<LifecycleComponent>(out var life))
                return Result.Failure(ErrorCode.ComponentMissing, "Lifecycle missing.");
            if (life.IsDead || life.IsRemoved || life.IsIncapacitated)
                return Result.Failure(ErrorCode.ActionCannotStart, "Subject cannot labor.", life.State.ToString());
            if (!entity.TryGet<DailyTaskComponent>(out _))
                return Result.Failure(ErrorCode.ComponentMissing, "DailyTaskComponent missing.");
            return Result.Success();
        }

        public Result Start(SimulationWorld world)
        {
            var can = CanStart(world);
            if (can.IsFailure) return can;
            Status = ActionStatus.Running;
            return Result.Success();
        }

        public Result Advance(SimulationWorld world)
        {
            if (Status != ActionStatus.Running)
                return Result.Failure(ErrorCode.InvalidOperation, "Action not running.");

            if (!world.Entities.TryGet(Subject, out var entity) ||
                !entity.TryGet<DailyTaskComponent>(out var daily))
            {
                Status = ActionStatus.Failed;
                return Result.Failure(ErrorCode.ActionFailed, "DailyTaskComponent missing during labor.");
            }

            Clock = Clock.Consume(1);
            daily.CompletedAmount += 1;
            // Demo work-zone gather is player-driven; schedule labor stays on settlement day-end production.
            if (entity.TryGet<ActionStateComponent>(out var actionState) &&
                actionState.ActiveOrderSource == OrderSource.Player)
                ProduceFromCurrentLocation(world, entity);

            if (Clock.IsComplete)
                Status = ActionStatus.Completed;

            return Result.Success();
        }

        static void ProduceFromCurrentLocation(SimulationWorld world, Entity entity)
        {
            if (!entity.TryGet<EntityLocationComponent>(out var loc) || !loc.HasLocation)
                return;
            if (!world.WorldRegion.TryGet(loc.LocationId, out var location))
                return;
            if (string.IsNullOrEmpty(location.ResourceOnExploreId) || location.ResourceOnExploreAmount <= 0)
                return;
            if (!world.Settlements.TryGetPrimary(out var settlement))
                return;

            var amount = location.ResourceOnExploreAmount;
            settlement.AddStock(location.ResourceOnExploreId, amount);
            world.Events.Publish(
                EventType.SettlementStockChanged,
                world.Tick,
                actor: entity.Id,
                payload: settlement.Id + ":" + location.ResourceOnExploreId + ":" +
                         settlement.GetStock(location.ResourceOnExploreId));
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
