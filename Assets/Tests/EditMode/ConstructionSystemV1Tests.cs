using System.IO;
using NUnit.Framework;
using XianXia.Core.Construction;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Data.Bootstrap;
using XianXia.Data.Content;

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
        public void DismantleOwnFlagRefundsFiveAndRebuildsTerritory()
        {
            var world = World();
            var anchor = new HexCoord(5, 5);
            world.Strategic.FactionFlags.Register(new FactionFlagState
            {
                FlagId = "flag:authored", FactionId = Player, AnchorHex = anchor,
                EstablishedOrder = 1, CurrentHp = 1, MaxHp = 100
            });

            var result = ConstructionService.TryDismantleFactionFlag(
                world, Building, Player, "flag:authored", out var refunds);
            Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error.ToString() : string.Empty);
            Assert.IsEmpty(world.Strategic.FactionFlags.Flags);
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
