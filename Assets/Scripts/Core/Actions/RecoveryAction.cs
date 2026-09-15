using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Orders;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Actions
{
    public sealed class RecoveryAction : IAction
    {
        public const ulong DefaultDurationTicks = 6;

        public RecoveryAction(ActionId id, EntityId subject, OrderId sourceOrderId, ulong durationTicks, string recoverySpotId)
        {
            Id = id;
            Subject = subject;
            SourceOrderId = sourceOrderId;
            RecoverySpotId = recoverySpotId ?? string.Empty;
            Clock = ActionClock.Start(durationTicks);
            Status = ActionStatus.Pending;
        }

        public ActionId Id { get; }
        public EntityId Subject { get; }
        public OrderId SourceOrderId { get; }
        public string RecoverySpotId { get; }
        public ActionStatus Status { get; private set; }
        public ActionClock Clock { get; private set; }

        public Result CanStart(SimulationWorld world)
        {
            if (world == null || string.IsNullOrWhiteSpace(RecoverySpotId))
                return Result.Failure(ErrorCode.InvalidArgument, "恢复处无效。");
            if (world.Strategic != null &&
                (world.Strategic.CharacterEncounter?.Phase == CharacterEncounterPhase.Active ||
                 world.Strategic.ClockFreeze.Reason != StrategicClockFreezeReason.None))
                return Result.Failure(ErrorCode.ActionCannotStart, "战斗或时间冻结期间不能恢复。");
            if (!world.Entities.TryGet(Subject, out var entity))
                return Result.Failure(ErrorCode.EntityNotFound, "恢复角色不存在。");
            return CombatRecoveryService.CanRecover(entity);
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
            Clock = Clock.Consume(1);
            if (!Clock.IsComplete) return Result.Success();
            if (!world.Entities.TryGet(Subject, out var entity))
            {
                Status = ActionStatus.Failed;
                return Result.Failure(ErrorCode.EntityNotFound, "恢复角色不存在。");
            }
            var restored = CombatRecoveryService.RestoreToMaximum(entity);
            Status = restored.IsSuccess ? ActionStatus.Completed : ActionStatus.Failed;
            return restored;
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
