using System.IO;
using NUnit.Framework;
using XianXia.Core.Construction;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Data.Bootstrap;
using XianXia.Data.Content;
using XianXia.Data.Serialization;

namespace XianXia.Tests
{
    public sealed class ConstructionSystemV1Tests
    {
        const string Player = "faction:player";
        const string Wood = "base:resource_rough_wood";
        const string Building = ConstructionService.FactionControlPostBuildingId;

        static string BaseGamePath
        {
            get
            {
                var current = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
                while (current != null)
                {
                    var candidate = Path.Combine(current.FullName, "Content", "BaseGame");
                    if (Directory.Exists(candidate))
                        return candidate;
                    current = current.Parent;
                }
                return Path.GetFullPath(Path.Combine("Content", "BaseGame"));
            }
        }

        static SimulationWorld World()
        {
            var world = new SimulationWorld();
            world.HexWorld.FillRectangle(12, 12, HexTerrainType.Plain);
            world.Strategic.PlayerFactionId = Player;
            world.InventoryCatalog.Register(Wood, "粗木", 99, new[] { "resource" });
            var spec = new BuildingConstructionSpec
            {
                BuildingId = Building,
                DisplayName = "势力控制建筑",
                UnlockedByDefault = true,
                PlacementKind = ConstructionPlacementKind.FactionFlag,
                DismantleRefundRate = .5f
            };
            spec.Costs.Add(new ConstructionMaterialCost { ItemId = Wood, Count = 10 });
            world.ConstructionCatalog.Register(spec);
            return world;
        }

        [Test]
        public void ConstructRequiresMaterialsAndCommitsFlagWithExactCost()
        {
            var world = World();
            world.Inventory.TryAddAll(Wood, 9);
            var anchor = new HexCoord(5, 5);
            var failed = ConstructionService.TryConstructFactionFlag(
                world, Building, Player, anchor, 2f, 3f, out _);
            Assert.IsTrue(failed.IsFailure);
            Assert.AreEqual(9, world.Inventory.GetCount(Wood));
            Assert.IsEmpty(world.Strategic.FactionFlags.Flags);

            world.Inventory.TryAddAll(Wood, 1);
            var success = ConstructionService.TryConstructFactionFlag(
                world, Building, Player, anchor, 2f, 3f, out var flagId);
            Assert.IsTrue(success.IsSuccess, success.IsFailure ? success.Error.ToString() : string.Empty);
            Assert.AreEqual(0, world.Inventory.GetCount(Wood));
            Assert.IsTrue(world.Strategic.FactionFlags.Flags.ContainsKey(flagId));
        }

        [Test]
        public void InvalidPlacementDoesNotConsumeMaterialsOrCreateFlag()
        {
            var world = World();
            world.Inventory.TryAddAll(Wood, 20);
            world.Strategic.FactionFlags.Register(new FactionFlagState
            {
                FlagId = "flag:enemy", FactionId = "faction:enemy",
                AnchorHex = new HexCoord(5, 5), EstablishedOrder = 1
            });
            StrategicTerritoryCoverageResolver.Rebuild(world);

            var result = ConstructionService.TryConstructFactionFlag(
                world, Building, Player, new HexCoord(6, 5), 0f, 0f, out _);
            Assert.IsTrue(result.IsFailure);
            Assert.AreEqual(20, world.Inventory.GetCount(Wood));
            Assert.AreEqual(1, world.Strategic.FactionFlags.Flags.Count);
        }

