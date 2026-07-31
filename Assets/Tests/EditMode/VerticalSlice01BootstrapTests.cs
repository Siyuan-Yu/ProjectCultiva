using System.IO;
using System.Linq;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Events;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Data.Bootstrap;
using XianXia.Data.Serialization;

namespace XianXia.Tests
{
    public sealed class VerticalSlice01BootstrapTests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        [Test]
        public void GameStart_CreatesWorldWithLayout()
        {
            var result = new ContentGameStart().StartVerticalSlice01(BaseGamePath);
            Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error.ToString() : "");
            var world = result.Value.World;
            Assert.IsNotNull(world.WorldLayout);
            Assert.AreEqual(1, world.WorldLayout.Regions.Count);
            Assert.AreEqual(1, world.WorldLayout.LocalMaps.Count);
            Assert.AreEqual(1, world.WorldLayout.Settlements.Count);
            Assert.AreEqual(new RegionId(1), world.RegionId);
        }

        [Test]
        public void GameStart_CreatesThreeCharacterEntities()
        {
            var result = new ContentGameStart().StartVerticalSlice01(BaseGamePath);
            Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error.ToString() : "");
            Assert.AreEqual(3, result.Value.CharacterIds.Count);
            Assert.AreEqual(3, result.Value.World.Entities.Count);

            Assert.IsTrue(result.Value.World.Entities.All.Any(e =>
                e.DefinitionId.Equals(new DefinitionId("base", "character_protagonist"))));
            Assert.IsTrue(result.Value.World.Entities.All.Any(e =>
                e.DefinitionId.Equals(new DefinitionId("base", "character_companion_a"))));
            Assert.IsTrue(result.Value.World.Entities.All.Any(e =>
                e.DefinitionId.Equals(new DefinitionId("base", "character_companion_b"))));
        }

        [Test]
        public void GameStart_PublishesInitEvents()
        {
            var result = new ContentGameStart().StartVerticalSlice01(BaseGamePath);
            Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error.ToString() : "");
            var events = result.Value.World.Events.Drain();
            Assert.AreEqual(3, events.Count(e => e.Type == EventType.EntityCreated));
            Assert.AreEqual(1, events.Count(e => e.Type == EventType.WorldInitialized));
        }

        [Test]
        public void GameStart_Snapshot_SaveAndRestore_KeepsCharacters()
        {
            var started = new ContentGameStart().StartVerticalSlice01(BaseGamePath);
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");
            var world = started.Value.World;
            var loop = new SimulationLoop(world);

            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, loop);
            Assert.IsTrue(json.IsSuccess, json.IsFailure ? json.Error.ToString() : "");

            var restored = service.RestoreJson(json.Value, expectedPackageVersion: world.EnabledPackageVersion);
            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : "");
            Assert.AreEqual(3, restored.Value.Item1.Entities.Count);
            Assert.IsTrue(restored.Value.Item1.Entities.All.Any(e =>
                e.DefinitionId.Equals(new DefinitionId("base", "character_protagonist"))));
        }
    }
}
