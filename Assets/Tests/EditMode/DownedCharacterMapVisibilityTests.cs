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
        const string TestNodeA = "test:node_a";
        const string TestRoute = "test:route_ab";

        static SimulationWorld CreateGraphWorld()
        {
            var world = new SimulationWorld();
            world.WorldGraph.RegisterNode(new WorldNodeState
            {
                Id = TestNodeA,
                Name = "A",
                OwnerId = TestFactionA,
                WorldX = 0f,
                WorldY = 0f
            });
            world.WorldGraph.RegisterNode(new WorldNodeState
            {
                Id = "test:node_b",
                Name = "B",
                OwnerId = TestFactionA,
                WorldX = 10f,
                WorldY = 0f
            });
            world.WorldGraph.RegisterRoute(new WorldRouteState
            {
                Id = TestRoute,
                FromNodeId = TestNodeA,
                ToNodeId = "test:node_b",
                TravelCost = 1
            });
            return world;
        }

        static SimulationWorld CreateHexWorld()
        {
            var world = CreateGraphWorld();
            HexTestWorldBootstrap.EnsureMinimalHexMap(world);
            return world;
        }

        static EntityId SpawnCharacter(SimulationWorld world, string name, string nodeId)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            var entity = created.Value;
            entity.Get<FactionMembershipComponent>().Assign(TestFactionA, FactionRoleKind.Member);
            world.WorldPresence.SetAtNode(entity.Id, nodeId);
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

        [Test]
        public void DOWNED_VIS_01_DownedCharacterRemainsInDomainAfterBattleSync()
        {
            var world = CreateGraphWorld();
            var solo = SpawnCharacter(world, "Solo", TestNodeA);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { solo });
            Assert.IsTrue(armyResult.IsSuccess);
            EnterIncapacitated(world, solo);

            var snap = new BattleParticipantSnapshot
            {
                AttackerArmyId = armyResult.Value.ArmyId,
                BattleAnchorNodeId = TestNodeA,
                BattleAnchorRouteId = TestRoute,
                BattleAnchorDestNodeId = "test:node_b",
                BattleAnchorProgress = 0.42f
            };
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
            var world = CreateGraphWorld();
            var id = SpawnCharacter(world, "Hero", TestNodeA);
            EnterIncapacitated(world, id);
            Assert.IsTrue(LingeringBattlefieldPartyService.IsIncapacitated(world, id));
            Assert.IsTrue(LingeringBattlefieldPartyService.IsLingeringDowned(world, id));
            Assert.IsFalse(LingeringBattlefieldPartyService.IsVisibleCorpse(world, id));
        }

        [Test]
        public void DOWNED_VIS_03_DownedDetachedFromFormalArmyStillQueryable()
        {
            var world = CreateGraphWorld();
            var solo = SpawnCharacter(world, "Solo", TestNodeA);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { solo });
            Assert.IsTrue(armyResult.IsSuccess);
            EnterIncapacitated(world, solo);
            ArmyPostBattleSyncService.SyncAttackerArmyAfterBattle(
                world,
                new BattleParticipantSnapshot
                {
                    AttackerArmyId = armyResult.Value.ArmyId,
                    BattleAnchorNodeId = TestNodeA,
                    BattleAnchorRouteId = TestRoute,
                    BattleAnchorDestNodeId = "test:node_b",
                    BattleAnchorProgress = 0.5f
                });

            Assert.IsFalse(ArmyService.TryGetArmyForCharacter(world, solo, out _));
            Assert.IsTrue(LingeringBattlefieldPartyService.IsIncapacitated(world, solo));
        }

        [Test]
        public void DOWNED_VIS_04_PresentationQuery_IncludesDownedAtNode()
        {
            var world = CreateGraphWorld();
            var solo = SpawnCharacter(world, "Solo", TestNodeA);
            EnterIncapacitated(world, solo);
            StrategicEncounterResolveService.PlaceAtBattleAnchor(
                world,
                world.WorldPresence.GetOrCreate(solo),
                new BattleParticipantSnapshot
                {
                    BattleAnchorNodeId = TestNodeA,
                    BattleAnchorRouteId = TestRoute,
                    BattleAnchorDestNodeId = "test:node_b",
                    BattleAnchorProgress = 0.33f
                });

            // Legacy graph residual still uses independent portrait; Hex Residual uses aggregated markers.
            Assert.IsTrue(ArmyWorldMapPresentation.ShouldDrawIndependentCharacterPortrait(world, solo));
        }

        [Test]
        public void DOWNED_VIS_05_DeadCorpseStillDistinctFromDowned()
        {
            var world = CreateGraphWorld();
            var id = SpawnCharacter(world, "Dead", TestNodeA);
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
            var solo = SpawnCharacter(world, "Solo", TestNodeA);
            EnterIncapacitated(world, solo);
            var anchorHex = Ch01HexPrototypeMapBuilder.HuangcunHex;
            var snap = new BattleParticipantSnapshot
            {
                BattleAnchorHexQ = anchorHex.Q,
                BattleAnchorHexR = anchorHex.R,
                BattleAnchorNodeId = TestNodeA
            };
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

        static EntityId SpawnEnemyNpc(SimulationWorld world, string name, string nodeId)
        {
            var created = world.Entities.CreateNpc(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            var entity = created.Value;
            entity.Get<FactionMembershipComponent>().Assign("enemy:faction", FactionRoleKind.Member);
            world.WorldPresence.SetAtNode(entity.Id, nodeId);
            return entity.Id;
        }

        [Test]
        public void DOWNED_VIS_07_EnemyAndFriendlyShareLingeringSemantics()
        {
            var world = CreateGraphWorld();
            var friendly = SpawnCharacter(world, "Friend", TestNodeA);
            var enemy = SpawnEnemyNpc(world, "Bandit", TestNodeA);
            EnterIncapacitated(world, friendly);
            EnterIncapacitated(world, enemy);

            Assert.IsTrue(LingeringBattlefieldPartyService.IsLingeringDowned(world, friendly));
            Assert.IsTrue(LingeringBattlefieldPartyService.IsLingeringDowned(world, enemy));
            Assert.IsTrue(LingeringBattlefieldPartyService.IsFriendlyLingeringDowned(world, friendly));
            Assert.IsFalse(LingeringBattlefieldPartyService.IsFriendlyLingeringDowned(world, enemy));
            Assert.IsTrue(ArmyWorldMapPresentation.ShouldDrawIndependentCharacterPortrait(world, friendly));
        }
    }
}
