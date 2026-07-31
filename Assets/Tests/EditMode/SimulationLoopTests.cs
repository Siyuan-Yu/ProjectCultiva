using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Orders;
using XianXia.Core.Simulation;

namespace XianXia.Tests
{
    public sealed class SimulationLoopTests
    {
        [Test]
        public void Wait4_Completes_On_Fourth_Tick()
        {
            var world = new SimulationWorld();
            var loop = new SimulationLoop(world);
            var entity = world.Entities.CreateCharacter(new DefinitionId("base", "character_labor_disciple")).Value;
            world.Events.Publish(EventType.EntityCreated, world.Tick, target: entity.Id);

            var order = loop.CreateWaitOrder(entity.Id, 4, OrderSource.Player);
            Assert.IsTrue(loop.EnqueueOrder(order).IsSuccess);
            Assert.IsTrue(entity.Get<ActionStateComponent>().HasActiveAction);

            for (var i = 1; i <= 3; i++)
            {
                loop.TickOnce();
                Assert.AreEqual((ulong)i, world.Tick.Value);
                Assert.IsFalse(entity.Get<ActionStateComponent>().ActiveClock.Value.IsComplete);
            }

            loop.TickOnce();
            Assert.AreEqual(4UL, world.Tick.Value);
            Assert.IsFalse(entity.Get<ActionStateComponent>().HasActiveAction);

            var drained = world.Events.Drain();
            Assert.IsTrue(drained.Exists(e => e.Type == EventType.ActionCompleted && e.Tick.Value == 4UL));
        }

        [Test]
        public void ActionClock_DoesNotChangeWorldTick_Directly()
        {
            var world = new SimulationWorld();
            var before = world.Tick;
            var entity = world.Entities.CreateCharacter(new DefinitionId("base", "a")).Value;
            var loop = new SimulationLoop(world);
            loop.EnqueueOrder(loop.CreateWaitOrder(entity.Id, 2));
            // Advancing action alone is done only through SimulationLoop.TickOnce which owns WorldTick++.
            Assert.AreEqual(before, world.Tick);
            loop.TickOnce();
            Assert.AreEqual(before.Add(1), world.Tick);
        }

        [Test]
        public void CanStartFailure_PublishesOrderRejected()
        {
            var world = new SimulationWorld();
            var loop = new SimulationLoop(world);
            var entity = world.Entities.CreateCharacter(new DefinitionId("base", "a")).Value;
            entity.Get<LifecycleComponent>().State = LifecycleState.Incapacitated;

            loop.EnqueueOrder(loop.CreateWaitOrder(entity.Id, 2, OrderSource.Ai));
            var events = world.Events.Drain();
            Assert.IsTrue(events.Exists(e => e.Type == EventType.OrderRejected));
        }
    }
}
