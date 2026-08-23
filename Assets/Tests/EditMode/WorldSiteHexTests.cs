using NUnit.Framework;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    public sealed class WorldSiteHexTests
    {
        [Test]
        public void SITE01_WorldSitePlacedOnHex()
        {
            var world = new SimulationWorld();
            world.HexWorld.FillRectangle(6, 6);
            var site = new WorldSite
            {
                SiteId = "base:site_test",
                DisplayName = "Test Village",
                AnchorHex = new HexCoord(2, 3),
            };
            site.SetFootprint(new[] { new HexCoord(2, 3) });
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, site);

            Assert.IsTrue(world.Strategic.Sites.TryGet("base:site_test", out var loaded));
            Assert.AreEqual(new HexCoord(2, 3), loaded.AnchorHex);
            Assert.IsTrue(world.HexWorld.TryGetTile(new HexCoord(2, 3), out var tile));
            Assert.AreEqual("base:site_test", tile.WorldSiteId);
        }

        [Test]
        public void SITE02_SiteOwnerPreservedFromNodeMigration()
        {
            var world = new SimulationWorld();
            world.HexWorld.FillRectangle(4, 4);
            world.WorldGraph.RegisterNode(new WorldNodeState
            {
                Id = "base:node_huangcun",
                Name = "青石荒村",
                OwnerId = "base:sect_huangcun_labor",
                LocalMapId = "base:map_huangcun",
            });

            var site = WorldSiteRegistrationService.CreateSiteFromNode(
                world.WorldGraph.Nodes["base:node_huangcun"],
                new HexCoord(1, 1));
            site.SetFootprint(new[] { new HexCoord(1, 1) });
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, site);

            Assert.AreEqual("base:sect_huangcun_labor", site.OwnerFactionId);
            Assert.AreEqual("base:map_huangcun", site.LocalMapId);
            Assert.AreEqual("base:node_huangcun", site.LegacyNodeId);
        }

        [Test]
        public void SITE03_SiteLocalMapMappingPreserved()
        {
            var world = new SimulationWorld();
            world.HexWorld.FillRectangle(4, 4);
            world.WorldGraph.RegisterNode(new WorldNodeState
            {
                Id = "base:node_qingyun_lu",
                Name = "青石路",
                LocalMapId = "base:map_qingyun_lu",
            });

            var site = WorldSiteRegistrationService.CreateSiteFromNode(
                world.WorldGraph.Nodes["base:node_qingyun_lu"],
                new HexCoord(3, 2));
            site.SetFootprint(new[] { new HexCoord(3, 2) });
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, site);

            Assert.IsTrue(world.Strategic.Sites.TryGet(site.SiteId, out var loaded));
            Assert.AreEqual("base:map_qingyun_lu", loaded.LocalMapId);
        }

        [Test]
        public void SITE04_MultiHexFootprintRegistersAllCells()
        {
            var world = new SimulationWorld();
            world.HexWorld.FillRectangle(8, 8);
            var anchor = new HexCoord(3, 3);
            var site = new WorldSite
            {
                SiteId = "base:site_city",
                DisplayName = "Test City",
                SiteType = "City",
                AnchorHex = anchor,
            };
            site.SetFootprint(new[]
            {
                anchor,
                HexMath.Neighbor(anchor, 0),
                HexMath.Neighbor(anchor, 1),
            });
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, site);

            Assert.IsTrue(world.Strategic.Sites.TryGetAtHex(anchor, out var atAnchor));
            Assert.AreEqual("base:site_city", atAnchor.SiteId);
            Assert.IsTrue(world.Strategic.Sites.TryGetAtHex(HexMath.Neighbor(anchor, 0), out _));
        }
    }
}
