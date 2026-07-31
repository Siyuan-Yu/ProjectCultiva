using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using XianXia.Core;
using XianXia.Core.Attributes;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Persistence;
using XianXia.Core.Random;
using XianXia.Core.Simulation;
using XianXia.Data;
using XianXia.Data.Content;
using XianXia.Data.Serialization;

namespace XianXia.Tests
{
    public sealed class CoreM1IntegrationTests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        [Test]
        public void CoreM1_SingleRegion_LogicLoop_Smoke()
        {
            var loaded = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : "");
            Assert.IsTrue(loaded.Value.Registry.TryGetCharacter(
                new DefinitionId("base", "character_labor_disciple"), out var def));

            var world = new SimulationWorld(random: new DeterministicRandom(123));
            world.EnabledPackageId = loaded.Value.Manifests[0].ModId;
            world.EnabledPackageVersion = loaded.Value.Manifests[0].Version.Value;
            var loop = new SimulationLoop(world);

            var created = world.Entities.CreateCharacter(def.Id, "劳役甲");
            Assert.IsTrue(created.IsSuccess);
            var entity = created.Value;
            world.Events.Publish(EventType.EntityCreated, world.Tick, target: entity.Id);

            var attrs = entity.Get<AttributesComponent>();
            foreach (var kv in def.BaseAttributes)
                attrs.SetBase(kv.Key, kv.Value);
            Assert.AreEqual(100, attrs.GetFinal(AttributeId.MaxHp));

            loop.EnqueueOrder(loop.CreateWaitOrder(entity.Id, 4));
            loop.TickOnce();
            loop.TickOnce();

            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, loop);
            Assert.IsTrue(json.IsSuccess);

            var prngExpectedState = world.Random.CaptureState();
            var probe = new DeterministicRandom(1, prngExpectedState.StreamId);
            probe.RestoreState(prngExpectedState);
            var expectedNext = probe.NextInt(0, 500);

            var restored = service.RestoreJson(json.Value, expectedPackageVersion: world.EnabledPackageVersion);
            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : "");
            var (world2, loop2) = restored.Value;

            loop2.TickOnce();
            loop2.TickOnce();
            Assert.AreEqual(4UL, world2.Tick.Value);
            Assert.IsTrue(world2.Events.Drain().Exists(e => e.Type == EventType.ActionCompleted));
            Assert.AreEqual(expectedNext, world2.Random.NextInt(0, 500));
        }

        [Test]
        public void Core_And_Data_Assemblies_Still_Avoid_UnityEngine()
        {
            AssertNoUnity(typeof(CoreAssemblyMarker).Assembly, "XianXia.Core");
            AssertNoUnity(typeof(DataAssemblyMarker).Assembly, "XianXia.Data");
        }

        static void AssertNoUnity(Assembly assembly, string label)
        {
            var hits = assembly.GetReferencedAssemblies()
                .Select(a => a.Name)
                .Where(n => n == "UnityEngine" || n.StartsWith("UnityEngine.") || n == "UnityEditor" || n.StartsWith("UnityEditor."))
                .ToArray();
            Assert.IsEmpty(hits, label + " refs: " + string.Join(",", hits));
        }
    }
}
