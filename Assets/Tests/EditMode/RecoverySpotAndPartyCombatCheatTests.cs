using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using XianXia.Core.Actions;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Construction;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Orders;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;
using XianXia.Core.World.Surface;
using XianXia.Data.Content;
using XianXia.Data.Serialization;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    public sealed class RecoverySpotAndPartyCombatCheatTests
    {
        const string Surface = "test:surface";
        const string Faction = "test:player";
        const string Building = "base:building_recovery_spot";
        const string Wood = "base:resource_rough_wood";
        static string ContentPath => Environment.GetEnvironmentVariable("XIANXIA_BASEGAME") ??
                                     Path.GetFullPath("Content/BaseGame");

        static Entity Character(SimulationWorld world, int hp = 100, int spirit = 40)
        {
            var entity = world.Entities.CreateCharacter(new DefinitionId("test", "character"), "测试角色").Value;
            var attrs = entity.Get<AttributesComponent>();
            attrs.SetBase(AttributeId.MaxHp, hp);
            attrs.SetBase(AttributeId.SpiritPower, spirit);
            CombatDamageRules.EnsureVitals(entity);
            var vitals = entity.Get<CombatVitalsComponent>();
            vitals.CurrentHp = hp;
            vitals.CurrentSpiritPower = spirit;
            vitals.PoolsInitialized = true;
            return entity;
        }

        [Test]
        public void RecoveryCompletesAtSixTicksAndCancellationDoesNotChangePools()
        {
            var world = new SimulationWorld();
            var entity = Character(world, 150, 80);
            var vitals = entity.Get<CombatVitalsComponent>();
            vitals.CurrentHp = 40;
            vitals.CurrentSpiritPower = 10;
            var cancelled = new RecoveryAction(new ActionId(1), entity.Id, new OrderId(1), 6, "spot:a");
            Assert.IsTrue(cancelled.Start(world).IsSuccess);
            for (var i = 0; i < 3; i++) cancelled.Advance(world);
            cancelled.Cancel();
            Assert.AreEqual(40, vitals.CurrentHp);
            Assert.AreEqual(10, vitals.CurrentSpiritPower);

            var completed = new RecoveryAction(new ActionId(2), entity.Id, new OrderId(2), 6, "spot:a");
            Assert.IsTrue(completed.Start(world).IsSuccess);
            for (var i = 0; i < 6; i++) Assert.IsTrue(completed.Advance(world).IsSuccess);
            Assert.AreEqual(ActionStatus.Completed, completed.Status);
            Assert.AreEqual(150, vitals.CurrentHp);
            Assert.AreEqual(80, vitals.CurrentSpiritPower);
        }

        [Test]
        public void RecoveryRejectsUnavailableAndAlreadyFullCharacters()
        {
            var world = new SimulationWorld();
            var full = Character(world);
            Assert.IsTrue(CombatRecoveryService.CanRecover(full).IsFailure);
            full.Get<LifecycleComponent>().State = LifecycleState.Incapacitated;
            Assert.IsTrue(CombatRecoveryService.CanRecover(full).IsFailure);
            full.Get<LifecycleComponent>().State = LifecycleState.Dead;
            Assert.IsTrue(CombatRecoveryService.RestoreToMaximum(full).IsFailure);
            Assert.AreEqual(LifecycleState.Dead, full.Get<LifecycleComponent>().State);
        }

        [Test]
        public void IncreasedMaximumDoesNotRefillUntilRecoveryCompletes()
        {
            var world = new SimulationWorld();
            var entity = Character(world);
            var attrs = entity.Get<AttributesComponent>();
            var vitals = entity.Get<CombatVitalsComponent>();
            attrs.SetBase(AttributeId.MaxHp, 150);
            attrs.SetBase(AttributeId.SpiritPower, 70);
            vitals.SyncMaxFromAttributes(attrs, true);
            Assert.AreEqual(100, vitals.CurrentHp);
            Assert.AreEqual(40, vitals.CurrentSpiritPower);
            Assert.IsTrue(CombatRecoveryService.RestoreToMaximum(entity).IsSuccess);
            Assert.AreEqual(150, vitals.CurrentHp);
            Assert.AreEqual(70, vitals.CurrentSpiritPower);
        }

        [Test]
        public void RecoveryConstructionUsesActualControlAndNoAdministrativeAnchors()
        {
            var world = new SimulationWorld();
            world.SurfaceSpatial.Register(new OutdoorSurfaceSpatialMetric(
                Surface, 0, 0, 1, 50, 50, new[] { new SurfaceChunkCoord(0, 0) }));
            world.Strategic.PlayerFactionId = Faction;
            world.InventoryCatalog.Register(Wood, "粗木", 99, null);
            world.Inventory.TryAddAll(Wood, 20);
            var spec = new BuildingConstructionSpec {
                BuildingId = Building, DisplayName = "恢复处", PlacementKind = ConstructionPlacementKind.RecoverySpot,
                OutdoorKind = "recoverySpot", FootprintCellsW = 2, FootprintCellsH = 2, UnlockedByDefault = true };
            spec.Costs.Add(new ConstructionMaterialCost { ItemId = Wood, Count = 5 });
            world.ConstructionCatalog.Register(spec);
            Assert.IsTrue(ConstructionService.TryConstructRecoverySpot(
                world, Building, Faction, Surface, 10, 10, out _).IsFailure);
            var a = Site(world, "A", 10.5f, 1f);
            var b = Site(world, "B", 11.5f, 1f);
            Assert.IsTrue(ConstructionService.TryConstructRecoverySpot(
                world, Building, Faction, Surface, 10, 10, out var id).IsSuccess);
            Assert.AreEqual("asset:runtime:recovery:1", id);
            Assert.AreEqual(15, world.Inventory.GetCount(Wood));
            Assert.AreEqual(0, world.OutdoorAdministrativeAssetAnchors.Anchors.Count);
            b.OwnerFactionId = "test:enemy";
            Assert.IsTrue(ConstructionService.TryConstructRecoverySpot(
                world, Building, Faction, Surface, 14, 10, out _).IsFailure);
            Assert.AreEqual(15, world.Inventory.GetCount(Wood));
            Assert.IsTrue(a.IsCoreActive);
        }

        [Test]
        public void RuntimeRecoveryAndHalfFinishedActionRoundTripThroughSnapshot()
        {
            var world = new SimulationWorld();
            var entity = Character(world);
            entity.Get<CombatVitalsComponent>().CurrentHp = 20;
            var asset = new OutdoorConstructedAssetState {
                StableAssetId = "asset:runtime:recovery:1", BuildingId = Building, Kind = "recoverySpot",
                SurfaceId = Surface, WorldX = 2, WorldY = 3, WorldWidth = 2, WorldHeight = 2,
                CellsW = 2, CellsH = 2, BoundLocationId = "location:runtime:recovery:asset:runtime:recovery:1" };
            Assert.IsTrue(world.OutdoorConstructedAssets.TryRegister(asset));
            Assert.IsTrue(world.OutdoorConstructedAssets.RestoreSequence(2));
            var action = new RecoveryAction(new ActionId(1), entity.Id, new OrderId(1), 6, asset.StableAssetId);
            Assert.IsTrue(action.Start(world).IsSuccess);
            for (var i = 0; i < 3; i++) action.Advance(world);
            world.ActiveActions[action.Id] = action;
            var state = entity.Get<ActionStateComponent>();
            state.ActiveActionId = action.Id;
            state.ActiveClock = action.Clock;
            var snapshots = new SnapshotService(new JsonSnapshotSerializer());
            var party = new XianXia.Core.World.PlayerPartyRuntime();
            party.BindWorld(world);
            Assert.IsTrue(party.TryInitialize(entity.Id, out var partyError), partyError);
            var json = snapshots.CaptureJson(world, new SimulationLoop(world), party);
            Assert.IsTrue(json.IsSuccess, json.IsFailure ? json.Error.ToString() : string.Empty);
            var restored = snapshots.RestoreJson(json.Value);
            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : string.Empty);
            Assert.IsTrue(restored.Value.world.OutdoorConstructedAssets.Assets.ContainsKey(asset.StableAssetId));
            var restoredAction = restored.Value.world.ActiveActions.Values.OfType<RecoveryAction>().Single();
            Assert.AreEqual(3UL, restoredAction.Clock.RemainingTicks);
            Assert.AreEqual(asset.StableAssetId, restoredAction.RecoverySpotId);
            for (var i = 0; i < 3; i++) Assert.IsTrue(restored.Value.loop.TickOnce().IsSuccess);
            var restoredEntity = restored.Value.world.Entities.All.Single();
            Assert.AreEqual(100, restoredEntity.Get<CombatVitalsComponent>().CurrentHp);
        }

        [Test]
        public void StrictContentContainsRecoveryDefinitionAndThreeHuangcunPlacements()
        {
            var loaded = new ContentPackageLoader().Load(new[] { ContentPath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : string.Empty);
            Assert.IsTrue(loaded.Value.Registry.TryGetBuilding(
                new DefinitionId("base", "building_recovery_spot"), out var definition));
            Assert.AreEqual("recoverySpot", definition.PlacementKind);
            Assert.AreEqual(2, definition.FootprintCellsW);
            var surface = loaded.Value.Registry.OutdoorSurfaces[
                new DefinitionId("base", "surface_main_wilderness_v1")];
            Assert.AreEqual(3, surface.SitePlacements.Count(p =>
                p != null && p.Kind == "recoverySpot" && p.SiteId == "base:site_huangcun"));
        }

        [Test]
        public void PartyCombatCheatsModifyOnlyExplicitPartyMembersAndNeverReviveDead()
        {
            var world = new SimulationWorld();
            var first = Character(world, 220, 60);
            var second = Character(world, 120, 30);
            var enemy = Character(world, 300, 90);
            first.Get<AttributesComponent>().SetBase(AttributeId.Attack, 28);
            second.Get<AttributesComponent>().SetBase(AttributeId.Attack, 13);
            enemy.Get<AttributesComponent>().SetBase(AttributeId.Attack, 99);
            var members = new[] { first.Id, second.Id };
            var attack = LevelTesterPartyCombatCheats.AddAttack(world, members);
            Assert.AreEqual(2, attack.Succeeded);
            Assert.AreEqual(38, first.Get<AttributesComponent>().GetBase(AttributeId.Attack));
            Assert.AreEqual(23, second.Get<AttributesComponent>().GetBase(AttributeId.Attack));
            Assert.AreEqual(99, enemy.Get<AttributesComponent>().GetBase(AttributeId.Attack));

            first.Get<CombatVitalsComponent>().CurrentHp = 1;
            second.Get<CombatVitalsComponent>().CurrentHp = 2;
            LevelTesterPartyCombatCheats.AddMaxHpAndRefill(world, members);
            Assert.AreEqual(270, first.Get<AttributesComponent>().GetFinal(AttributeId.MaxHp));
            Assert.AreEqual(170, second.Get<AttributesComponent>().GetFinal(AttributeId.MaxHp));
            Assert.AreEqual(270, first.Get<CombatVitalsComponent>().CurrentHp);
            Assert.AreEqual(170, second.Get<CombatVitalsComponent>().CurrentHp);
            Assert.AreEqual(300, enemy.Get<AttributesComponent>().GetFinal(AttributeId.MaxHp));

            second.Get<LifecycleComponent>().State = LifecycleState.Dead;
            second.Get<CombatVitalsComponent>().CurrentHp = 0;
            first.Get<CombatVitalsComponent>().CurrentHp = 10;
            var refill = LevelTesterPartyCombatCheats.Refill(world, members);
            Assert.AreEqual(1, refill.Succeeded);
            Assert.AreEqual(1, refill.Skipped);
            Assert.AreEqual(LifecycleState.Dead, second.Get<LifecycleComponent>().State);
            Assert.AreEqual(0, second.Get<CombatVitalsComponent>().CurrentHp);
        }

        static WorldSite Site(SimulationWorld world, string id, float centerX, float width)
        {
            var site = new WorldSite { SiteId = id, OwnerFactionId = Faction, CoreSurfaceId = Surface,
                CoreAssetId = id + ":core", HasCoreWorldPosition = true, CoreWorldX = centerX, CoreWorldY = 11,
                CoreRangeWidth = width, CoreRangeHeight = 4, CoreLevel = 1, IsCoreActive = true };
            world.Strategic.Sites.Register(site);
            Assert.IsTrue(TerritoryClaimService.CreateInitialClaim(world, site).IsSuccess);
            return site;
        }
    }
}
