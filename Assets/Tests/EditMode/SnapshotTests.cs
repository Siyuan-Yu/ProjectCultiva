using System.IO;
using NUnit.Framework;
using XianXia.Core.Attributes;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Persistence;
using XianXia.Core.Random;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Data.Serialization;

namespace XianXia.Tests
{
    public sealed class SnapshotTests
    {
        [Test]
        public void WaitAction_Midway_Snapshot_Restores_And_Completes_SameTick()
        {
            var world = new SimulationWorld(random: new DeterministicRandom(42));
            var loop = new SimulationLoop(world);
            var entity = world.Entities.CreateCharacter(new DefinitionId("base", "character_labor_disciple")).Value;
            loop.EnqueueOrder(loop.CreateWaitOrder(entity.Id, 4));
            loop.TickOnce();
            loop.TickOnce();
            Assert.AreEqual(2UL, world.Tick.Value);
            Assert.AreEqual(2UL, entity.Get<ActionStateComponent>().ActiveClock.Value.RemainingTicks);

            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, loop);
            Assert.IsTrue(json.IsSuccess, json.IsFailure ? json.Error.ToString() : "");

            var restored = service.RestoreJson(json.Value);
            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : "");
            var (world2, loop2) = restored.Value;
            Assert.AreEqual(2UL, world2.Tick.Value);

            loop2.TickOnce();
            loop2.TickOnce();
            Assert.AreEqual(4UL, world2.Tick.Value);
            var e2 = System.Linq.Enumerable.First(world2.Entities.All);
            Assert.IsFalse(e2.Get<ActionStateComponent>().HasActiveAction);
            var events = world2.Events.Drain();
            Assert.IsTrue(events.Exists(ev => ev.Type == EventType.ActionCompleted && ev.Tick.Value == 4UL));
        }

        [Test]
        public void ModifierFinal_And_Prng_RestoreConsistent()
        {
            var world = new SimulationWorld(random: new DeterministicRandom(7));
            var loop = new SimulationLoop(world);
            var entity = world.Entities.CreateCharacter(new DefinitionId("base", "a")).Value;
            var attrs = entity.Get<AttributesComponent>();
            attrs.SetBase(AttributeId.Attack, 100);
            attrs.AddModifier(AttributeId.Attack, ModifierOperation.Fixed, 10, new SourceRef(SourceKind.Equipment));
            attrs.AddModifier(AttributeId.Attack, ModifierOperation.Percentage, 0.20, new SourceRef(SourceKind.SpiritRoot));
            attrs.AddModifier(AttributeId.Attack, ModifierOperation.Percentage, 0.30, new SourceRef(SourceKind.Manual));
            Assert.AreEqual(165, attrs.GetFinal(AttributeId.Attack));

            world.Random.NextInt(0, 100);
            var state = world.Random.CaptureState();
            var expected = new DeterministicRandom(1);
            expected.RestoreState(state);
            var expectedDraw = expected.NextInt(0, 1000);

            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, loop).Value;
            var (world2, _) = service.RestoreJson(json).Value;
            var e2 = System.Linq.Enumerable.First(world2.Entities.All);
            Assert.AreEqual(165, e2.Get<AttributesComponent>().GetFinal(AttributeId.Attack));
            Assert.AreEqual(expectedDraw, world2.Random.NextInt(0, 1000));
        }

        [Test]
        public void ContentVersionMismatch_Fails()
        {
            var world = new SimulationWorld();
            world.EnabledPackageVersion = "0.0.1-m1";
            var loop = new SimulationLoop(world);
            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, loop).Value;
            var result = service.RestoreJson(json, expectedPackageVersion: "9.9.9");
            Assert.IsTrue(result.IsFailure);
            Assert.AreEqual(ErrorCode.IncompatibleContentVersion, result.Error.Code);
        }
    }
}
