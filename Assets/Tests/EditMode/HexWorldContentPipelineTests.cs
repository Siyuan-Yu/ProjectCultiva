using System.Linq;
using NUnit.Framework;
using XianXia.Core.World;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Tests
{
    public sealed class HexWorldContentPipelineTests
    {
        [Test]
        public void WGE05_TerrainRoundtrip_FromBuilderExportShape()
        {
            var world = new SimulationWorld();
            Ch01HexPrototypeMapBuilder.Build(world);

            var definition = HexWorldContentExporter.Export(world);
            var loaded = new SimulationWorld();
            Assert.IsTrue(HexWorldContentLoader.Apply(loaded, definition).IsSuccess);
            Assert.AreEqual(world.HexWorld.Width, loaded.HexWorld.Width);
            Assert.AreEqual(world.HexWorld.Height, loaded.HexWorld.Height);
        }

        [Test]
        public void WGE_Runtime01_LoaderAppliesForestCell()
        {
            var definition = new HexWorldContentDefinition
            {
                Id = Core.Domain.Ids.DefinitionId.Parse("test:hex_world").Value,
                Name = "Test",
                Width = 10,
                Height = 10,
                DefaultTerrain = "Mountain",
                DefaultPassable = false,
            };
            for (var r = 0; r < 10; r++)
            for (var q = 0; q < 10; q++)
                definition.Cells.Add(new HexWorldCellDefinition { Q = q, R = r, Terrain = "Mountain", Passable = false });
            var forestCell = definition.Cells.First(c => c.Q == 4 && c.R == 4);
            forestCell.Terrain = "Forest";
            forestCell.Passable = true;
            definition.Sites.Add(new HexWorldSiteDefinition
            {
                SiteId = "test:site_village",
                DisplayName = "测试村",
                SiteType = "Village",
                AnchorQ = 5,
                AnchorR = 2,
                Footprint = { new HexWorldCoordDefinition { Q = 5, R = 2 } },
            });

            var world = new SimulationWorld();
            Assert.IsTrue(HexWorldContentLoader.Apply(world, definition).IsSuccess);
            Assert.IsTrue(world.HexWorld.TryGetCell(new HexCoord(4, 4), out var forest));
            Assert.AreEqual(HexTerrainType.Forest, forest.Terrain);
            Assert.IsTrue(world.Strategic.Sites.TryGet("test:site_village", out var site));
            Assert.AreEqual("测试村", site.DisplayName);
            Assert.AreEqual(new HexCoord(5, 2), site.AnchorHex);
        }

        [Test]
        public void WYSIWYG_05_SiteAnchorRoundtrip_AfterOfficialLoader()
        {
            var definition = BuildSampleDefinitionWithSite(42, 17, "test:site_probe", "探针村");
            var world = new SimulationWorld();
            Assert.IsTrue(HexWorldContentLoader.Apply(world, definition).IsSuccess);
            Assert.IsTrue(world.Strategic.Sites.TryGet("test:site_probe", out var site));
            Assert.AreEqual(new HexCoord(42, 17), site.AnchorHex);
        }

        [Test]
        public void WYSIWYG_07_RoadCellSetRoundtrip()
        {
            var definition = BuildSampleDefinitionWithSite(10, 10, "test:site_a", "A");
            var road = definition.Cells.First(c => c.Q == 11 && c.R == 10);
            road.Terrain = "Road";
            road.IsRoad = true;
            road.Passable = true;

            var world = new SimulationWorld();
            Assert.IsTrue(HexWorldContentLoader.Apply(world, definition).IsSuccess);
            Assert.IsTrue(world.HexWorld.TryGetCell(new HexCoord(11, 10), out var cell));
            Assert.IsTrue(cell.IsRoad);
            Assert.AreEqual(HexTerrainType.Road, cell.Terrain);
        }

        static HexWorldContentDefinition BuildSampleDefinitionWithSite(int q, int r, string siteId, string name)
        {
            var definition = new HexWorldContentDefinition
            {
                Id = Core.Domain.Ids.DefinitionId.Parse("test:hex_world").Value,
                Name = "Test",
                Width = 20,
                Height = 20,
                DefaultTerrain = "Mountain",
                DefaultPassable = false,
            };
            for (var row = 0; row < 20; row++)
            for (var col = 0; col < 20; col++)
                definition.Cells.Add(new HexWorldCellDefinition { Q = col, R = row, Terrain = "Mountain", Passable = false });
            definition.Sites.Add(new HexWorldSiteDefinition
            {
                SiteId = siteId,
                DisplayName = name,
                SiteType = "Village",
                AnchorQ = q,
                AnchorR = r,
                Footprint = { new HexWorldCoordDefinition { Q = q, R = r } },
            });
            return definition;
        }

        [Test]
        public void WGE11_DeterministicOrdering_OnExport()
        {
            var world = new SimulationWorld();
            Ch01HexPrototypeMapBuilder.BuildMinimalTwoSitePrototype(world);
            var a = HexWorldContentExporter.Export(world);
            var b = HexWorldContentExporter.Export(world);
            Assert.AreEqual(a.Cells.Count, b.Cells.Count);
            for (var i = 0; i < a.Cells.Count; i++)
            {
                Assert.AreEqual(a.Cells[i].Q, b.Cells[i].Q);
                Assert.AreEqual(a.Cells[i].R, b.Cells[i].R);
                Assert.AreEqual(a.Cells[i].Terrain, b.Cells[i].Terrain);
            }
        }
    }
}
