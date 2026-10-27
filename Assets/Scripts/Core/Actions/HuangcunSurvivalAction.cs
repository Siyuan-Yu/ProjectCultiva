using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Npc;
using XianXia.Core.Orders;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Actions
{
    public sealed class HuangcunSurvivalAction : IAction
    {
        public HuangcunSurvivalAction(ActionId id, EntityId subject, OrderId order, ulong ticks, string task)
        { Id = id; Subject = subject; SourceOrderId = order; Task = task; Clock = ActionClock.Start(ticks); }
        public ActionId Id { get; }
        public EntityId Subject { get; }
        public OrderId SourceOrderId { get; }
        public string Task { get; }
        public ActionStatus Status { get; private set; } = ActionStatus.Pending;
        public ActionClock Clock { get; private set; }
        public Result CanStart(SimulationWorld world) => HuangcunSurvivalService.Validate(world, Subject, Task);
        public Result Start(SimulationWorld world)
        {
            var r = CanStart(world);
            if (r.IsSuccess)
            {
                Status = ActionStatus.Running;
                if ((Task == "feed" || Task == "medicine" || Task == "force_release") && HuangcunSurvivalService.Prisoner(world) is var prisoner &&
                    prisoner != null && prisoner.TryGet<MovementIntentComponent>(out var intent)) intent.Clear();
            }
            return r;
        }
        public Result Advance(SimulationWorld world)
        {
            var r = CanStart(world);
            if (r.IsFailure) { Status = ActionStatus.Failed; world.HuangcunSurvival.Message = r.Error.ToString(); return r; }
            Clock = Clock.Consume(1);
            if (!Clock.IsComplete) return Result.Success();
            r = HuangcunSurvivalService.Complete(world, Subject, Task);
            Status = r.IsSuccess ? ActionStatus.Completed : ActionStatus.Failed;
            if (r.IsFailure) world.HuangcunSurvival.Message = r.Error.ToString();
            return r;
        }
        public void Cancel() { if (Status == ActionStatus.Pending || Status == ActionStatus.Running) Status = ActionStatus.Cancelled; }
        public void Restore(ActionStatus status, ActionClock clock) { Status = status; Clock = clock; }
    }
}
