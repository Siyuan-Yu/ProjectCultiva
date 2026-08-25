using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Tests
{
    public sealed class MultiHexWorldSiteAcceptanceTests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        static readonly (string SiteId, int ExpectedCount, HexCoord Anchor)[] Ch01MultiHexSamples =
        {
            ("base:site_a", 6, new HexCoord(68, 40)),
            ("base:site_b", 6, new HexCoord(106, 26)),
            ("base:site_chengzhen", 4, new HexCoord(118, 46)),
            ("base:site_zhuangyuan", 4, new HexCoord(118, 32)),
        };

        [Test]
        public void MH01_FootprintValidator_RejectsDisconnectedFootprint()
        {
            var footprint = new List<HexCoord>
            {
                new(0, 0),
                new(2, 0),
            };
            Assert.IsFalse(WorldSiteFootprintValidator.IsFootprintConnected(footprint));
        }

        [Test]
        public void MH02_FootprintValidator_AcceptsConnectedSixHexCluster()
        {
            var anchor = new HexCoord(10, 10);
            var footprint = new List<HexCoord>
            {
                anchor,
                HexMath.Neighbor(anchor, 0),
                HexMath.Neighbor(anchor, 1),
                HexMath.Neighbor(anchor, 2),
                HexMath.Neighbor(anchor, 3),
                HexMath.Neighbor(anchor, 4),
            };
            Assert.IsTrue(WorldSiteFootprintValidator.IsFootprintConnected(footprint));
        }

        [Test]
        public void MH03_SixHexWorldSite_TryGetAtHexFromEveryFootprintCell()
        {
            var world = BuildSixHexSiteWorld(out var site, out var footprint);
            Assert.AreEqual(6, WorldSiteFootprintValidator.CountFootprintHexes(site));
            foreach (var hex in footprint)
            {
                Assert.IsTrue(world.Strategic.Sites.TryGetAtHex(hex, out var found), hex.ToString());
                Assert.AreEqual(site.SiteId, found.SiteId);
            }
        }

        [Test]
        public void MH04_FourHexWorldSite_TryGetAtHexFromEveryFootprintCell()
        {
            var world = BuildFourHexSiteWorld(out var site, out var footprint);
            Assert.AreEqual(4, WorldSiteFootprintValidator.CountFootprintHexes(site));
            foreach (var hex in footprint)
            {
                Assert.IsTrue(world.Strategic.Sites.TryGetAtHex(hex, out var found), hex.ToString());
                Assert.AreEqual(site.SiteId, found.SiteId);
            }
        }

        [Test]
        public void MH05_AnchorMustBelongToFootprint()
        {
            var site = new WorldSite
            {
                SiteId = "test:site_bad_anchor",
                DisplayName = "Bad Anchor",
                AnchorHex = new HexCoord(5, 5),
            };
            site.SetFootprint(new[] { new HexCoord(1, 1), new HexCoord(2, 1) });
            Assert.IsFalse(WorldSiteFootprintValidator.IsAnchorInFootprint(site));
        }

        [Test]
        public void MH06_Ch01Content_MultiHexSamplesLoadWithExplicitFootprints()
        {
            var loaded = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : string.Empty);
            Assert.IsTrue(
                loaded.Value.Registry.TryGetHexWorldContent(
                    DefinitionId.Parse(HexStrategicMapBootstrap.DefaultHexWorldContentId).Value,
                    out var definition),
                "ch01 hex world content missing");

            var world = new SimulationWorld();
            Assert.IsTrue(HexWorldContentLoader.Apply(world, definition).IsSuccess);

            foreach (var sample in Ch01MultiHexSamples)
            {
                Assert.IsTrue(world.Strategic.Sites.TryGet(sample.SiteId, out var site), sample.SiteId);
                Assert.AreEqual(sample.Anchor, site.AnchorHex, sample.SiteId);
                Assert.AreEqual(sample.ExpectedCount, WorldSiteFootprintValidator.CountFootprintHexes(site), sample.SiteId);
                Assert.IsTrue(WorldSiteFootprintValidator.IsAnchorInFootprint(site), sample.SiteId);

                var seen = new HashSet<HexCoord>();
                foreach (var hex in site.EnumerateFootprintHexes())
                {
                    Assert.IsTrue(seen.Add(hex), $"{sample.SiteId} duplicate footprint hex {hex}");
                    Assert.IsTrue(world.Strategic.Sites.TryGetAtHex(hex, out var atHex));
                    Assert.AreEqual(sample.SiteId, atHex.SiteId);
                }
            }
        }

        [Test]
        public void MH07_NonAnchorFootprintHex_TryGetAtHexReturnsSameSite()
        {
            var world = BuildSixHexSiteWorld(out var site, out var footprint);
            var nonAnchor = footprint[3];
            Assert.AreNotEqual(site.AnchorHex, nonAnchor);
            Assert.IsTrue(world.Strategic.Sites.TryGetAtHex(nonAnchor, out var atHex));
            Assert.AreEqual(site.SiteId, atHex.SiteId);
        }

        static SimulationWorld BuildSixHexSiteWorld(out WorldSite site, out List<HexCoord> footprint)
        {
            var world = new SimulationWorld();
            world.HexWorld.FillRectangle(20, 20);
            var anchor = new HexCoord(8, 8);
            footprint = new List<HexCoord>
            {
                anchor,
                HexMath.Neighbor(anchor, 0),
                HexMath.Neighbor(anchor, 1),
                HexMath.Neighbor(anchor, 2),
                HexMath.Neighbor(anchor, 3),
                HexMath.Neighbor(anchor, 4),
            };
            site = new WorldSite
            {
                SiteId = "test:site_wild_six",
                DisplayName = "六格荒原",
                SiteType = "Wild",
                AnchorHex = anchor,
                LocalMapId = "test:map_wild_six",
            };
            site.SetFootprint(footprint);
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, site);
            return world;
        }

        static SimulationWorld BuildFourHexSiteWorld(out WorldSite site, out List<HexCoord> footprint)
        {
            var world = new SimulationWorld();
            world.HexWorld.FillRectangle(20, 20);
            var anchor = new HexCoord(5, 5);
            footprint = new List<HexCoord>
            {
                anchor,
                HexMath.Neighbor(anchor, 0),
                HexMath.Neighbor(anchor, 1),
                HexMath.Neighbor(anchor, 5),
            };
            site = new WorldSite
            {
                SiteId = "test:site_town_four",
                DisplayName = "四格镇",
                SiteType = "Town",
                AnchorHex = anchor,
                LocalMapId = "test:map_town_four",
            };
            site.SetFootprint(footprint);
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, site);
            return world;
        }
    }
}
