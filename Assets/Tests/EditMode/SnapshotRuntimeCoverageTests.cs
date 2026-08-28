using NUnit.Framework;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Data.Serialization;

namespace XianXia.Tests
{
    /// <summary>Snapshot Runtime Mutable Truth coverage：Faction／Vitals／Inventory roundtrip.</summary>
    public sealed class SnapshotRuntimeCoverageTests
    {
        const string FactionOppressor = "base:faction_oppressor_sect";
        const string FactionPlayer = "base:faction_player";

        static SimulationWorld CreateWorld()
        {
            var world = new SimulationWorld();
            world.Strategic.PlayerFactionId = FactionPlayer;
            HexTestWorldBootstrap.EnsureMinimalHexMap(world);
            return world;
        }

        static EntityId SpawnCharacter(SimulationWorld world, string name, string factionId)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            var entity = created.Value;
            entity.Get<FactionMembershipComponent>().Assign(factionId, FactionRoleKind.Member);
            if (entity.TryGet<AttributesComponent>(out var attrs))
                attrs.SetBase(AttributeId.MaxHp, 100);
            CombatDamageRules.EnsureVitals(entity);
            return entity.Id;
        }

        [Test]
        public void SNAP_COV_01_FactionMembership_SurvivesCaptureRestore()
        {
            var world = CreateWorld();
            var id = SpawnCharacter(world, "Recruit", FactionOppressor);
            Assert.IsTrue(world.Entities.TryGet(id, out var entity));
            entity.Get<FactionMembershipComponent>().Assign(FactionPlayer, FactionRoleKind.Member);

            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, new SimulationLoop(world));
            Assert.IsTrue(json.IsSuccess);
            StringAssert.Contains("\"factionId\":\"" + FactionPlayer + "\"", json.Value);

