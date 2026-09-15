using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using XianXia.Core.Construction;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Exploration;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World.Strategic;
using XianXia.Core.World.Surface;
using XianXia.Data.Bootstrap;
using XianXia.Data.Content;
using XianXia.Data.Serialization;

namespace XianXia.Tests
{
    public sealed class ConstructibleFarmLifecycleTests
    {
        const string Wood = "base:resource_rough_wood", Farm = "base:building_grain_field";
        const string Faction = "test:player", Surface = "test:surface";
        static string ContentPath => Environment.GetEnvironmentVariable("XIANXIA_BASEGAME") ?? Path.GetFullPath("Content/BaseGame");

        static SimulationWorld World()
        {
            var w = new SimulationWorld();
            w.SurfaceSpatial.Register(new OutdoorSurfaceSpatialMetric(Surface, 0, 0, 1, 50, 50, new[] { new SurfaceChunkCoord(0, 0) }));
            w.Strategic.PlayerFactionId = Faction;
            w.InventoryCatalog.Register(Wood, "粗木", 99, null);
            w.Inventory.TryAddAll(Wood, 20);
            var spec = new BuildingConstructionSpec { BuildingId = Farm, PlacementKind = ConstructionPlacementKind.FarmField,
                OutdoorKind = "grainField", FootprintCellsW = 5, FootprintCellsH = 4, UnlockedByDefault = true };
            spec.Costs.Add(new ConstructionMaterialCost { ItemId = Wood, Count = 5 });
            w.ConstructionCatalog.Register(spec);
            return w;
        }

        static WorldSite Site(SimulationWorld w, string id, float cx, float width, string faction = Faction)
        {
            var site = new WorldSite { SiteId = id, OwnerFactionId = faction, CoreSurfaceId = Surface,
                CoreAssetId = id + ":core", HasCoreWorldPosition = true, CoreWorldX = cx, CoreWorldY = 12,
                CoreRangeWidth = width, CoreRangeHeight = 20, CoreLevel = 1, IsCoreActive = true };
            w.Strategic.Sites.Register(site);
            Assert.IsTrue(TerritoryClaimService.CreateInitialClaim(w, site).IsSuccess);
            return site;
        }

        static XianXia.Core.World.PlayerPartyRuntime Party(SimulationWorld world)
        {
            var actor = world.Entities.All.FirstOrDefault(e => (e.Tags & XianXia.Core.Entities.EntityTag.Character) != 0);
            if (actor == null) actor = world.Entities.CreateCharacter(new DefinitionId("test", "farmer"), "Farmer").Value;
            var party = new XianXia.Core.World.PlayerPartyRuntime();
            party.BindWorld(world);
            Assert.IsTrue(party.TryInitialize(actor.Id, out var failure), failure);
            SquadMembershipService.EnsureSingletonsForUnassignedCharacters(world);
            return party;
        }

        static OutdoorConstructedAssetState Candidate() => new OutdoorConstructedAssetState {
            StableAssetId = "asset:runtime:farm:1", BuildingId = Farm, Kind = "grainField", SurfaceId = Surface,
            WorldX = 10, WorldY = 10, WorldWidth = 5, WorldHeight = 4, CellsW = 5, CellsH = 4,
            BoundLocationId = "location:runtime:farm:asset:runtime:farm:1"
        };

        [Test]
        public void WholeFootprintAcceptsTwoOwnSitesButRejectsOneUnmanagedOrForeignCell()
        {
            var w = World();
            Site(w, "A", 12, 4); // x10..14; last column x14.5 is initially unmanaged
            Assert.IsTrue(OutdoorAdministrativeConstructionAuthorizationService.Validate(w, Faction, Candidate()).IsFailure);
            var b = Site(w, "B", 15, 2);
            Assert.IsTrue(OutdoorAdministrativeConstructionAuthorizationService.Validate(w, Faction, Candidate()).IsSuccess);
            b.OwnerFactionId = "test:foreign";
            Assert.IsTrue(OutdoorAdministrativeConstructionAuthorizationService.Validate(w, Faction, Candidate()).IsFailure);
        }

