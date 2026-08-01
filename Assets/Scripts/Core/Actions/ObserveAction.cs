using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Opportunity;
using XianXia.Core.Orders;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Actions
{
    /// <summary>
    /// RTS Observe: spend ActionClock, maybe discover an abstract OpportunitySite.
    /// </summary>
    public sealed class ObserveAction : IAction
    {
        public ObserveAction(ActionId id, EntityId subject, OrderId sourceOrderId, ulong durationTicks)
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
                return Result.Failure(ErrorCode.ActionCannotStart, "Subject cannot observe.", life.State.ToString());
            if (!entity.TryGet<KnownSitesComponent>(out _))
                return Result.Failure(ErrorCode.ComponentMissing, "KnownSitesComponent missing.");
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

            Clock = Clock.Consume(1);
            if (!Clock.IsComplete)
                return Result.Success();

            Status = ActionStatus.Completed;
            ResolveObservation(world);
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

        void ResolveObservation(SimulationWorld world)
        {
            if (!world.Entities.TryGet(Subject, out var entity) ||
                !entity.TryGet<KnownSitesComponent>(out var known))
            {
                world.Events.Publish(EventType.ObservationResolved, world.Tick, Subject, Subject, "result=failed");
                return;
            }

            var candidates = new List<OpportunitySite>();
            foreach (var site in world.OpportunitySites.Values)
            {
                if (!known.Knows(site.Id))
                    candidates.Add(site);
            }

            if (candidates.Count == 0)
            {
                world.Events.Publish(
                    EventType.ObservationResolved,
                    world.Tick,
                    Subject,
                    Subject,
                    "result=none;reason=no_unknown_sites");
                return;
            }

            var roll = world.Random.NextInt(0, 100);
            if (roll >= world.ObservationDiscoverChancePercent)
            {
                world.Events.Publish(
                    EventType.ObservationResolved,
                    world.Tick,
                    Subject,
                    Subject,
                    "result=miss;roll=" + roll);
                return;
            }

            // Deterministic pick: lowest DefinitionId string among unknowns.
            candidates.Sort((a, b) => string.CompareOrdinal(a.Id.ToString(), b.Id.ToString()));
            var discovered = candidates[0];
            known.Discover(discovered.Id);

            world.Events.Publish(
                EventType.OpportunitySiteDiscovered,
                world.Tick,
                Subject,
                Subject,
                "site=" + discovered.Id + ";nameKey=" + discovered.NameKey);
            world.Events.Publish(
                EventType.ObservationResolved,
                world.Tick,
                Subject,
                Subject,
                "result=discovered;site=" + discovered.Id);
        }
    }
}
