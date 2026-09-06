using NUnit.Framework;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Data.Serialization;

namespace XianXia.Tests
{
    public sealed class FactionFlagTerritoryTests
    {
        static SimulationWorld World()
        {
            var world = new SimulationWorld();
            world.HexWorld.FillRectangle(12, 12, HexTerrainType.Plain);
            return world;
        }

        [Test]
        public void EarlierAssetWinsAndDestroyingItExpandsLaterAsset()
        {
            var world = World();
            world.Strategic.FactionFlags.Register(new FactionFlagState
                { FlagId = "flag:early", FactionId = "faction:a", AnchorHex = new HexCoord(5, 5), EstablishedOrder = 1, CurrentHp = 100, MaxHp = 100 });
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, new WorldSite
                { SiteId = "site:late", OwnerFactionId = "faction:b", AnchorHex = new HexCoord(6, 5), ControlEstablishedOrder = 2 });

            StrategicTerritoryCoverageResolver.Rebuild(world);
            Assert.AreEqual("faction:a", TerritoryControlService.GetController(world, new HexCoord(6, 5)));

            Assert.IsTrue(FactionFlagService.TryDestroy(world, "flag:early").IsSuccess);
            Assert.AreEqual("faction:b", TerritoryControlService.GetController(world, new HexCoord(6, 5)));
            Assert.AreEqual("faction:b", TerritoryControlService.GetController(world, new HexCoord(5, 5)));
        }

        [Test]
        public void SiteCapturePreservesEstablishedOrder()
        {
            var world = World();
            var site = new WorldSite { SiteId = "site:a", OwnerFactionId = "faction:a", AnchorHex = new HexCoord(3, 3), ControlEstablishedOrder = 17 };
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, site);

            Assert.IsTrue(WorldSiteTerritoryTransferService.Transfer(world, site.SiteId, "faction:b").IsSuccess);
            Assert.AreEqual(17, site.ControlEstablishedOrder);
            Assert.AreEqual("faction:b", TerritoryControlService.GetController(world, new HexCoord(3, 3)));
        }

        [Test]
        public void PlacementGatesCoverNeutralSiteDuplicateEnemyAndFullyCovered()
        {
            var neutral = World();
            Assert.IsTrue(FactionFlagService.ValidatePlacement(neutral, "faction:player", new HexCoord(5, 5), out var gain).IsSuccess);
            Assert.Greater(gain, 0);

            var siteWorld = World();
            WorldSiteRegistrationService.RegisterSiteOnGrid(siteWorld, new WorldSite
                { SiteId = "site:a", AnchorHex = new HexCoord(5, 5), OwnerFactionId = "faction:player", ControlEstablishedOrder = 1 });
            StrategicTerritoryCoverageResolver.Rebuild(siteWorld);
            Assert.IsTrue(FactionFlagService.ValidatePlacement(siteWorld, "faction:player", new HexCoord(5, 5), out _).IsFailure);

            var enemyWorld = World();
            enemyWorld.Strategic.FactionFlags.Register(new FactionFlagState
                { FlagId = "flag:enemy", FactionId = "faction:enemy", AnchorHex = new HexCoord(5, 5), EstablishedOrder = 1 });
            StrategicTerritoryCoverageResolver.Rebuild(enemyWorld);
            Assert.IsTrue(FactionFlagService.ValidatePlacement(enemyWorld, "faction:player", new HexCoord(5, 5), out _).IsFailure);
            Assert.IsTrue(FactionFlagService.ValidatePlacement(enemyWorld, "faction:player", new HexCoord(6, 5), out _).IsFailure);

            var covered = World();
            var center = new HexCoord(5, 5);
            for (var d = 0; d < 6; d++)
                covered.Strategic.FactionFlags.Register(new FactionFlagState
                    { FlagId = "flag:" + d, FactionId = "faction:player", AnchorHex = HexMath.Neighbor(center, d), EstablishedOrder = d + 1 });
            StrategicTerritoryCoverageResolver.Rebuild(covered);
            Assert.IsTrue(FactionFlagService.ValidatePlacement(covered, "faction:player", center, out var noGain).IsFailure);
            Assert.AreEqual(0, noGain);
        }

        [Test]
        public void SnapshotAuthorityRestoresRuntimeFlagAndPreventsAuthoredResurrection()
        {
            var world = World();
            world.Strategic.FactionFlags.Register(new FactionFlagState
            {
                FlagId = "flag:runtime", FactionId = "faction:a", AnchorHex = new HexCoord(4, 4),
                EstablishedOrder = 8, CurrentHp = 35, MaxHp = 100, HasLocalPosition = true, LocalX = 1.5f, LocalZ = -2f
            });
            var snapshotService = new SnapshotService(new JsonSnapshotSerializer());
            var json = snapshotService.CaptureJson(world, new SimulationLoop(world));
            Assert.IsTrue(json.IsSuccess);
            var parsed = new JsonSnapshotSerializer().Deserialize(json.Value);
            Assert.IsTrue(parsed.IsSuccess);
            var snapshot = parsed.Value.Strategic;
            Assert.IsTrue(snapshot.HasFactionFlagSnapshotAuthority);

            var restored = World();
            restored.Strategic.FactionFlags.Register(new FactionFlagState
                { FlagId = "flag:authored", FactionId = "faction:b", AnchorHex = new HexCoord(8, 8), EstablishedOrder = 2 });
            StrategicSnapshotHelper.RestoreHexPoliticalState(restored, snapshot);

            Assert.False(restored.Strategic.FactionFlags.Flags.ContainsKey("flag:authored"));
            var flag = restored.Strategic.FactionFlags.Flags["flag:runtime"];
            Assert.AreEqual(35, flag.CurrentHp);
            Assert.AreEqual(8, flag.EstablishedOrder);
            Assert.IsTrue(flag.HasLocalPosition);
            Assert.AreEqual(1.5f, flag.LocalX);

            Assert.IsTrue(FactionFlagService.TryDestroy(world, "flag:runtime").IsSuccess);
            var afterDestroyJson = snapshotService.CaptureJson(world, new SimulationLoop(world));
            Assert.IsTrue(afterDestroyJson.IsSuccess);
            var afterDestroyParsed = new JsonSnapshotSerializer().Deserialize(afterDestroyJson.Value);
            Assert.IsTrue(afterDestroyParsed.IsSuccess);
            var afterDestroy = afterDestroyParsed.Value.Strategic;
            var newShell = World();
            newShell.Strategic.FactionFlags.Register(new FactionFlagState
                { FlagId = "flag:runtime", FactionId = "faction:a", AnchorHex = new HexCoord(4, 4), EstablishedOrder = 8 });
            StrategicSnapshotHelper.RestoreHexPoliticalState(newShell, afterDestroy);
            Assert.IsEmpty(newShell.Strategic.FactionFlags.Flags);
        }
    }
}
