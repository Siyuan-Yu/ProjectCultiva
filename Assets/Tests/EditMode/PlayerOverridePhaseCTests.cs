using NUnit.Framework;
using XianXia.Core.Actions;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Input;
using XianXia.Core.Labor;
using XianXia.Core.Orders;
using XianXia.Core.Persistence;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;
using XianXia.Data.Serialization;

namespace XianXia.Tests
{
    public sealed class PlayerOverridePhaseCTests
    {
        static (SimulationWorld world, SimulationLoop loop, Entity entity, PlayerInputPort port) CreateLaboringWorld()
        {
            var world = new SimulationWorld();
            var schedule = new ScheduleDefinition("test:labor_day")
                .AddBlock(0, 96, ScheduleActivity.Labor, 8);
            world.RegisterSchedule(schedule);
            var loop = new SimulationLoop(world);
            var entity = world.Entities.CreateCharacter(new DefinitionId("base", "character_protagonist"), "甲").Value;
            Assert.IsTrue(entity.AddComponent(new ScheduleComponent(schedule.Id)).IsSuccess);
            entity.Get<DailyTaskComponent>().RequiredAmount = 10;

            // Enter schedule Labor
            loop.TickOnce();
            Assert.IsInstanceOf<LaborAction>(FirstActive(world));
            Assert.AreEqual(OrderSource.Schedule, entity.Get<ActionStateComponent>().ActiveOrderSource);

            return (world, loop, entity, new PlayerInputPort(loop));
        }

        [Test]
        public void Schedule_AutoEntersLabor()
        {
            var (world, _, entity, _) = CreateLaboringWorld();
            Assert.IsInstanceOf<LaborAction>(FirstActive(world));
            Assert.AreEqual(OrderSource.Schedule, entity.Get<ActionStateComponent>().ActiveOrderSource);
        }

        [Test]
        public void PlayerOrder_InterruptsScheduleLabor()
        {
            var (world, loop, entity, port) = CreateLaboringWorld();
            loop.TickOnce(); // progress some labor
            Assert.Greater(entity.Get<DailyTaskComponent>().CompletedAmount, 0);

            Assert.IsTrue(port.Submit(new PlayerCommandRequest(entity.Id, PlayerCommandKind.Rest, 3)).IsSuccess);

            var events = world.Events.Drain();
            Assert.IsTrue(events.Exists(e =>
                e.Type == EventType.ScheduleInterrupted &&
                e.Payload == SimulationLoop.OverrideByPlayerReason));
            Assert.IsNotInstanceOf<LaborAction>(FirstActive(world));
            Assert.IsInstanceOf<RestAction>(FirstActive(world));
        }

        [Test]
        public void PlayerAction_RunsAfterOverride()
        {
            var (world, _, entity, port) = CreateLaboringWorld();
            Assert.IsTrue(port.Submit(new PlayerCommandRequest(entity.Id, PlayerCommandKind.Rest, 3)).IsSuccess);
            Assert.IsInstanceOf<RestAction>(FirstActive(world));
            Assert.AreEqual(OrderSource.Player, entity.Get<ActionStateComponent>().ActiveOrderSource);
        }

        [Test]
        public void IncompleteScheduleLabor_CreatesQuotaDeviation()
        {
            var (world, loop, entity, port) = CreateLaboringWorld();
            loop.TickOnce();
            var completedBefore = entity.Get<DailyTaskComponent>().CompletedAmount;
            Assert.Greater(completedBefore, 0);
            Assert.AreEqual(0, entity.Get<DailyTaskComponent>().Deviation);

            Assert.IsTrue(port.Submit(new PlayerCommandRequest(entity.Id, PlayerCommandKind.Rest, 2)).IsSuccess);

            var daily = entity.Get<DailyTaskComponent>();
            Assert.Greater(daily.Deviation, 0);
            Assert.AreEqual(completedBefore, daily.CompletedAmount);

            var events = world.Events.Drain();
            Assert.IsTrue(events.Exists(e => e.Type == EventType.QuotaDeviationCreated));
            Assert.IsTrue(events.Exists(e => e.Type == EventType.ScheduleInterrupted));
        }

        [Test]
        public void Snapshot_RestoresDeviationAndOverrideState()
        {
            var (world, loop, entity, port) = CreateLaboringWorld();
            loop.TickOnce();
            Assert.IsTrue(port.Submit(new PlayerCommandRequest(entity.Id, PlayerCommandKind.Rest, 4)).IsSuccess);
            var deviation = entity.Get<DailyTaskComponent>().Deviation;
            Assert.Greater(deviation, 0);

            loop.TickOnce();
            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, loop);
            Assert.IsTrue(json.IsSuccess, json.IsFailure ? json.Error.ToString() : "");

            var restored = service.RestoreJson(json.Value, expectedPackageVersion: world.EnabledPackageVersion);
            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : "");
            var (world2, _) = restored.Value;

            Entity e2 = null;
            foreach (var e in world2.Entities.All)
                e2 = e;
            Assert.IsNotNull(e2);
            Assert.AreEqual(deviation, e2.Get<DailyTaskComponent>().Deviation);
            Assert.AreEqual(OrderSource.Player, e2.Get<ActionStateComponent>().ActiveOrderSource);
            Assert.IsInstanceOf<RestAction>(FirstActive(world2));
        }

        static IAction FirstActive(SimulationWorld world)
        {
            foreach (var a in world.ActiveActions.Values)
                return a;
            return null;
        }
    }
}
