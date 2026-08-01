using NUnit.Framework;
using XianXia.Core.Actions;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Input;
using XianXia.Core.Labor;
using XianXia.Core.Orders;
using XianXia.Core.Simulation;

namespace XianXia.Tests
{
    public sealed class PlayerInputPhaseATests
    {
        [Test]
        public void PlayerInput_Labor_GoesThroughOrderSourcePlayer_AndAdvancesProgress()
        {
            var world = new SimulationWorld();
            var loop = new SimulationLoop(world);
            var port = new PlayerInputPort(loop);
            var entity = world.Entities.CreateCharacter(new DefinitionId("base", "character_protagonist"), "主角").Value;

            var submit = port.Submit(new PlayerCommandRequest(entity.Id, PlayerCommandKind.Labor, 4));
            Assert.IsTrue(submit.IsSuccess, submit.IsFailure ? submit.Error.ToString() : "");
            Assert.IsTrue(entity.Get<ActionStateComponent>().HasActiveAction);

            IAction active = null;
            foreach (var a in world.ActiveActions.Values)
                active = a;
            Assert.IsInstanceOf<LaborAction>(active);

            for (var i = 0; i < 4; i++)
                loop.TickOnce();

            Assert.AreEqual(4UL, world.Tick.Value);
            Assert.AreEqual(4, entity.Get<DailyTaskComponent>().LaborProgress);
            Assert.IsFalse(entity.Get<ActionStateComponent>().HasActiveAction);
            Assert.IsTrue(world.Events.Drain().Exists(e => e.Type == EventType.ActionCompleted));
        }

        [Test]
        public void PlayerOrderFactory_CreatesOrderWithSourcePlayer()
        {
            var factory = new PlayerOrderFactory();
            var order = factory.Create(
                new OrderId(1),
                new PlayerCommandRequest(new EntityId(9), PlayerCommandKind.Labor, 3));
            Assert.IsTrue(order.IsSuccess);
            Assert.AreEqual(OrderSource.Player, order.Value.Source);
            Assert.AreEqual(OrderType.Labor, order.Value.Type);
            Assert.AreEqual(3UL, order.Value.WaitTicks);
        }

        [Test]
        public void PlayerInput_MissingEntity_Fails()
        {
            var world = new SimulationWorld();
            var loop = new SimulationLoop(world);
            var port = new PlayerInputPort(loop);
            var result = port.Submit(new PlayerCommandRequest(new EntityId(99), PlayerCommandKind.Labor, 2));
            Assert.IsTrue(result.IsFailure);
        }

        [Test]
        public void PlayerInput_Observe_RejectedInPhaseA()
        {
            var world = new SimulationWorld();
            var loop = new SimulationLoop(world);
            var port = new PlayerInputPort(loop);
            var entity = world.Entities.CreateCharacter(new DefinitionId("base", "a")).Value;
            var result = port.Submit(new PlayerCommandRequest(entity.Id, PlayerCommandKind.Observe, 2));
            Assert.IsTrue(result.IsFailure);
        }

        [Test]
        public void PlayerInput_Rest_CompletesViaSameBridge()
        {
            var world = new SimulationWorld();
            var loop = new SimulationLoop(world);
            var port = new PlayerInputPort(loop);
            var entity = world.Entities.CreateCharacter(new DefinitionId("base", "a")).Value;
            Assert.IsTrue(port.Submit(new PlayerCommandRequest(entity.Id, PlayerCommandKind.Rest, 2)).IsSuccess);
            loop.TickOnce();
            loop.TickOnce();
            Assert.AreEqual(2UL, world.Tick.Value);
            Assert.IsFalse(entity.Get<ActionStateComponent>().HasActiveAction);
        }
    }
}
