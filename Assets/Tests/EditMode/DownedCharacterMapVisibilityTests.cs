using NUnit.Framework;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    public sealed class DownedCharacterMapVisibilityTests
    {
        const string TestFactionA = "test:faction_a";
        const string TestSiteA = "base:site_huangcun";
        static readonly HexCoord TestHexA = Ch01HexPrototypeMapBuilder.HuangcunHex;

        static SimulationWorld CreateHexWorld()
        {
            var world = new SimulationWorld();
            HexTestWorldBootstrap.EnsureMinimalHexMap(world);
            return world;
        }

        static EntityId SpawnCharacter(SimulationWorld world, string name, string siteId)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            var entity = created.Value;
            entity.Get<FactionMembershipComponent>().Assign(TestFactionA, FactionRoleKind.Member);
            world.WorldPresence.SetAtSite(entity.Id, siteId);
            return entity.Id;
        }

        static void EnterIncapacitated(SimulationWorld world, EntityId id)
        {
            Assert.IsTrue(world.Entities.TryGet(id, out var entity));
            CombatDamageRules.EnsureVitals(entity);
            if (entity.TryGet<CombatVitalsComponent>(out var vitals))
                vitals.CurrentHp = 0;
            Assert.IsTrue(CombatLifeStateService.TryEnterIncapacitated(world, entity));
        }

        static BattleParticipantSnapshot SnapAtHex(HexCoord hex, string attackerArmyId = "")
        {
            var snap = new BattleParticipantSnapshot { AttackerArmyId = attackerArmyId ?? string.Empty };
            ArmyHexBattleAnchorService.SetBattleAnchorHex(snap, hex);
            return snap;
        }

        [Test]
        public void DOWNED_VIS_01_DownedCharacterRemainsInDomainAfterBattleSync()
        {
            var world = CreateHexWorld();
            var solo = SpawnCharacter(world, "Solo", TestSiteA);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestSiteA, new[] { solo });
            Assert.IsTrue(armyResult.IsSuccess);
            ArmyHexTravelService.InitializeArmyAtHex(armyResult.Value, TestHexA);
            EnterIncapacitated(world, solo);

            var snap = SnapAtHex(TestHexA, armyResult.Value.ArmyId);
            StrategicEncounterResolveService.PlaceAtBattleAnchor(
                world, world.WorldPresence.GetOrCreate(solo), snap);
            ArmyPostBattleSyncService.SyncAttackerArmyAfterBattle(world, snap);

            Assert.IsTrue(world.Entities.TryGet(solo, out var entity));
            Assert.IsTrue(entity.TryGet<LifecycleComponent>(out var life));
            Assert.IsTrue(life.IsIncapacitated);
            Assert.IsFalse(life.IsDead);
        }

        [Test]
        public void DOWNED_VIS_02_DownedDoesNotEqualDead()
        {
            var world = CreateHexWorld();
            var id = SpawnCharacter(world, "Hero", TestSiteA);
            EnterIncapacitated(world, id);
            Assert.IsTrue(LingeringBattlefieldPartyService.IsIncapacitated(world, id));
            Assert.IsTrue(LingeringBattlefieldPartyService.IsLingeringDowned(world, id));
            Assert.IsFalse(LingeringBattlefieldPartyService.IsVisibleCorpse(world, id));
        }

        [Test]
        public void DOWNED_VIS_03_DownedDetachedFromFormalArmyStillQueryable()
        {
            var world = CreateHexWorld();
            var solo = SpawnCharacter(world, "Solo", TestSiteA);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestSiteA, new[] { solo });
            Assert.IsTrue(armyResult.IsSuccess);
            ArmyHexTravelService.InitializeArmyAtHex(armyResult.Value, TestHexA);
            EnterIncapacitated(world, solo);
            ArmyPostBattleSyncService.SyncAttackerArmyAfterBattle(
                world,
                SnapAtHex(TestHexA, armyResult.Value.ArmyId));

            Assert.IsFalse(ArmyService.TryGetArmyForCharacter(world, solo, out _));
            Assert.IsTrue(LingeringBattlefieldPartyService.IsIncapacitated(world, solo));
        }

        [Test]
        public void DOWNED_VIS_04_PresentationQuery_IncludesDownedAtSite()
        {
            var world = CreateHexWorld();
            var solo = SpawnCharacter(world, "Solo", TestSiteA);
            EnterIncapacitated(world, solo);
            StrategicEncounterResolveService.PlaceAtBattleAnchor(
                world,
                world.WorldPresence.GetOrCreate(solo),
                SnapAtHex(TestHexA));

            Assert.IsTrue(ArmyWorldMapPresentation.ShouldDrawIndependentCharacterPortrait(world, solo));
        }

        [Test]
        public void DOWNED_VIS_05_DeadCorpseStillDistinctFromDowned()
        {
            var world = CreateHexWorld();
            var id = SpawnCharacter(world, "Dead", TestSiteA);
            Assert.IsTrue(world.Entities.TryGet(id, out var entity));
            CombatDamageRules.EnsureVitals(entity);
            Assert.IsTrue(CombatLifeStateService.TryEnterIncapacitated(world, entity));
            Assert.IsTrue(CombatLifeStateService.TryConfirmDeath(world, EntityId.None, entity, out _));
            Assert.IsTrue(LingeringBattlefieldPartyService.IsLingeringDowned(world, id));
            Assert.IsTrue(LingeringBattlefieldPartyService.IsVisibleCorpse(world, id));
            Assert.IsFalse(LingeringBattlefieldPartyService.IsIncapacitated(world, id));
        }

        [Test]
        public void DOWNED_VIS_06_HexAnchor_ResolvesDownedPortraitPosition()
        {
            var world = CreateHexWorld();
            var solo = SpawnCharacter(world, "Solo", TestSiteA);
            EnterIncapacitated(world, solo);
            var anchorHex = Ch01HexPrototypeMapBuilder.HuangcunHex;
            var snap = SnapAtHex(anchorHex);
            StrategicEncounterResolveService.PlaceAtBattleAnchor(
                world, world.WorldPresence.GetOrCreate(solo), snap);

            Assert.IsTrue(world.WorldPresence.TryGet(solo, out var wp));
            Assert.AreEqual(PartyWorldPresenceMode.AtHex, wp.Mode);
            Assert.IsTrue(
                WorldAgentMapPositionResolver.TryResolve(
                    world,
                    solo,
                    wp,
                    out var wx,
                    out var wy));
            HexMath.ToWorldPosition(anchorHex, world.HexWorld.HexSize, out var expectedX, out var expectedY);
            Assert.AreEqual(expectedX, wx, 0.01f);
            Assert.AreEqual(expectedY, wy, 0.01f);
            Assert.IsFalse(ArmyWorldMapPresentation.ShouldDrawIndependentCharacterPortrait(world, solo));
        }

        static EntityId SpawnEnemyNpc(SimulationWorld world, string name, string siteId)
        {
            var created = world.Entities.CreateNpc(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            var entity = created.Value;
            entity.Get<FactionMembershipComponent>().Assign("enemy:faction", FactionRoleKind.Member);
            world.WorldPresence.SetAtSite(entity.Id, siteId);
            return entity.Id;
        }
    }
}
