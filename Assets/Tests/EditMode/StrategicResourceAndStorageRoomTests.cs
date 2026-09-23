using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using XianXia.Core.Construction;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Inventory;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;
using XianXia.Data.Bootstrap;
using XianXia.Data.Content;
using XianXia.Data.Serialization;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    public sealed class StrategicResourceAndStorageRoomTests
    {
        const string Player = "test:player";
        const string Enemy = "test:enemy";
        const string Surface = "test:surface";
        const string Wood = "base:resource_rough_wood";
        const string Herb = "base:resource_spirit_herb";
        const string Manual = "test:manual";
        static string ContentPath => Environment.GetEnvironmentVariable("XIANXIA_BASEGAME") ??
                                     Path.GetFullPath("Content/BaseGame");

        static SimulationWorld World()
        {
            var world = new SimulationWorld();
            world.Strategic.PlayerFactionId = Player;
            world.InventoryCatalog.Register(Wood, "粗木", 99, new[] { "resource" });
            world.InventoryCatalog.Register(Herb, "灵药", 99, new[] { "resource" });
            world.InventoryCatalog.Register(Manual, "秘籍", 1, new[] { "manual" });
            AddSite(world, "A", Player, true, true);
            AddSite(world, "B", Player, true, true);
            world.PlayerPartyTravel.SetCurrentOutdoorWorldSiteContext("A");
            return world;
        }

        [Test]
        public void AccessibleStockUsesStorageOnlyForResourcesAndOnlyInsideControlledNetwork()
        {
            var world = World();
            Assert.IsTrue(WorldSitePublicStockService.SetInitial(world, "A", Herb, 2).IsSuccess);
            Assert.IsTrue(WorldSitePublicStockService.SetInitial(world, "A", Manual, 9).IsSuccess);
            var herbCondition = new ContentCondition { Kind = "stockAtLeast", Id = Herb, Amount = 1 };
            var manualCondition = new ContentCondition { Kind = "stockAtLeast", Id = Manual, Amount = 1 };

            Assert.IsTrue(PlayerStrategicResourceService.CanAccessSiteStorageNetwork(world));
            Assert.AreEqual(2, PlayerStrategicResourceService.GetPlayerAccessibleCount(world, Herb));
            Assert.IsTrue(ContentConditionEvaluator.Pass(world, EntityId.None, herbCondition));
            Assert.AreEqual(0, PlayerStrategicResourceService.GetPlayerAccessibleCount(world, Manual));
            Assert.IsFalse(ContentConditionEvaluator.Pass(world, EntityId.None, manualCondition));
            Assert.IsTrue(ContentOutcomeApplier.Apply(world, EntityId.None,
                new ContentOutcome { Kind = "removeStock", Id = Manual, Amount = 1 }).IsFailure);
            Assert.AreEqual(9, WorldSitePublicStockService.GetCount(world, "A", Manual),
                "Non-resource outcomes must never consume strategic public stock.");

            world.PlayerPartyTravel.SetCurrentOutdoorWorldSiteContext(string.Empty);
            Assert.IsFalse(PlayerStrategicResourceService.CanAccessSiteStorageNetwork(world));
            Assert.AreEqual(0, PlayerStrategicResourceService.GetPlayerAccessibleCount(world, Herb));
            Assert.IsFalse(ContentConditionEvaluator.Pass(world, EntityId.None, herbCondition));
            Assert.AreEqual(2, WorldSitePublicStockService.GetCount(world, "A", Herb));

            var noStorage = new SimulationWorld();
            noStorage.Strategic.PlayerFactionId = Player;
            noStorage.InventoryCatalog.Register(Herb, "灵药", 99, new[] { "resource" });
            AddSite(noStorage, "A", Player, true, false);
            noStorage.PlayerPartyTravel.SetCurrentOutdoorWorldSiteContext("A");
            Assert.IsFalse(PlayerStrategicResourceService.CanAccessSiteStorageNetwork(noStorage));
        }

        [Test]
        public void RemoveStockUsesStrategicSourcesOutsideGateAndTransactionRollback()
        {
            var strategic = World();
            Assert.IsTrue(WorldSitePublicStockService.SetInitial(strategic, "A", Herb, 2).IsSuccess);
            Assert.IsTrue(ContentOutcomeApplier.Apply(strategic, EntityId.None,
                new ContentOutcome { Kind = "removeStock", Id = Herb, Amount = 1 }).IsSuccess);
            Assert.AreEqual(1, WorldSitePublicStockService.GetCount(strategic, "A", Herb));
            Assert.AreEqual(0, strategic.Inventory.GetCount(Herb));

            var outside = World();
            Assert.IsTrue(WorldSitePublicStockService.SetInitial(outside, "A", Herb, 2).IsSuccess);
            outside.PlayerPartyTravel.SetCurrentOutdoorWorldSiteContext(string.Empty);
            Assert.IsTrue(ContentOutcomeApplier.Apply(outside, EntityId.None,
                new ContentOutcome { Kind = "removeStock", Id = Herb, Amount = 1 }).IsFailure);
            Assert.AreEqual(2, WorldSitePublicStockService.GetCount(outside, "A", Herb));

            var mixed = World();
            Assert.IsTrue(WorldSitePublicStockService.SetInitial(mixed, "A", Herb, 1).IsSuccess);
            Assert.IsTrue(mixed.Inventory.TryAddAll(Herb, 1));
            Assert.IsTrue(ContentOutcomeApplier.Apply(mixed, EntityId.None,
                new ContentOutcome { Kind = "removeStock", Id = Herb, Amount = 2 }).IsSuccess);
            Assert.AreEqual(0, WorldSitePublicStockService.GetCount(mixed, "A", Herb));
            Assert.AreEqual(0, mixed.Inventory.GetCount(Herb));

            var rollback = World();
            Assert.IsTrue(WorldSitePublicStockService.SetInitial(rollback, "A", Herb, 1).IsSuccess);
            var failed = ContentOutcomeApplier.ApplyAll(rollback, EntityId.None, new[]
            {
                new ContentOutcome { Kind = "removeStock", Id = Herb, Amount = 1 },
                new ContentOutcome { Kind = "unknown" }
            });
            Assert.IsTrue(failed.IsFailure);
            Assert.AreEqual(1, WorldSitePublicStockService.GetCount(rollback, "A", Herb));
            Assert.AreEqual(0, rollback.Inventory.GetCount(Herb));
            Assert.AreEqual(0, rollback.Events.Count);
        }

        [Test]
        public void WarehouseWithdrawMovesOnlyStorageResourcesAndLeavesStockUntouchedWhenBagIsFull()
        {
            var world = World();
            Assert.IsTrue(WorldSitePublicStockService.SetInitial(world, "A", Herb, 5).IsSuccess);
            Assert.AreEqual(5, PlayerStrategicResourceService.GetAccessibleStorageCount(world, Herb));
            var totals = new System.Collections.Generic.List<AccessibleStrategicStock>();
            PlayerStrategicResourceService.CollectAccessibleStorageStock(world, totals);
            Assert.AreEqual(1, totals.Count, "Warehouse enumeration must contain resources only and exclude bag stock.");
            Assert.AreEqual(5, totals.Single(x => x.ResourceId == Herb).Amount);

            Assert.IsTrue(PlayerStrategicResourceService.TryWithdrawToPartyInventory(world, Herb, 1).IsSuccess);
            Assert.AreEqual(4, WorldSitePublicStockService.GetCount(world, "A", Herb));
            Assert.AreEqual(1, world.Inventory.GetCount(Herb));

            var full = World();
            full.Inventory.SetSlotCapacity(1);
            Assert.IsTrue(full.Inventory.TryAddAll(Manual, 1));
            Assert.IsTrue(WorldSitePublicStockService.SetInitial(full, "A", Herb, 5).IsSuccess);
            Assert.IsTrue(PlayerStrategicResourceService.TryWithdrawToPartyInventory(full, Herb, 1).IsFailure);
            Assert.AreEqual(5, WorldSitePublicStockService.GetCount(full, "A", Herb));
            Assert.AreEqual(0, full.Inventory.GetCount(Herb));
        }

        [Test]
        public void ResourceQuestConditionProgressAndJournalShareAccessibleStockCount()
        {
            var world = World();
            Assert.IsTrue(WorldSitePublicStockService.SetInitial(world, "A", Herb, 2).IsSuccess);
            var spec = new QuestSpec { Id = "test:strategic_stock_quest" };
            spec.CompleteConditions.Add(new ContentCondition { Kind = "stockAtLeast", Id = Herb, Amount = 3 });
            world.Quests.Register(spec);
            Assert.IsTrue(new QuestService().TryStart(world, spec.Id, EntityId.None).IsSuccess);
            Assert.IsTrue(world.Quests.TryGet(spec.Id, out var runtime));

            new QuestService().Evaluate(world, EntityId.None);
            Assert.AreEqual(2, runtime.ProgressCount);
            Assert.IsFalse(ContentConditionEvaluator.AllPass(world, EntityId.None, spec.CompleteConditions));
            Assert.IsTrue(QuestJournalQuery.TryGetStockProgress(world, spec, out var journalCount, out var max));
            Assert.AreEqual(2, journalCount);
            Assert.AreEqual(3, max);

            world.PlayerPartyTravel.SetCurrentOutdoorWorldSiteContext(string.Empty);
            new QuestService().Evaluate(world, EntityId.None);
            Assert.AreEqual(0, runtime.ProgressCount);
            Assert.IsFalse(ContentConditionEvaluator.AllPass(world, EntityId.None, spec.CompleteConditions));
            Assert.IsTrue(QuestJournalQuery.TryGetStockProgress(world, spec, out journalCount, out max));
            Assert.AreEqual(0, journalCount);
            Assert.AreEqual(3, max);
        }

        static WorldSite AddSite(SimulationWorld world, string id, string owner, bool active, bool storage)
        {
            var site = new WorldSite { SiteId = id, DisplayName = id, OwnerFactionId = owner, IsCoreActive = active };
            world.Strategic.Sites.Register(site);
            if (storage)
                Assert.IsTrue(world.SiteStorageRooms.TryRegister(new WorldSiteStorageRoomState {
                    StorageRoomId = "storage:" + id, SiteId = id, SurfaceId = Surface,
                    DisplayName = id + "储藏室", WorldX = 1, WorldY = 1
                }));
            return site;
        }

        [Test]
        public void SharedCountAndWithdrawalUseCurrentThenOrdinalSitesThenBagAndRollback()
        {
            var world = World();
            Assert.IsTrue(world.Inventory.TryAddAll(Wood, 5));
            Assert.IsTrue(world.Inventory.TryAddAll(Manual, 1));
            Assert.IsTrue(WorldSitePublicStockService.SetInitial(world, "A", Wood, 10).IsSuccess);
            Assert.IsTrue(WorldSitePublicStockService.SetInitial(world, "B", Wood, 7).IsSuccess);
            Assert.AreEqual(22, PlayerStrategicResourceService.GetAvailableCount(world, Wood));
            Assert.AreEqual(0, PlayerStrategicResourceService.GetAvailableCount(world, Manual));
            Assert.AreEqual(1, ConstructionService.GetAvailableMaterialCount(world, Manual));
            var spec = new BuildingConstructionSpec();
            spec.Costs.Add(new ConstructionMaterialCost { ItemId = Wood, Count = 20 });
            Assert.IsTrue(ConstructionService.HasRequiredMaterials(world, spec, out _));

            Assert.IsTrue(PlayerStrategicResourceService.TryConsume(world, Wood, 12, out var receipt).IsSuccess);
            Assert.AreEqual(0, WorldSitePublicStockService.GetCount(world, "A", Wood));
            Assert.AreEqual(5, WorldSitePublicStockService.GetCount(world, "B", Wood));
            Assert.AreEqual(5, world.Inventory.GetCount(Wood));
            Assert.IsTrue(PlayerStrategicResourceService.Rollback(world, receipt).IsSuccess);
            Assert.AreEqual(10, WorldSitePublicStockService.GetCount(world, "A", Wood));
            Assert.AreEqual(7, WorldSitePublicStockService.GetCount(world, "B", Wood));
            Assert.AreEqual(5, world.Inventory.GetCount(Wood));
        }

        [Test]
        public void OutsideEnemyInactiveAndNoStorageStocksStayOfflineWithoutBeingDeleted()
        {
            var world = World();
            var enemy = AddSite(world, "C", Enemy, true, true);
            var inactive = AddSite(world, "D", Player, false, true);
            AddSite(world, "E", Player, true, false);
            foreach (var id in new[] { "A", "B", "C", "D", "E" })
                Assert.IsTrue(WorldSitePublicStockService.SetInitial(world, id, Wood, 10).IsSuccess);
            Assert.IsTrue(world.Inventory.TryAddAll(Wood, 3));
            Assert.AreEqual(23, PlayerStrategicResourceService.GetAvailableCount(world, Wood));

            enemy.OwnerFactionId = Player;
            Assert.AreEqual(33, PlayerStrategicResourceService.GetAvailableCount(world, Wood));
            inactive.IsCoreActive = true;
            Assert.AreEqual(43, PlayerStrategicResourceService.GetAvailableCount(world, Wood));
            world.PlayerPartyTravel.SetCurrentOutdoorWorldSiteContext(string.Empty);
            Assert.AreEqual(3, PlayerStrategicResourceService.GetAvailableCount(world, Wood));
            var spec = new BuildingConstructionSpec();
            spec.Costs.Add(new ConstructionMaterialCost { ItemId = Wood, Count = 4 });
            Assert.IsFalse(ConstructionService.HasRequiredMaterials(world, spec, out _));
            Assert.AreEqual(10, WorldSitePublicStockService.GetCount(world, "C", Wood));
            Assert.AreEqual(10, WorldSitePublicStockService.GetCount(world, "D", Wood));
            Assert.AreEqual(10, WorldSitePublicStockService.GetCount(world, "E", Wood));
        }

        [Test]
        public void BoundWorldSiteIdIsAnOptionalConstructedAssetJsonField()
        {
            var snapshot = new WorldSnapshot();
            snapshot.OutdoorConstructedAssets.Add(new OutdoorConstructedAssetSnapshotDto {
                StableAssetId = "asset:runtime:storage:1", BuildingId = "base:building_storage_room",
                Kind = "storageRoom", SurfaceId = Surface, WorldX = 1, WorldY = 2,
                WorldWidth = 3, WorldHeight = 3, CellsW = 3, CellsH = 3,
                BoundLocationId = "location:runtime:storage:asset:runtime:storage:1", BoundWorldSiteId = "A"
            });
            var serializer = new JsonSnapshotSerializer();
            var json = serializer.Serialize(snapshot);
            Assert.IsTrue(json.IsSuccess);
            var restored = serializer.Deserialize(json.Value);
            Assert.IsTrue(restored.IsSuccess);
            Assert.AreEqual("A", restored.Value.OutdoorConstructedAssets.Single().BoundWorldSiteId);

            var oldJson = json.Value.Replace(",\"boundWorldSiteId\":\"A\"", string.Empty);
            var old = serializer.Deserialize(oldJson);
            Assert.IsTrue(old.IsSuccess);
            Assert.AreEqual(string.Empty, old.Value.OutdoorConstructedAssets.Single().BoundWorldSiteId);
        }

        [Test]
        public void StrictContentContainsOneHuangcunStorageRoomAndRuntimeRegistry()
        {
            var loaded = new ContentPackageLoader().Load(new[] { ContentPath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : string.Empty);
            var report = new ContentReferenceValidator().Validate(loaded.Value.Registry);
            Assert.IsTrue(report.IsValid, string.Join("\n", report.Errors.Select(e => e.ToString())));
            var buildingId = XianXia.Core.Domain.Ids.DefinitionId.Parse("base:building_storage_room").Value;
            Assert.IsTrue(loaded.Value.Registry.Buildings.TryGetValue(buildingId, out var building));
            Assert.AreEqual("storageRoom", building.PlacementKind);
            Assert.AreEqual(3, building.FootprintCellsW);
            Assert.AreEqual(3, building.FootprintCellsH);

            var surface = loaded.Value.Registry.OutdoorSurfaces.Values.Single(s =>
                s.SurfaceId == "base:surface_main_wilderness_v1");
            Assert.AreEqual(1, surface.SitePlacements.Count(p =>
                p.SiteId == "base:site_huangcun" && p.Kind == "storageRoom"));
            var placement = surface.SitePlacements.Single(p =>
                p.StableId == "base:site_huangcun:storage_room");
            var mapper = new XianXia.Core.World.Surface.OutdoorSurfaceCoordinateMapper(
                surface.ChunkWidth, surface.ChunkHeight, surface.CellSize,
                originWorldX: surface.OriginWorldX, originWorldY: surface.OriginWorldY);
            var materializationChunk = mapper.WorldToChunk(
                placement.WorldX + placement.WorldWidth * .5f,
                placement.WorldY + placement.WorldHeight * .5f);
            Assert.AreEqual(materializationChunk.X, placement.ChunkX);
            Assert.AreEqual(materializationChunk.Y, placement.ChunkY);
            Assert.AreEqual(mapper.WorldToChunk(placement.WorldX + .00001f, placement.WorldY + .00001f),
                materializationChunk, "storageRoom lower edge must stay in its declared chunk.");
            Assert.AreEqual(mapper.WorldToChunk(placement.WorldX + placement.WorldWidth - .00001f,
                placement.WorldY + placement.WorldHeight - .00001f), materializationChunk,
                "storageRoom upper edge must stay in its declared chunk.");
            Assert.IsFalse(surface.SitePlacements.Any(other => other.StableId != placement.StableId &&
                other.WorldX < placement.WorldX + placement.WorldWidth &&
                other.WorldX + other.WorldWidth > placement.WorldX &&
                other.WorldY < placement.WorldY + placement.WorldHeight &&
                other.WorldY + other.WorldHeight > placement.WorldY),
                "authored storageRoom must not overlap another authored placement.");
            Assert.IsTrue(HostDemoTileMap.TryEstimateOutdoorRenderedObjectCount(placement, out var renderedCount));
            Assert.AreEqual(1, renderedCount, "storageRoom must use the SingleCentered Host materialization path.");
            var started = new PlayableDayBootstrap().Start(loaded.Value,
                new PlayableDayOptions { OpeningScenarioId = "base:scenario_ch01_reference" });
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : string.Empty);
            Assert.IsTrue(started.Value.World.SiteStorageRooms.TryGetBySite("base:site_huangcun", out var room));
            Assert.AreEqual("base:site_huangcun:storage_room", room.StorageRoomId);
            Assert.IsTrue(WorldSiteAdministrativeControlResolver.TryResolve(
                started.Value.World, room.SurfaceId, room.WorldX, room.WorldY, out var managingSite, out _));
            Assert.AreEqual("base:site_huangcun", managingSite.SiteId);
        }
    }
}
