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
            world.LegacyHexWorld.FillRectangle(6, 6);
            var site = new WorldSite
            {
                SiteId = "base:site_test",
                DisplayName = "Test Village",
                LegacyAnchorHex = new HexCoord(2, 3),
            };
            site.SetLegacyHexFootprint(new[] { new HexCoord(2, 3) });
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, site);

            Assert.IsTrue(world.Strategic.Sites.TryGet("base:site_test", out var loaded));
            Assert.AreEqual(new HexCoord(2, 3), loaded.LegacyAnchorHex);
            Assert.IsTrue(world.LegacyHexWorld.TryGetTile(new HexCoord(2, 3), out var tile));
            Assert.AreEqual("base:site_test", tile.WorldSiteId);
        }

        [Test]
        public void SITE02_SiteRegistrationPreservesLocalMapId()
        {
            var world = new SimulationWorld();
            world.LegacyHexWorld.FillRectangle(4, 4);
            var site = new WorldSite
            {
                SiteId = "base:site_huangcun",
                DisplayName = "Huangcun",
                LegacyAnchorHex = new HexCoord(1, 1),
                LocalMapId = "base:map_huangcun",
            };
            site.SetLegacyHexFootprint(new[] { new HexCoord(1, 1) });
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, site);

            Assert.AreEqual(string.Empty, site.OwnerFactionId);
            Assert.AreEqual("base:map_huangcun", site.LocalMapId);
        }

        [Test]
        public void SITE03_SiteLocalMapMappingPreserved()
        {
            var world = new SimulationWorld();
            world.LegacyHexWorld.FillRectangle(4, 4);
            var site = new WorldSite
            {
                SiteId = "base:site_qingyun_lu",
                DisplayName = "Qingyun Lu",
                LegacyAnchorHex = new HexCoord(3, 2),
                LocalMapId = "base:map_qingyun_lu",
            };
            site.SetLegacyHexFootprint(new[] { new HexCoord(3, 2) });
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, site);

            Assert.IsTrue(world.Strategic.Sites.TryGet(site.SiteId, out var loaded));
            Assert.AreEqual("base:map_qingyun_lu", loaded.LocalMapId);
        }

        [Test]
        public void SITE04_MultiHexFootprintRegistersAllCells()
        {
            var world = new SimulationWorld();
            world.LegacyHexWorld.FillRectangle(8, 8);
            var anchor = new HexCoord(3, 3);
            var site = new WorldSite
            {
                SiteId = "base:site_city",
                DisplayName = "Test City",
                SiteType = "City",
                LegacyAnchorHex = anchor,
            };
            site.SetLegacyHexFootprint(new[]
            {
                anchor,
                HexMath.Neighbor(anchor, 0),
                HexMath.Neighbor(anchor, 1),
            });
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, site);

            Assert.IsTrue(world.Strategic.Sites.TryGetAtLegacyHex(anchor, out var atAnchor));
            Assert.AreEqual("base:site_city", atAnchor.SiteId);
            Assert.IsTrue(world.Strategic.Sites.TryGetAtLegacyHex(HexMath.Neighbor(anchor, 0), out _));
        }
    }
}
