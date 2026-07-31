using XianXia.Core.Actions;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Orders;
using XianXia.Core.Results;

namespace XianXia.Core.Simulation
{
    public sealed class SimulationLoop
    {
        readonly SimulationWorld _world;
        ulong _nextOrderId = 1;

        public SimulationLoop(SimulationWorld world)
        {
            _world = world;
        }

        public ulong PeekNextOrderId => _nextOrderId;

        public void RestoreNextOrderId(ulong next) => _nextOrderId = next == 0 ? 1UL : next;

        public Result EnqueueOrder(Order order)
        {
            if (order == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Order is null.");
            if (!_world.Entities.TryGet(order.Subject, out _))
                return Result.Failure(ErrorCode.EntityNotFound, "Order subject missing.");

            _world.GetOrCreateOrderQueue(order.Subject).Enqueue(order);
            TryStartNext(order.Subject);
            return Result.Success();
        }

        public Order CreateWaitOrder(EntityId subject, ulong ticks, OrderSource source = OrderSource.Player)
        {
            return new Order(new OrderId(_nextOrderId++), subject, OrderType.Wait, source, waitTicks: ticks);
        }

        public Result TickOnce()
        {
            _world.Tick = _world.Tick.Add(1);

            // Copy keys to avoid mutation during iteration.
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
                else if (action.Status == ActionStatus.Failed || advanced.IsFailure)
                {
                    _world.Events.Publish(EventType.ActionFailed, _world.Tick, action.Subject, action.Subject,
                        advanced.IsFailure ? advanced.Error.Message : "failed");
                    ClearActive(action);
                    TryStartNext(action.Subject);
                }
            }

            return Result.Success();
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
        }
    }
}
