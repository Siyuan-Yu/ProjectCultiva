using XianXia.Core.Concealment;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Orders;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Actions
{
    /// <summary>
    /// Cultivate session: Start → consume ActionClock → add CultivationProgress → Complete → maybe breakthrough.
    /// </summary>
    public sealed class CultivateAction : IAction
    {
        readonly CultivationService _cultivation = new CultivationService();

        public CultivateAction(ActionId id, EntityId subject, OrderId sourceOrderId, ulong durationTicks)
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
                return Result.Failure(ErrorCode.ActionCannotStart, "Subject cannot cultivate.", life.State.ToString());
            if (!entity.TryGet<CultivationComponent>(out var cultivation))
                return Result.Failure(ErrorCode.ComponentMissing, "CultivationComponent missing.");
            if (!cultivation.HasLearnedManual)
                return Result.Failure(ErrorCode.ActionCannotStart, "No learned manual.");
            if (cultivation.CultivationSpeed <= 0)
                return Result.Failure(ErrorCode.ActionCannotStart, "CultivationSpeed invalid.");
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
                !entity.TryGet<CultivationComponent>(out var cultivation))
            {
                Status = ActionStatus.Failed;
                return Result.Failure(ErrorCode.ActionFailed, "CultivationComponent missing during cultivate.");
            }

            Clock = Clock.Consume(1);
            cultivation.Progress += cultivation.CultivationSpeed;

            if (entity.TryGet<PersonalConcealmentRiskComponent>(out var risk))
                risk.Add(1);

            if (Clock.IsComplete)
            {
                Status = ActionStatus.Completed;
                var broke = _cultivation.TryBreakthrough(world, Subject);
                if (broke.IsFailure)
                {
                    Status = ActionStatus.Failed;
                    return broke;
                }
            }

            return Result.Success();
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
