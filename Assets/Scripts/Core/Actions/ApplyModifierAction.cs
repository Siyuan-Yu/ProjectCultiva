using XianXia.Core.Attributes;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Orders;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Actions
{
    /// <summary>Instant (1-tick) sample action that applies one modifier.</summary>
    public sealed class ApplyModifierAction : IAction
    {
        readonly AttributeId _attribute;
        readonly ModifierOperation _operation;
        readonly double _value;
        readonly SourceRef _source;

        public ApplyModifierAction(
            ActionId id,
            EntityId subject,
            OrderId sourceOrderId,
            AttributeId attribute,
            ModifierOperation operation,
            double value,
            SourceRef source)
        {
            Id = id;
            Subject = subject;
            SourceOrderId = sourceOrderId;
            _attribute = attribute;
            _operation = operation;
            _value = value;
            _source = source;
            Clock = ActionClock.Start(1);
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
            if (!entity.TryGet<AttributesComponent>(out _))
                return Result.Failure(ErrorCode.ComponentMissing, "Attributes missing.");
            if (!entity.TryGet<LifecycleComponent>(out var life))
                return Result.Failure(ErrorCode.ComponentMissing, "Lifecycle missing.");
            if (life.IsDead || life.IsRemoved)
                return Result.Failure(ErrorCode.ActionCannotStart, "Subject cannot receive modifier.");
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
                !entity.TryGet<AttributesComponent>(out var attrs))
            {
                Status = ActionStatus.Failed;
                return Result.Failure(ErrorCode.ActionFailed, "Attributes missing during apply.");
            }

            attrs.AddModifier(_attribute, _operation, _value, _source);
            Clock = Clock.Consume(1);
            Status = ActionStatus.Completed;
            return Result.Success();
        }

        public void Restore(ActionStatus status, ActionClock clock)
        {
            Status = status;
            Clock = clock;
        }
    }
}