        [Test]
        public void ConstructionPaysFiveRegistersTwentyCellsAndRejectsOverlapWithoutMutation()
        {
            var w = World();
            Assert.IsTrue(ConstructionService.TryConstructFarmField(w, Farm, Faction, Surface, 10, 10, out _).IsFailure);
            Assert.AreEqual(20, w.Inventory.GetCount(Wood));
            Site(w, "A", 12, 20);
            var result = ConstructionService.TryConstructFarmField(w, Farm, Faction, Surface, 10, 10, out var id);
            Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error.ToString() : "");
            Assert.AreEqual(15, w.Inventory.GetCount(Wood));
            var asset = w.OutdoorConstructedAssets.Assets[id];
            Assert.AreEqual(20, asset.CellAnchors().Select(a => a.StableAssetId).Distinct().Count());
            Assert.AreEqual(20, w.OutdoorAdministrativeAssetAnchors.Anchors.Count);
            Assert.IsTrue(ConstructionService.TryConstructFarmField(w, Farm, Faction, Surface, 10, 10, out _).IsFailure);
            Assert.AreEqual(15, w.Inventory.GetCount(Wood));
            Assert.AreEqual(2, w.OutdoorConstructedAssets.NextSequence);
            Assert.IsTrue(ConstructionService.TryConstructFarmField(w, Farm, Faction, Surface, 16, 10, out var second).IsSuccess);
            Assert.AreNotEqual(asset.BoundLocationId, w.OutdoorConstructedAssets.Assets[second].BoundLocationId);
        }

        [Test]
        public void FarmSurvivesManagerSuccessionAndUnmanagedCropStillGrows()
        {
            var w = World();
            var a = Site(w, "A", 12, 20);
            var b = Site(w, "B", 12, 20);
            Assert.IsTrue(ConstructionService.TryConstructFarmField(w, Farm, Faction, Surface, 10, 10, out var id).IsSuccess);
            var cellId = OutdoorStatefulObjectId.ForCell(id, 0, 0);
            w.OutdoorStatefulObjects.SetFarmPlot(cellId, "crop_grain", OutdoorFarmCropStage.Growing, .25f);
            a.IsCoreActive = false;
            var auth = WorldAdministrativeAssetAuthorizationService.ResolveForFaction(w, cellId, Faction);
            Assert.IsTrue(auth.IsAllowed);
            Assert.AreSame(b, auth.ManagingSite);
            b.IsCoreActive = false;
            Assert.AreEqual(AdministrativeAssetAuthorizationStatus.Unmanaged,
                WorldAdministrativeAssetAuthorizationService.ResolveForFaction(w, cellId, Faction).Status);
            new SimulationLoop(w).TickOnce();
            Assert.IsTrue(w.OutdoorStatefulObjects.TryGetFarmPlot(cellId, out var crop));
            Assert.Greater(crop.Growth, .25f);
            b.IsCoreActive = true;
            Assert.IsTrue(WorldAdministrativeAssetAuthorizationService.ResolveForFaction(w, cellId, Faction).IsAllowed);
            b.OwnerFactionId = "test:foreign";
            Assert.AreEqual(AdministrativeAssetAuthorizationStatus.ManagedByOtherFaction,
                WorldAdministrativeAssetAuthorizationService.ResolveForFaction(w, cellId, Faction).Status);
            Assert.AreEqual(id, w.OutdoorConstructedAssets.Assets[id].StableAssetId);
            Assert.AreEqual(1, w.OutdoorStatefulObjects.FarmPlots.Count);
        }