        [Test]
        public void DismantleOwnFlagRefundsFiveAndRebuildsTerritory()
        {
            var world = World();
            var anchor = new HexCoord(5, 5);
            world.Strategic.FactionFlags.Register(new FactionFlagState
            {
                FlagId = "flag:authored", FactionId = Player, AnchorHex = anchor,
                EstablishedOrder = 1, CurrentHp = 1, MaxHp = 100
            });
            StrategicTerritoryCoverageResolver.Rebuild(world);
            Assert.AreEqual(Player, TerritoryControlService.GetController(world, anchor));

            var result = ConstructionService.TryDismantleFactionFlag(
                world, Building, Player, "flag:authored", out var refunds);
            Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error.ToString() : string.Empty);
            Assert.IsEmpty(world.Strategic.FactionFlags.Flags);
            Assert.AreEqual(string.Empty, TerritoryControlService.GetController(world, anchor));
            Assert.AreEqual(5, world.Inventory.GetCount(Wood));
            Assert.AreEqual(5, refunds[0].Count);
        }

        [Test]
        public void CombatDestroyNeverRefundsMaterials()
        {
            var world = World();
            world.Strategic.FactionFlags.Register(new FactionFlagState
            {
                FlagId = "flag:combat", FactionId = "faction:enemy",
                AnchorHex = new HexCoord(5, 5), EstablishedOrder = 1
            });
            Assert.IsTrue(FactionFlagService.TryDestroy(world, "flag:combat").IsSuccess);
            Assert.AreEqual(0, world.Inventory.GetCount(Wood));
        }

        [Test]
        public void FullInventoryRejectsDismantleWithoutMutation()
        {
            var world = World();
            for (var i = 0; i < world.Inventory.SlotCapacity; i++)
            {
                var id = "test:filler_" + i;
                world.InventoryCatalog.Register(id, id, 1, null);
                Assert.IsTrue(world.Inventory.TryAddAll(id, 1));
            }
            world.Strategic.FactionFlags.Register(new FactionFlagState
            {
                FlagId = "flag:full", FactionId = Player,
                AnchorHex = new HexCoord(5, 5), EstablishedOrder = 1
            });
            StrategicTerritoryCoverageResolver.Rebuild(world);

            var result = ConstructionService.TryDismantleFactionFlag(
                world, Building, Player, "flag:full", out _);
            Assert.IsTrue(result.IsFailure);
            StringAssert.Contains("背包空间不足", result.Error.Message);
            Assert.IsTrue(world.Strategic.FactionFlags.Flags.ContainsKey("flag:full"));
            Assert.AreEqual(0, world.Inventory.GetCount(Wood));
        }

        [Test]
        public void RuntimeFlagIdsDoNotRepeatAfterSameTickSameAnchorRebuild()
        {
            var world = World();
            var anchor = new HexCoord(5, 5);
            var first = FactionFlagService.NextRuntimeFlagId(world, Player, anchor);
            world.Strategic.FactionFlags.Register(new FactionFlagState
                { FlagId = first, FactionId = Player, AnchorHex = anchor, EstablishedOrder = 1 });
            world.Strategic.FactionFlags.Remove(first);
            var second = FactionFlagService.NextRuntimeFlagId(world, Player, anchor);
            Assert.AreNotEqual(first, second);
        }

        [Test]
        public void BuildingContentLoadsAndRehydratesForNewGameAndSnapshotShell()
        {
            var loaded = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : string.Empty);
            var world = new SimulationWorld();
            Assert.IsTrue(ContentRuntimeBootstrap.Apply(world, loaded.Value.Registry).IsSuccess);
            Assert.IsTrue(world.ConstructionCatalog.TryGet(Building, out var spec));
            Assert.AreEqual(Wood, spec.Costs[0].ItemId);
            Assert.AreEqual(10, spec.Costs[0].Count);

