using XianXia.Core.Actions;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Labor;
using XianXia.Core.Orders;
using XianXia.Core.Results;
using XianXia.Core.Schedule;

namespace XianXia.Core.Simulation
{
    public sealed class SimulationLoop
    {
        public const string OverrideByPlayerReason = "OverrideByPlayer";

        readonly SimulationWorld _world;
        readonly ScheduleDriver _scheduleDriver;
        ulong _nextOrderId = 1;

        public SimulationLoop(SimulationWorld world, ScheduleDriver scheduleDriver = null)
        {
            _world = world;
            _scheduleDriver = scheduleDriver ?? new ScheduleDriver();
        }

        public ulong PeekNextOrderId => _nextOrderId;

        public void RestoreNextOrderId(ulong next) => _nextOrderId = next == 0 ? 1UL : next;

        public OrderId AllocateOrderId() => new OrderId(_nextOrderId++);

        public Result EnqueueOrder(Order order)
        {
            if (order == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Order is null.");
            if (!_world.Entities.TryGet(order.Subject, out _))
                return Result.Failure(ErrorCode.EntityNotFound, "Order subject missing.");

            if (order.Source == OrderSource.Player)
                InterruptScheduleForPlayer(order.Subject);

            _world.GetOrCreateOrderQueue(order.Subject).Enqueue(order);
            TryStartNext(order.Subject);
            return Result.Success();
        }

        public Order CreateWaitOrder(EntityId subject, ulong ticks, OrderSource source = OrderSource.Player)
        {
            return new Order(new OrderId(_nextOrderId++), subject, OrderType.Wait, source, waitTicks: ticks);
        }

        public Order CreateCultivateOrder(EntityId subject, ulong ticks, OrderSource source = OrderSource.Player)
        {
            return new Order(new OrderId(_nextOrderId++), subject, OrderType.Cultivate, source, waitTicks: ticks);
        }

        public Result TickOnce()
        {
            _world.Tick = _world.Tick.Add(1);
            _scheduleDriver.Drive(_world, this);

            var actionIds = new System.Collections.Generic.List<ActionId>(_world.ActiveActions.Keys);
            foreach (var actionId in actionIds)
            {
                if (!_world.ActiveActions.TryGetValue(actionId, out var action))
                    continue;

                var advanced = action.Advance(_world);
                SyncActionState(action);

                if (action.Status == ActionStatus.Completed)
                {
                    _world.Events.Publish(EventType.ActionCompleted, _world.Tick, action.Subject, action.Subject, action.Id.ToString());
                    ClearActive(action);
                    TryStartNext(action.Subject);
                }
                else if (action.Status == ActionStatus.Cancelled)
                {
                    ClearActive(action);
                    TryStartNext(action.Subject);
                }
                else if (action.Status == ActionStatus.Failed || advanced.IsFailure)
                {
                    _world.Events.Publish(EventType.ActionFailed, _world.Tick, action.Subject, action.Subject,
                        advanced.IsFailure ? advanced.Error.Message : "failed");
                    ClearActive(action);
                    TryStartNext(action.Subject);
                }
            }

            _scheduleDriver.Drive(_world, this);
            return Result.Success();
        }

        void InterruptScheduleForPlayer(EntityId subject)
        {
            if (!_world.Entities.TryGet(subject, out var entity))
                return;
            if (!entity.TryGet<ActionStateComponent>(out var actionState) || !actionState.HasActiveAction)
            {
                _world.GetOrCreateOrderQueue(subject).RemoveWhere(o => o.Source == OrderSource.Schedule);
                return;
            }

            if (actionState.ActiveOrderSource != OrderSource.Schedule)
                return;

            if (!_world.ActiveActions.TryGetValue(actionState.ActiveActionId, out var action))
                return;

            var wasLabor = action is LaborAction;
            var remaining = action.Clock.RemainingTicks;
            var incomplete = !action.Clock.IsComplete &&
                             (action.Status == ActionStatus.Running || action.Status == ActionStatus.Pending);

            action.Cancel();
            _world.Events.Publish(
                EventType.ScheduleInterrupted,
                _world.Tick,
                actor: subject,
                target: subject,
                payload: OverrideByPlayerReason);

            if (wasLabor && incomplete && entity.TryGet<DailyTaskComponent>(out var daily))
            {
                var delta = remaining > 0 ? (int)remaining : 1;
                daily.Deviation += delta;
                _world.Events.Publish(
                    EventType.QuotaDeviationCreated,
                    _world.Tick,
                    actor: subject,
                    target: subject,
                    payload: "delta=" + delta + ";deviation=" + daily.Deviation +
                             ";completed=" + daily.CompletedAmount +
                             ";required=" + daily.RequiredAmount);
            }

            ClearActive(action);
            _world.GetOrCreateOrderQueue(subject).RemoveWhere(o => o.Source == OrderSource.Schedule);
        }

        void TryStartNext(EntityId subject)
        {
            if (!_world.Entities.TryGet(subject, out var entity))
                return;
            if (!entity.TryGet<ActionStateComponent>(out var actionState))
                return;
            if (actionState.HasActiveAction)
                return;

            var queue = _world.GetOrCreateOrderQueue(subject);
            if (!queue.TryDequeue(out var order))
                return;

            var translated = _world.Translator.Translate(order);
            if (translated.IsFailure)
            {
                _world.Events.Publish(EventType.OrderRejected, _world.Tick, subject, subject, translated.Error.Message);
                return;
            }

            var action = translated.Value;
            var can = action.CanStart(_world);
            if (can.IsFailure)
            {
                _world.Events.Publish(EventType.OrderRejected, _world.Tick, subject, subject, can.Error.Message);
                _world.Events.Publish(EventType.ActionFailed, _world.Tick, subject, subject, can.Error.Message);
                return;
            }

            var started = action.Start(_world);
            if (started.IsFailure)
            {
                _world.Events.Publish(EventType.ActionFailed, _world.Tick, subject, subject, started.Error.Message);
                return;
            }

            _world.ActiveActions[action.Id] = action;
            actionState.ActiveActionId = action.Id;
            actionState.ActiveClock = action.Clock;
            actionState.ActiveOrderSource = order.Source;
        }

        void SyncActionState(IAction action)
        {
            if (!_world.Entities.TryGet(action.Subject, out var entity))
                return;
            if (!entity.TryGet<ActionStateComponent>(out var state))
                return;
            state.ActiveActionId = action.Id;
            state.ActiveClock = action.Clock;
        }

        void ClearActive(IAction action)
        {
            _world.ActiveActions.Remove(action.Id);
            if (!_world.Entities.TryGet(action.Subject, out var entity))
                return;
            if (!entity.TryGet<ActionStateComponent>(out var state))
                return;
            state.ActiveActionId = ActionId.None;
            state.ActiveClock = null;
            state.ActiveOrderSource = OrderSource.Player;
        }
    }
}