            var restored = service.RestoreJson(json.Value);
            Assert.IsTrue(restored.IsSuccess);
            Assert.IsTrue(restored.Value.world.Entities.TryGet(id, out var after));
            Assert.IsTrue(after.TryGet<FactionMembershipComponent>(out var mem));
            Assert.AreEqual(FactionPlayer, mem.FactionId);
            Assert.AreEqual(FactionRoleKind.Member, mem.Role);
        }

        [Test]
        public void SNAP_COV_02_CombatVitalsAndIncapacitated_SurviveCaptureRestore()
        {
            var world = CreateWorld();
            var id = SpawnCharacter(world, "Wounded", FactionPlayer);
            Assert.IsTrue(world.Entities.TryGet(id, out var entity));
            CombatDamageRules.EnsureVitals(entity);
            Assert.IsTrue(entity.TryGet<CombatVitalsComponent>(out var vitals));
            vitals.CurrentHp = 17;
            vitals.CurrentSpiritPower = 3;
            vitals.PoolsInitialized = true;
            var life = entity.Get<LifecycleComponent>();
            life.State = LifecycleState.Incapacitated;
            life.BleedOutAfterTick = 9001;

            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, new SimulationLoop(world));
            Assert.IsTrue(json.IsSuccess);

            var restored = service.RestoreJson(json.Value);
            Assert.IsTrue(restored.IsSuccess);
            Assert.IsTrue(restored.Value.world.Entities.TryGet(id, out var after));
            Assert.IsTrue(after.TryGet<CombatVitalsComponent>(out var vitals2));
            Assert.AreEqual(17, vitals2.CurrentHp);
            Assert.AreEqual(3, vitals2.CurrentSpiritPower);
            Assert.IsTrue(vitals2.PoolsInitialized);
            Assert.IsTrue(after.TryGet<LifecycleComponent>(out var life2));
            Assert.AreEqual(LifecycleState.Incapacitated, life2.State);
            Assert.AreEqual(9001UL, life2.BleedOutAfterTick);

            CombatDamageRules.EnsureVitals(after);
            Assert.AreEqual(17, after.Get<CombatVitalsComponent>().CurrentHp);
        }

        [Test]
        public void SNAP_COV_03_PartyInventory_SurvivesCaptureRestore()
        {
            var world = CreateWorld();
            SpawnCharacter(world, "Carrier", FactionPlayer);
            world.InventoryCatalog.Register("test:rough_wood", "粗木", 99, new[] { "resource", "wood" });
            Assert.AreEqual(5, world.Inventory.TryAdd("test:rough_wood", 5));

            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, new SimulationLoop(world));
            Assert.IsTrue(json.IsSuccess);
            StringAssert.Contains("partyInventorySlots", json.Value);

            var restored = service.RestoreJson(json.Value);
            Assert.IsTrue(restored.IsSuccess);
            restored.Value.world.InventoryCatalog.Register(
                "test:rough_wood", "粗木", 99, new[] { "resource", "wood" });
            Assert.AreEqual(5, restored.Value.world.Inventory.GetCount("test:rough_wood"));
        }

        [Test]
        public void SNAP_COV_04_Fingerprint_IncludesFactionAndHpAfterRoundtrip()
        {
            var world = CreateWorld();
            var a = SpawnCharacter(world, "A", FactionOppressor);
            var b = SpawnCharacter(world, "B", FactionPlayer);
            Assert.IsTrue(world.Entities.TryGet(a, out var entA));
            entA.Get<FactionMembershipComponent>().Assign(FactionPlayer, FactionRoleKind.Member);
            CombatDamageRules.EnsureVitals(entA);
            entA.Get<CombatVitalsComponent>().CurrentHp = 42;
            entA.Get<CombatVitalsComponent>().PoolsInitialized = true;

            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryRestoreFromSnapshot(a, new[] { a, b }, out _));

            var before = SnapshotRuntimeFingerprint.Build(world, party);
            StringAssert.Contains("faction=" + FactionPlayer, before);
            StringAssert.Contains("hp=42", before);

            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, new SimulationLoop(world), party);
            Assert.IsTrue(json.IsSuccess);

            var restored = service.RestoreJson(json.Value);
            Assert.IsTrue(restored.IsSuccess);
            var world2 = restored.Value.world;
            var parsed = new JsonSnapshotSerializer().Deserialize(json.Value);
            Assert.IsTrue(parsed.IsSuccess);
            var party2 = new PlayerPartyRuntime();
            PlayerPartySnapshotRestore.Apply(world2, party2, parsed.Value.Strategic?.PlayerParty);

            var after = SnapshotRuntimeFingerprint.Build(world2, party2);
            StringAssert.Contains("faction=" + FactionPlayer, after);
            StringAssert.Contains("hp=42", after);
            Assert.AreEqual(2, party2.Count);
            Assert.AreEqual(a, party2.ActiveCharacterId);
        }

        [Test]
        public void SNAP_COV_05_PrototypeBanditEntitiesAndStacks_SurviveFullRestore()
        {
            var world = CreateWorld();
            Ch01ScenarioStrategicSetup.Apply(world);
            Ch01ScenarioStrategicSetup.PositionPrototypeTestBanditArmies(world);

            Assert.IsTrue(
                world.Strategic.FormalArmies.TryGet(ArmyStackAdapter.BanditPatrolFormalArmyId, out var banditArmy));
            Assert.Greater(banditArmy.MemberCharacterIds.Count, 0);
            Assert.IsTrue(
                world.Strategic.Armies.TryGet(ArmyStackAdapter.BanditPatrolStackId, out var stackBefore));
            Assert.AreEqual(ArmyStackAdapter.BanditPatrolFormalArmyId, stackBefore.FormalArmyId);

            var leaderId = new EntityId(banditArmy.LeaderCharacterId.Value);
            Assert.IsTrue(world.Entities.TryGet(leaderId, out var leaderBefore));
            Assert.AreEqual("BanditLeader", leaderBefore.DisplayName);
            Assert.AreEqual(
                StrategicFactionCatalog.BanditId,
                leaderBefore.Get<FactionMembershipComponent>().FactionId);

            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, new SimulationLoop(world));
            Assert.IsTrue(json.IsSuccess);

            var restored = service.RestoreJson(json.Value);
            Assert.IsTrue(restored.IsSuccess);
            var world2 = restored.Value.world;

            Assert.IsTrue(world2.Entities.TryGet(leaderId, out var leaderAfter));
            Assert.AreEqual("BanditLeader", leaderAfter.DisplayName);
            Assert.AreEqual(
                StrategicFactionCatalog.BanditId,
                leaderAfter.Get<FactionMembershipComponent>().FactionId);
            Assert.IsTrue(
                world2.Strategic.FormalArmies.TryGet(ArmyStackAdapter.BanditPatrolFormalArmyId, out var banditArmyAfter));
            Assert.AreEqual(banditArmy.MemberCharacterIds.Count, banditArmyAfter.MemberCharacterIds.Count);
            Assert.IsTrue(
                world2.Strategic.Armies.TryGet(ArmyStackAdapter.BanditPatrolStackId, out var stackAfter));
            Assert.AreEqual(ArmyStackAdapter.BanditPatrolFormalArmyId, stackAfter.FormalArmyId);
            Assert.IsTrue(world2.WorldPresence.TryGet(leaderId, out _));
        }
    }
}
