using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.World.Hex;
using XianXia.Data.Content;
using XianXia.Unity.Host;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Data.Serialization;

namespace XianXia.Tests.EditMode
{
    public sealed class MainWildernessSurfaceW1DTests
    {
        const string MainSurfaceId = "base:surface_main_wilderness_v1";
        const string TravelWorldId = "base:hex_world_travel_mvp_30x15";
        static string BaseGamePath => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        [Test]
        public void MainSurface_CoversEveryPassableOrdinaryWildernessHexCenter()
        {
            var loaded = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : string.Empty);
            var registry = loaded.Value.Registry;
            Assert.IsTrue(registry.TryGetOutdoorSurface(DefinitionId.Parse(MainSurfaceId).Value, out var surface));
            Assert.IsTrue(registry.TryGetHexWorldContent(DefinitionId.Parse(TravelWorldId).Value, out var world));

            var siteHexes = new HashSet<HexCoord>();
            foreach (var site in world.Sites)
                foreach (var hex in site.Footprint)
                    siteHexes.Add(new HexCoord(hex.Q, hex.R));
            var covered = 0;
            foreach (var cell in world.Cells)
            {
                var hex = new HexCoord(cell.Q, cell.R);
                if (cell.Passable != true || string.Equals(cell.Terrain, "Water", System.StringComparison.OrdinalIgnoreCase) || siteHexes.Contains(hex))
                    continue;
                HexMath.ToWorldPosition(hex, world.HexSize, out var x, out var y);
                Assert.IsTrue(OutdoorSurfaceCoverageResolver.ContainsWorldPosition(surface, x, y), hex.ToString());
                covered++;
            }
            Assert.Greater(covered, 0);
        }

        [Test]
        public void NormalResolver_ChoosesMainSurfaceAndExcludesAcceptanceOnlyOverlap()
        {
            var loaded = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : string.Empty);
            HexMath.ToWorldPosition(new HexCoord(1, 1), 1f, out var x, out var y);
            Assert.IsTrue(OutdoorSurfaceCoverageResolver.TryResolveAtWorldPosition(
                loaded.Value.Registry, x, y, out var resolved));
            Assert.AreEqual(MainSurfaceId, resolved.SurfaceId);
            Assert.IsFalse(resolved.AcceptanceOnly);
        }

        [Test]
        public void AllSevenOutdoorSites_HaveCheckedInRegionsPlacementsAndValidChunks()
        {
            var loaded = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : string.Empty);
            var registry = loaded.Value.Registry;
            Assert.IsTrue(registry.TryGetOutdoorSurface(DefinitionId.Parse(MainSurfaceId).Value, out var surface));
            Assert.AreEqual(7, surface.SiteRegions.Count);
            Assert.GreaterOrEqual(surface.SitePlacements.Count, 7);
            var ids = new HashSet<string>();
            foreach (var placement in surface.SitePlacements)
            {
                Assert.IsTrue(ids.Add(placement.StableId), placement.StableId);
                Assert.IsTrue(surface.Chunks.Exists(c => c.Coord == new XianXia.Core.World.Surface.SurfaceChunkCoord(
                    placement.ChunkX, placement.ChunkY)), placement.StableId);
            }
            foreach (var region in surface.SiteRegions)
            {
                Assert.IsTrue(surface.SitePlacements.Exists(p => p.SiteId == region.SiteId), region.SiteId);
                Assert.IsTrue(surface.SitePlaces.Exists(p => p.SiteId == region.SiteId), region.SiteId);
            }
            Assert.AreEqual(405, CountRenderedObjects(surface.SitePlacements));
        }

        [TestCase("road", 1, 1, 1)]
        [TestCase("wall", 6, 1, 6)]
        [TestCase("herbField", 12, 12, 144)]
        [TestCase("controlCore", 8, 8, 1)]
        [TestCase("zoneHousing", 12, 12, 1)]
        public void OutdoorPlacement_RenderCountUsesAuthoredCellsAndStampMode(
            string kind, int cellsW, int cellsH, int expected)
        {
            var placement = new OutdoorSurfacePlacementDefinition
            { Kind = kind, SourceCellsW = cellsW, SourceCellsH = cellsH };
            Assert.IsTrue(HostDemoTileMap.TryEstimateOutdoorRenderedObjectCount(placement, out var actual));
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void OutdoorStatefulObjects_AreIncludedInSnapshotJson()
        {
            var world = new SimulationWorld();
            world.OutdoorStatefulObjects.SetDestructible("base:wall:0:0", 37, false);
            world.OutdoorStatefulObjects.SetFarmPlot("base:field:11:7", "base:crop_herb", 2, .75f);
            var serializer = new JsonSnapshotSerializer();
            var snapshot = new SnapshotService(serializer).Capture(world, new SimulationLoop(world));
            var json = serializer.Serialize(snapshot);
            Assert.IsTrue(json.IsSuccess, json.IsFailure ? json.Error.ToString() : string.Empty);
            var decoded = serializer.Deserialize(json.Value);
            Assert.IsTrue(decoded.IsSuccess, decoded.IsFailure ? decoded.Error.ToString() : string.Empty);
            Assert.AreEqual("base:wall:0:0", decoded.Value.OutdoorDestructibles[0].StableId);
            Assert.AreEqual(37, decoded.Value.OutdoorDestructibles[0].CurrentHp);
            Assert.AreEqual("base:field:11:7", decoded.Value.OutdoorFarmPlots[0].StableCellId);
            Assert.AreEqual(.75f, decoded.Value.OutdoorFarmPlots[0].Growth, .0001f);

            var restored = new SnapshotService(serializer).RestoreJson(json.Value);
            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : string.Empty);
            Assert.IsTrue(restored.Value.world.OutdoorStatefulObjects.TryGetDestructible(
                "base:wall:0:0", out var restoredWall));
            Assert.AreEqual(37, restoredWall.Hp);
            Assert.IsFalse(restoredWall.Destroyed);
            Assert.IsTrue(restored.Value.world.OutdoorStatefulObjects.TryGetFarmPlot(
                "base:field:11:7", out var restoredFarm));
            Assert.AreEqual("base:crop_herb", restoredFarm.CropId);
            Assert.AreEqual(2, restoredFarm.CropStage);
            Assert.AreEqual(.75f, restoredFarm.Growth, .0001f);
        }

        static int CountRenderedObjects(IReadOnlyList<OutdoorSurfacePlacementDefinition> placements)
        {
            var count = 0;
            for (var i = 0; i < placements.Count; i++)
            {
                Assert.IsTrue(HostDemoTileMap.TryEstimateOutdoorRenderedObjectCount(placements[i], out var n),
                    placements[i].StableId);
                count += n;
            }
            return count;
        }
    }
}