            world.ConstructionCatalog.Clear();
            var shell = RuntimeContentShellBootstrap.Rehydrate(world, loaded.Value.Registry);
            Assert.IsTrue(shell.IsSuccess, shell.IsFailure ? shell.Error.ToString() : string.Empty);
            Assert.IsTrue(world.ConstructionCatalog.TryGet(Building, out _));
        }

        [Test]
        public void ConstructAndDismantlePersistThroughExistingFlagAndInventorySnapshots()
        {
            var loaded = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : string.Empty);
            var world = World();
            world.Inventory.TryAddAll(Wood, 20);
            Assert.IsTrue(ConstructionService.TryConstructFactionFlag(
                world, Building, Player, new HexCoord(5, 5), 1f, 2f, out var flagId).IsSuccess);

            var snapshots = new SnapshotService(new JsonSnapshotSerializer());
            var builtJson = snapshots.CaptureJson(world, new SimulationLoop(world));
            Assert.IsTrue(builtJson.IsSuccess);
            var built = snapshots.RestoreJson(builtJson.Value);
            Assert.IsTrue(built.IsSuccess);
            var builtDto = new JsonSnapshotSerializer().Deserialize(builtJson.Value);
            Assert.IsTrue(builtDto.IsSuccess);
            built.Value.world.HexWorld.FillRectangle(12, 12, HexTerrainType.Plain);
            Assert.IsTrue(StrategicSnapshotHelper.RestoreHexPoliticalState(
                built.Value.world, builtDto.Value.Strategic).IsSuccess);
            Assert.AreEqual(10, built.Value.world.Inventory.GetCount(Wood));
            Assert.IsTrue(built.Value.world.Strategic.FactionFlags.Flags.ContainsKey(flagId));
            Assert.IsTrue(RuntimeContentShellBootstrap.Rehydrate(
                built.Value.world, loaded.Value.Registry).IsSuccess);
            Assert.IsTrue(built.Value.world.ConstructionCatalog.TryGet(Building, out _));

            Assert.IsTrue(ConstructionService.TryDismantleFactionFlag(
                built.Value.world, Building, Player, flagId, out _).IsSuccess);
            Assert.AreEqual(15, built.Value.world.Inventory.GetCount(Wood));
            var dismantledJson = snapshots.CaptureJson(built.Value.world, built.Value.loop);
            Assert.IsTrue(dismantledJson.IsSuccess);
            var dismantled = snapshots.RestoreJson(dismantledJson.Value);
            Assert.IsTrue(dismantled.IsSuccess);
            var dismantledDto = new JsonSnapshotSerializer().Deserialize(dismantledJson.Value);
            Assert.IsTrue(dismantledDto.IsSuccess);
            dismantled.Value.world.HexWorld.FillRectangle(12, 12, HexTerrainType.Plain);
            Assert.IsTrue(StrategicSnapshotHelper.RestoreHexPoliticalState(
                dismantled.Value.world, dismantledDto.Value.Strategic).IsSuccess);
            Assert.AreEqual(15, dismantled.Value.world.Inventory.GetCount(Wood));
            Assert.IsFalse(dismantled.Value.world.Strategic.FactionFlags.Flags.ContainsKey(flagId));
            Assert.AreNotEqual(flagId, FactionFlagService.NextRuntimeFlagId(
                dismantled.Value.world, Player, new HexCoord(5, 5)));
        }

        [TestCase("base:missing", 0.5)]
        [TestCase(Wood, 1.1)]
        public void InvalidBuildingReferenceOrRefundRateFailsContentValidation(string itemId, double rate)
        {
            var root = Path.Combine(Path.GetTempPath(), "xianxia_construction_" + Path.GetRandomFileName());
            Directory.CreateDirectory(Path.Combine(root, "Data"));
            File.WriteAllText(Path.Combine(root, "manifest.json"),
                "{\"modId\":\"test\",\"namespace\":\"base\",\"version\":\"1.0.0\"}");
            File.WriteAllText(Path.Combine(root, "Data", "defs.json"),
                "{\"definitions\":[" +
                "{\"id\":\"base:resource_rough_wood\",\"type\":\"resource\",\"name\":\"粗木\"}," +
                "{\"id\":\"base:building_test\",\"type\":\"building\",\"placementKind\":\"factionFlag\"," +
                "\"dismantleRefundRate\":" + rate.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                ",\"costs\":[{\"itemId\":\"" + itemId + "\",\"count\":10}]}]}");
            try
            {
                Assert.IsTrue(new ContentPackageLoader().Load(new[] { root }).IsFailure);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
    }
}
