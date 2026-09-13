using System.Text.RegularExpressions;
using NUnit.Framework;
using XianXia.Core.Exploration;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Data.Content;
using XianXia.Data.Serialization;

namespace XianXia.Tests.EditMode
{
    public sealed class OutdoorStatefulObjectClosureTests
    {
        [Test]
        public void JsonRoundtrip_PreservesDestroyedHpAndFarmState()
        {
            var world = new SimulationWorld();
            world.OutdoorStatefulObjects.SetDestructible("base:wall:0:0", 0, true);
            world.OutdoorStatefulObjects.SetFarmPlot("base:field:11:7", "base:crop_herb", 2, .75f);
            var service = new SnapshotService(new JsonSnapshotSerializer());

            var json = service.CaptureJson(world, new SimulationLoop(world));
            Assert.IsTrue(json.IsSuccess, json.IsFailure ? json.Error.ToString() : string.Empty);
            StringAssert.Contains("\"outdoorDestructibles\"", json.Value);
            StringAssert.Contains("\"outdoorFarmPlots\"", json.Value);

            var restored = service.RestoreJson(json.Value);
            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : string.Empty);
            Assert.IsTrue(restored.Value.world.OutdoorStatefulObjects.TryGetDestructible(
                "base:wall:0:0", out var wall));
            Assert.AreEqual(0, wall.Hp);
            Assert.IsTrue(wall.Destroyed);
            Assert.IsTrue(restored.Value.world.OutdoorStatefulObjects.TryGetFarmPlot(
                "base:field:11:7", out var farm));
            Assert.AreEqual("base:crop_herb", farm.CropId);
            Assert.AreEqual(2, farm.CropStage);
            Assert.AreEqual(.75f, farm.Growth, .0001f);

            restored.Value.world.OutdoorStatefulObjects.SetDestructible("base:wall:2:0", 0, true);
            restored.Value.world.OutdoorStatefulObjects.SetFarmPlot(
                "base:field:11:7", "base:crop_grain", 1, .25f);
            var secondJson = service.CaptureJson(restored.Value.world, restored.Value.loop);
            Assert.IsTrue(secondJson.IsSuccess,
                secondJson.IsFailure ? secondJson.Error.ToString() : string.Empty);
            var secondRestore = service.RestoreJson(secondJson.Value);
            Assert.IsTrue(secondRestore.IsSuccess,
                secondRestore.IsFailure ? secondRestore.Error.ToString() : string.Empty);
            Assert.IsTrue(secondRestore.Value.world.OutdoorStatefulObjects.IsDestructibleDestroyed(
                "base:wall:2:0"));
            Assert.IsTrue(secondRestore.Value.world.OutdoorStatefulObjects.TryGetFarmPlot(
                "base:field:11:7", out var modifiedFarm));
            Assert.AreEqual("base:crop_grain", modifiedFarm.CropId);
            Assert.AreEqual(1, modifiedFarm.CropStage);
            Assert.AreEqual(.25f, modifiedFarm.Growth, .0001f);
        }

        [Test]
        public void OlderSnapshotWithoutOutdoorArrays_LoadsWithEmptyOverrideBoard()
        {
            var world = new SimulationWorld();
            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, new SimulationLoop(world));
            Assert.IsTrue(json.IsSuccess, json.IsFailure ? json.Error.ToString() : string.Empty);
            var legacyJson = Regex.Replace(json.Value,
                ",\"outdoorDestructibles\":\\[[^\\]]*\\]", string.Empty);
            legacyJson = Regex.Replace(legacyJson,
                ",\"outdoorFarmPlots\":\\[[^\\]]*\\]", string.Empty);

            var restored = service.RestoreJson(legacyJson);

            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : string.Empty);
            Assert.AreEqual(0, restored.Value.world.OutdoorStatefulObjects.Destructibles.Count);
            Assert.AreEqual(0, restored.Value.world.OutdoorStatefulObjects.FarmPlots.Count);
        }

        [Test]
        public void WallTombstone_DisablesOnlyItsStableAuthoredCell()
        {
            var placement = new OutdoorSurfacePlacementDefinition
            {
                StableId = "base:site_test:wall_north",
                Kind = "wall",
                BlocksMovement = true,
                SourceCellsW = 3,
                SourceCellsH = 1
            };
            var board = new OutdoorStatefulObjectBoard();
            var middleId = OutdoorStatefulObjectId.ForCell(placement.StableId, 1, 0);
            Assert.AreEqual(middleId, OutdoorStatefulPlacementResolver.ResolveObjectId(placement, 1, 0));

            board.SetDestructible(middleId, 0, true);

            Assert.IsTrue(OutdoorStatefulPlacementResolver.IsBlockerActive(board, placement, 0, 0));
            Assert.IsFalse(OutdoorStatefulPlacementResolver.IsBlockerActive(board, placement, 1, 0));
            Assert.IsTrue(OutdoorStatefulPlacementResolver.IsBlockerActive(board, placement, 2, 0));
            Assert.AreEqual(middleId, OutdoorStatefulPlacementResolver.ResolveObjectId(placement, 1, 0),
                "Chunk load order must not participate in authored cell identity.");
        }

        [Test]
        public void TopologyRevision_ChangesOnlyWhenCollisionChanges()
        {
            var board = new OutdoorStatefulObjectBoard();
            board.SetDestructible("wall:0:0", 80, false);
            var baseline = board.DestructibleTopologyRevision;
            board.SetDestructible("wall:0:0", 40, false);
            Assert.AreEqual(baseline, board.DestructibleTopologyRevision);

            board.SetDestructible("wall:0:0", 0, true);
            Assert.AreEqual(baseline + 1, board.DestructibleTopologyRevision);
            board.SetDestructible("wall:0:0", 0, true);
            Assert.AreEqual(baseline + 1, board.DestructibleTopologyRevision);

            board.SetDestructible("wall:0:0", 80, false);
            Assert.AreEqual(baseline + 2, board.DestructibleTopologyRevision);
        }
    }
}