        [Test]
        public void SnapshotPersistsPhysicalFarmCropAndSequenceWithoutManagerFields()
        {
            var w = World();
            Site(w, "A", 12, 20);
            Assert.IsTrue(ConstructionService.TryConstructFarmField(w, Farm, Faction, Surface, 10, 10, out var id).IsSuccess);
            var cellId = OutdoorStatefulObjectId.ForCell(id, 4, 3);
            w.OutdoorStatefulObjects.SetFarmPlot(cellId, "crop_grain", OutdoorFarmCropStage.Growing, .37f);
            // This fixture uses content-shell Sites; isolate placement persistence from unrelated political shell restore.
            w.Strategic.Sites.Clear();
            TerritoryClaimService.ResetForContentBootstrap(w);
            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(w, new SimulationLoop(w), Party(w));
            Assert.IsTrue(json.IsSuccess);
            var restored = service.RestoreJson(json.Value);
            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : "");
            var rw = restored.Value.world;
            Assert.AreEqual(15, rw.Inventory.GetCount(Wood));
            Assert.AreEqual(10, rw.OutdoorConstructedAssets.Assets[id].WorldX);
            Assert.AreEqual(2, rw.OutdoorConstructedAssets.NextSequence);
            Assert.IsTrue(rw.OutdoorStatefulObjects.TryGetFarmPlot(cellId, out var crop));
            Assert.AreEqual(.37f, crop.Growth);
            var node = XianXia.Data.Serialization.SimpleJson.Parse(json.Value);
            Assert.IsTrue(node.TryGetProperty("outdoorConstructedAssets", out var assets));
            var farmJson = XianXia.Data.Serialization.SimpleJson.Stringify(assets);
            StringAssert.DoesNotContain("manager", farmJson.ToLowerInvariant());
            StringAssert.DoesNotContain("siteId", farmJson);
            StringAssert.DoesNotContain("claim", farmJson.ToLowerInvariant());
            rw.SurfaceSpatial.Register(w.SurfaceSpatial.Registered[Surface]);
            Assert.IsTrue(OutdoorAdministrativeAssetAnchorBootstrap.Rehydrate(rw, new DefinitionRegistry()).IsSuccess);
            Assert.AreEqual(20, rw.OutdoorAdministrativeAssetAnchors.Anchors.Count);
        }

        [Test]
        public void StrictContentMapsFarmAndNewGameInventoryIsNotGrantedByRestoreShell()
        {
            var loaded = new ContentPackageLoader().Load(new[] { ContentPath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : "");
            var started = new PlayableDayBootstrap().Start(loaded.Value, new PlayableDayOptions { OpeningScenarioId = "base:scenario_ch01_reference" });
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");
            var w = started.Value.World;
            Assert.AreEqual(20, w.Inventory.GetCount(Wood));
            Assert.IsTrue(w.ConstructionCatalog.TryGet(Farm, out var spec));
            Assert.AreEqual(ConstructionPlacementKind.FarmField, spec.PlacementKind);
            Assert.AreEqual(5, spec.FootprintCellsW); Assert.AreEqual(4, spec.FootprintCellsH);
            Assert.AreEqual(5, spec.Costs.Single().Count);
            w.Inventory.TryRemoveAll(Wood, 7);
            var service = new SnapshotService(new JsonSnapshotSerializer());
            var saved = service.CaptureJson(w, started.Value.Loop, Party(w));
            Assert.IsTrue(saved.IsSuccess);
            w.Inventory.TryRemoveAll(Wood, 3);
            var restored = service.RestoreJson(saved.Value);
            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : "");
            var shell = RuntimeContentShellBootstrap.Rehydrate(restored.Value.world, loaded.Value.Registry);
            Assert.IsTrue(shell.IsSuccess, shell.IsFailure ? shell.Error.ToString() : "");
            Assert.AreEqual(13, restored.Value.world.Inventory.GetCount(Wood));
            Assert.IsTrue(RuntimeContentShellBootstrap.Rehydrate(restored.Value.world, loaded.Value.Registry).IsSuccess);
            Assert.AreEqual(13, restored.Value.world.Inventory.GetCount(Wood));
        }

        [Test]
        public void OpeningInventoryCapacityFailureRollsBackPartialEntry()
        {
            var w = new SimulationWorld();
            w.InventoryCatalog.Register(Wood, "粗木", 99, null);
            w.Inventory.SetSlotCapacity(1);
            w.Inventory.TryAddAll(Wood, 98);
            var scenario = new OpeningScenarioDefinition { Id = new DefinitionId("test", "opening") };
            scenario.StartingInventory.Add(new OpeningStartingInventoryEntry { ItemId = Wood, Count = 20 });
            Assert.IsTrue(OpeningInventoryBootstrap.Apply(w, scenario).IsFailure);
            Assert.AreEqual(98, w.Inventory.GetCount(Wood));
        }

        [Test]
        public void NpcHarvestReauthorizesAndDepositsIntoCurrentManagingSiteOnly()
        {
            const string grain = "base:resource_grain";
            var w = World();
            var a = Site(w, "A", 12, 20);
            Assert.IsTrue(ConstructionService.TryConstructFarmField(
                w, Farm, Faction, Surface, 10, 10, out var farmId).IsSuccess);
            var cellId = OutdoorStatefulObjectId.ForCell(farmId, 0, 0);
            var npc = w.Entities.CreateNpc(new DefinitionId("test", "real_farmer"), "Farmer").Value;
            npc.Get<FactionMembershipComponent>().Assign(Faction, FactionRoleKind.Member);

            var first = WorldSiteFarmHarvestService.TryDepositNpcHarvest(w, npc.Id, cellId, grain);
            Assert.IsTrue(first.IsSuccess, first.IsFailure ? first.Error.ToString() : "");
            Assert.AreEqual(1, WorldSitePublicStockService.GetCount(w, a.SiteId, grain));
            Assert.AreEqual(0, w.Inventory.GetCount(grain), "NPC schedule harvest never enters Party Inventory.");

            var b = Site(w, "B", 12, 20);
            a.IsCoreActive = false;
            var second = WorldSiteFarmHarvestService.TryDepositNpcHarvest(w, npc.Id, cellId, grain);
            Assert.IsTrue(second.IsSuccess, second.IsFailure ? second.Error.ToString() : "");
            Assert.AreEqual(1, WorldSitePublicStockService.GetCount(w, a.SiteId, grain));
            Assert.AreEqual(1, WorldSitePublicStockService.GetCount(w, b.SiteId, grain));

            b.OwnerFactionId = "test:foreign";
            Assert.IsTrue(WorldSiteFarmHarvestService.TryDepositNpcHarvest(
                w, npc.Id, cellId, grain).IsFailure);
            Assert.AreEqual(Faction, npc.Get<FactionMembershipComponent>().FactionId);
            Assert.AreEqual(1, WorldSitePublicStockService.GetCount(w, b.SiteId, grain));
        }

        [Test]
        public void WorldTickGrowsCropWithoutPhantomPublicStockProduction()
        {
            const string grain = "base:resource_grain";
            var w = World();
            Site(w, "A", 12, 20);
            Assert.IsTrue(ConstructionService.TryConstructFarmField(
                w, Farm, Faction, Surface, 10, 10, out var farmId).IsSuccess);
            var cellId = OutdoorStatefulObjectId.ForCell(farmId, 0, 0);
            w.OutdoorStatefulObjects.SetFarmPlot(cellId, "crop_grain", OutdoorFarmCropStage.Growing, .25f);
            Assert.IsTrue(new SimulationLoop(w).TickOnce().IsSuccess);
            Assert.Greater(w.OutdoorStatefulObjects.FarmPlots[cellId].Growth, .25f);
            Assert.AreEqual(0, WorldSitePublicStockService.GetCount(w, "A", grain));
        }

        [Test]
        public void FarmMetricUsesSurfaceCellSizeAndOriginAndRejectsOffGrid()
        {
            var w = World();
            w.SurfaceSpatial.Clear();
            w.SurfaceSpatial.Register(new OutdoorSurfaceSpatialMetric(Surface, 6.1f, 2.3f, .028f, 1.4f, 1.4f, new[] { new SurfaceChunkCoord(0, 0) }));
            var farm = Candidate();
            farm.WorldX = 6.1f + 5 * .028f; farm.WorldY = 2.3f + 6 * .028f;
            farm.WorldWidth = 5 * .028f; farm.WorldHeight = 4 * .028f;
            Assert.IsTrue(OutdoorAdministrativeConstructionAuthorizationService.ValidatePhysicalPlacement(w, farm).IsSuccess);
            farm.WorldX += .014f;
            Assert.IsTrue(OutdoorAdministrativeConstructionAuthorizationService.ValidatePhysicalPlacement(w, farm).IsFailure);
        }

        [Test]
        public void LegacyOptionalFieldsLoadButCorruptConstructedAssetsFail()
        {
            var serializer = new JsonSnapshotSerializer();
            var service = new SnapshotService(serializer);
            var world = new SimulationWorld();
            var saved = service.CaptureJson(world, new SimulationLoop(world), Party(world));
            Assert.IsTrue(saved.IsSuccess);
            var node = XianXia.Data.Serialization.SimpleJson.Parse(saved.Value);
            node.Object.Remove("outdoorConstructedAssets");
            node.Object.Remove("nextOutdoorConstructedAssetSequence");
            Assert.IsTrue(service.RestoreJson(XianXia.Data.Serialization.SimpleJson.Stringify(node)).IsSuccess);
            var corrupt = saved.Value.Replace("\"outdoorConstructedAssets\":[]", "\"outdoorConstructedAssets\":null");
            Assert.IsTrue(serializer.Deserialize(corrupt).IsFailure);
        }
    }
}
