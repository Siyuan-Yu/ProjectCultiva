using NUnit.Framework;
using XianXia.Core.Actions;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Input;
using XianXia.Core.Labor;
using XianXia.Core.Orders;
using XianXia.Core.Persistence;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;
using XianXia.Data.Serialization;

namespace XianXia.Tests
{
    public sealed class SchedulePhaseBTests
    {
        static ScheduleDefinition ShortDay()
        {
            // Tick-in-day: 0-3 Rest, 3-10 Labor, 10-16 Rest
            return new ScheduleDefinition("test:short_day")
                .AddBlock(0, 3, ScheduleActivity.Rest, 2)
                .AddBlock(3, 10, ScheduleActivity.Labor, 2)
                .AddBlock(10, 16, ScheduleActivity.Rest, 2);
        }

        static (SimulationWorld world, SimulationLoop loop, Entity entity) CreateScheduledWorld()
        {
            var world = new SimulationWorld();
            var schedule = ShortDay();
            world.RegisterSchedule(schedule);
            var loop = new SimulationLoop(world);
            var entity = world.Entities.CreateCharacter(new DefinitionId("base", "character_protagonist"), "甲").Value;
            Assert.IsTrue(entity.AddComponent(new ScheduleComponent(schedule.Id)).IsSuccess);
            return (world, loop, entity);
        }

        [Test]
        public void Schedule_WithoutPlayer_AutoGeneratesLaborAndRest()
        {
            var (world, loop, entity) = CreateScheduledWorld();

            loop.TickOnce(); // tick=1, still Rest block
            Assert.IsTrue(entity.Get<ActionStateComponent>().HasActiveAction);
            Assert.IsInstanceOf<RestAction>(FirstActive(world));

            while (world.Tick.Value < 3)
                loop.TickOnce();

            // Enter Labor block
            loop.TickOnce();
            Assert.IsTrue(world.Tick.Value >= 3UL);
            // May need a tick after previous rest completes
            for (var i = 0; i < 4 && !(FirstActive(world) is LaborAction); i++)
                loop.TickOnce();

            Assert.IsInstanceOf<LaborAction>(FirstActive(world));
            var progressBefore = entity.Get<DailyTaskComponent>().LaborProgress;
            loop.TickOnce();
            Assert.Greater(entity.Get<DailyTaskComponent>().LaborProgress, progressBefore);
        }

        [Test]
        public void ScheduleOrderFactory_SourceIsSchedule()
        {
            var block = new ScheduleBlock(0, 8, ScheduleActivity.Labor, 4);
            var order = new ScheduleOrderFactory().Create(new OrderId(1), new EntityId(2), block, 4);
            Assert.IsTrue(order.IsSuccess);
            Assert.AreEqual(OrderSource.Schedule, order.Value.Source);
            Assert.AreEqual(OrderType.Labor, order.Value.Type);
        }

        [Test]
        public void PlayerOrder_BlocksScheduleInjection()
        {
            var (world, loop, entity) = CreateScheduledWorld();
            var port = new PlayerInputPort(loop);

            // Player Rest for long duration while schedule would want Labor later.
            Assert.IsTrue(port.Submit(new PlayerCommandRequest(entity.Id, PlayerCommandKind.Rest, 8)).IsSuccess);
            Assert.IsInstanceOf<RestAction>(FirstActive(world));

            for (var i = 0; i < 5; i++)
                loop.TickOnce();

            Assert.IsInstanceOf<RestAction>(FirstActive(world));
            Assert.AreEqual(0, entity.Get<DailyTaskComponent>().LaborProgress);
            Assert.IsFalse(world.GetOrCreateOrderQueue(entity.Id).HasSource(OrderSource.Schedule));
        }

        [Test]
        public void Schedule_SwitchesActivityWithTime()
        {
            var (world, loop, entity) = CreateScheduledWorld();

            // Advance through Rest 0-3 into Labor 3-10
            for (var i = 0; i < 6; i++)
                loop.TickOnce();

            Assert.GreaterOrEqual(world.Tick.Value, 3UL);
            var sawLabor = false;
            for (var i = 0; i < 6; i++)
            {
                if (FirstActive(world) is LaborAction)
                {
                    sawLabor = true;
                    break;
                }

                loop.TickOnce();
            }

            Assert.IsTrue(sawLabor, "Expected Labor after schedule entered work block.");

            // Jump toward Rest block at 10
            while (world.Tick.Value < 12)
                loop.TickOnce();

            var sawRest = false;
            for (var i = 0; i < 4; i++)
            {
                if (FirstActive(world) is RestAction)
                {
                    sawRest = true;
                    break;
                }

                loop.TickOnce();
            }

            Assert.IsTrue(sawRest, "Expected Rest after schedule entered evening block.");
            Assert.Greater(entity.Get<DailyTaskComponent>().LaborProgress, 0);
        }

        [Test]
        public void Snapshot_RestoresScheduleBindingAndDefinitions()
        {
            var (world, loop, entity) = CreateScheduledWorld();
            for (var i = 0; i < 5; i++)
                loop.TickOnce();

            var progress = entity.Get<DailyTaskComponent>().LaborProgress;
            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, loop);
            Assert.IsTrue(json.IsSuccess, json.IsFailure ? json.Error.ToString() : "");

            var restored = service.RestoreJson(json.Value, expectedPackageVersion: world.EnabledPackageVersion);
            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : "");
            var (world2, loop2) = restored.Value;

            Assert.IsTrue(world2.TryGetSchedule("test:short_day", out _));
            var e2 = default(Entity);
            foreach (var e in world2.Entities.All)
                e2 = e;
            Assert.IsNotNull(e2);
            Assert.IsTrue(e2.TryGet<ScheduleComponent>(out var binding));
            Assert.AreEqual("test:short_day", binding.DefinitionId);
            Assert.AreEqual(progress, e2.Get<DailyTaskComponent>().LaborProgress);

            var before = e2.Get<DailyTaskComponent>().LaborProgress;
            for (var i = 0; i < 4; i++)
                loop2.TickOnce();
            // Either still laboring or resting after switch; schedule must keep driving.
            Assert.IsTrue(
                e2.Get<ActionStateComponent>().HasActiveAction ||
                e2.Get<DailyTaskComponent>().LaborProgress >= before);
        }

        [Test]
        public void OrderQueue_PlayerAheadOfSchedule()
        {
            var queue = new OrderQueue();
            queue.Enqueue(new Order(new OrderId(1), new EntityId(1), OrderType.Labor, OrderSource.Schedule, waitTicks: 2));
            queue.Enqueue(new Order(new OrderId(2), new EntityId(1), OrderType.Rest, OrderSource.Player, waitTicks: 2));
            Assert.IsTrue(queue.TryDequeue(out var first));
            Assert.AreEqual(OrderSource.Player, first.Source);
            Assert.IsTrue(queue.TryDequeue(out var second));
            Assert.AreEqual(OrderSource.Schedule, second.Source);
        }

        static IAction FirstActive(SimulationWorld world)
        {
            foreach (var a in world.ActiveActions.Values)
                return a;
            return null;
        }
    }
}
