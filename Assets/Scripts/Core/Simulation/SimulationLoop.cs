using System.Collections.Generic;
using XianXia.Core.Actions;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Labor;
using XianXia.Core.Npc;
using XianXia.Core.Orders;
using XianXia.Core.Results;
using XianXia.Core.Schedule;
using XianXia.Core.Settlement;
using XianXia.Core.Social;
using XianXia.Core.Combat;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Simulation
{
    public sealed class SimulationLoop
    {
        public const string OverrideByPlayerReason = "OverrideByPlayer";

        readonly SimulationWorld _world;
        readonly ScheduleDriver _scheduleDriver;
        readonly NpcActivityDriver _npcActivityDriver;
        readonly SocialTickDriver _socialTickDriver;
        readonly XianXia.Core.Social.SupervisorAngerDriver _supervisorAngerDriver;
        readonly bool _socialTickEnabled;
        readonly List<IDayBoundaryHandler> _dayBoundaryHandlers;
        ulong _nextOrderId = 1;

        public SimulationLoop(
            SimulationWorld world,
            ScheduleDriver scheduleDriver = null,
            IEnumerable<IDayBoundaryHandler> dayBoundaryHandlers = null,
            SocialTickDriver socialTickDriver = null,
            bool enableSocialTick = false,
            NpcActivityDriver npcActivityDriver = null)
        {
            _world = world;
            _scheduleDriver = scheduleDriver ?? new ScheduleDriver();
            _npcActivityDriver = npcActivityDriver ?? new NpcActivityDriver();
            _socialTickDriver = socialTickDriver ?? new SocialTickDriver();
            _supervisorAngerDriver = new XianXia.Core.Social.SupervisorAngerDriver();
            _socialTickEnabled = enableSocialTick;
            if (dayBoundaryHandlers != null)
            {
                _dayBoundaryHandlers = new List<IDayBoundaryHandler>(dayBoundaryHandlers);
            }
            else
            {
                _dayBoundaryHandlers = new List<IDayBoundaryHandler>
                {
                    new QuotaConsequenceHandler(),
                    new SettlementProductionHandler()
                };
            }
        }

        public SimulationWorld World => _world;

        /// <summary>Register an additional day-boundary consumer (QuotaConsequence is default).</summary>
        public void AddDayBoundaryHandler(IDayBoundaryHandler handler)
        {
            if (handler != null)
                _dayBoundaryHandlers.Add(handler);
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

        /// <summary>
        /// Demo Stop semantics [49]/[32]: cancel active action and clear pending orders.
        /// </summary>
        public Result StopSubject(EntityId subject)
        {
            if (!_world.Entities.TryGet(subject, out var entity))
                return Result.Failure(ErrorCode.EntityNotFound, "Stop subject missing.");

            _world.GetOrCreateOrderQueue(subject).Clear();

            if (entity.TryGet<ActionStateComponent>(out var actionState) &&
                actionState.HasActiveAction &&
                _world.ActiveActions.TryGetValue(actionState.ActiveActionId, out var action))
            {
                action.Cancel();
                ClearActive(action);
                _world.Events.Publish(
                    EventType.ScheduleInterrupted,
                    _world.Tick,
                    actor: subject,
                    target: subject,
                    payload: "Stop");
            }

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
            if (StrategicClockFreezeService.IsWorldTickFrozen(_world))
                return Result.Success();

            var previous = _world.Tick;
            _world.Tick = _world.Tick.Add(1);
            ProcessDayBoundary(previous, _world.Tick);
            _npcActivityDriver.Drive(_world, this);
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

            _npcActivityDriver.Drive(_world, this);
            _scheduleDriver.Drive(_world, this);
            _supervisorAngerDriver.Tick(_world);
            if (_socialTickEnabled)
                _socialTickDriver.Tick(_world);
            // PlayerParty World Travel advance:
            // SimulationLoop -> StrategicTravelDriver.AfterTravelTick
            //   -> PlayerPartyHexTravelService.AdvanceAll -> AdvanceDistanceBudget
            // (FormalArmy / Background also advanced inside AfterTravelTick.)
            StrategicTravelDriver.AfterTravelTick(_world, 1);
            CombatLifeStateService.TickCorpseDecay(_world);
            return Result.Success();
        }

        void ProcessDayBoundary(WorldTick previous, WorldTick current)
        {
            var before = DayClock.FromWorldTick(previous);
            var after = DayClock.FromWorldTick(current);
            if (after.DayIndex <= before.DayIndex)
                return;

            for (var ended = before.DayIndex; ended < after.DayIndex; ended++)
            {
                var started = ended + 1UL;
                _world.Events.Publish(
                    EventType.DayEnded,
                    current,
                    payload: "dayIndex=" + ended);

                for (var i = 0; i < _dayBoundaryHandlers.Count; i++)
                    _dayBoundaryHandlers[i].OnDayEnded(_world, ended);

                _world.Events.Publish(
                    EventType.DayStarted,
                    current,
                    payload: "dayIndex=" + started);

                for (var i = 0; i < _dayBoundaryHandlers.Count; i++)
                    _dayBoundaryHandlers[i].OnDayStarted(_world, started);
            }
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
            if (action is MoveAction &&
                entity.TryGet<MovementIntentComponent>(out var intent))
                intent.Clear();
            // Soft slot: release when leaving the action. Move→Work same tick re-reserves in Work.Start.
            _world.WorkAreaOccupancy.Release(action.Subject);
            if (!entity.TryGet<ActionStateComponent>(out var state))
                return;
            state.ActiveActionId = ActionId.None;
            state.ActiveClock = null;
            state.ActiveOrderSource = OrderSource.Player;
        }
    }
}
