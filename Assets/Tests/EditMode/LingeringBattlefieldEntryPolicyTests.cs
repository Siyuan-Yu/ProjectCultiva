using NUnit.Framework;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    public sealed class LingeringBattlefieldEntryPolicyTests
    {
        const string TestFactionA = "test:faction_player";
        const string TestFactionB = "test:faction_enemy";
        const string TestNodeA = "test:node_a";

        static SimulationWorld CreateWorld()
        {
            var world = new SimulationWorld();
            Ch01HexPrototypeMapBuilder.Build(world);
            world.Strategic.PlayerFactionId = TestFactionA;return world;
        }

        static EntityId SpawnFriendly(SimulationWorld world, string name)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            var entity = created.Value;
            entity.Get<FactionMembershipComponent>().Assign(TestFactionA, FactionRoleKind.Member);
            world.WorldPresence.SetAtSite(entity.Id, TestNodeA);
            return entity.Id;
        }

        static EntityId SpawnEnemyNpc(SimulationWorld world, string name)
        {
            var created = world.Entities.CreateNpc(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            var entity = created.Value;
            entity.Get<FactionMembershipComponent>().Assign(TestFactionB, FactionRoleKind.Member);
            world.WorldPresence.SetAtSite(entity.Id, TestNodeA);
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

        static void SetupLingeringEncounter(SimulationWorld world, ArmyStack enemy)
        {
            world.Strategic.Encounter.BattlefieldLingering = true;
            world.Strategic.Encounter.ArmyStackId = enemy.Id;
        }

        [Test]
        public void FriendlyDowned_CanDirectEnterLingeringBattlefield()
        {
            var world = CreateWorld();
            var friendly = SpawnFriendly(world, "Friend");
            EnterIncapacitated(world, friendly);
            var enemy = new ArmyStack
            {
                Id = "enemy:stack",
                FactionId = TestFactionB,
                SiteId = TestNodeA,
                IsBattlefieldRemnant = true,
                IncapacitatedMemberCount = 1
            };
            world.Strategic.Armies.Register(enemy);
            SetupLingeringEncounter(world, enemy);

            Assert.IsTrue(LingeringBattlefieldPartyService.IsFriendlyLingeringDowned(world, friendly));
            Assert.IsTrue(BattleOfferService.TryBuildOfferForLingeringBattlefield(
                world,
                new[] { friendly },
                friendly,
                "残留战场",
                new[] { friendly }));
        }

        [Test]
        public void EnemyDowned_CannotDirectEnterLingeringBattlefield()
        {
            var world = CreateWorld();
            var attacker = SpawnFriendly(world, "Attacker");
            var enemyDowned = SpawnEnemyNpc(world, "Bandit");
            EnterIncapacitated(world, enemyDowned);
            var enemy = new ArmyStack
            {
                Id = "enemy:stack",
                FactionId = TestFactionB,
                SiteId = TestNodeA,
                IsBattlefieldRemnant = true,
                IncapacitatedMemberCount = 1
            };
            world.Strategic.Armies.Register(enemy);
            SetupLingeringEncounter(world, enemy);

            Assert.IsTrue(LingeringBattlefieldPartyService.IsLingeringDowned(world, enemyDowned));
            Assert.IsFalse(LingeringBattlefieldPartyService.IsFriendlyLingeringDowned(world, enemyDowned));
            Assert.IsFalse(BattleOfferService.TryBuildOfferForLingeringBattlefield(
                world,
                new[] { attacker },
                enemyDowned,
                "残留战场",
                new[] { attacker }));
        }

        [Test]
        public void RemnantStackAttack_StillOpensNormalBattleOffer()
        {
            var world = CreateWorld();
            var attacker = SpawnFriendly(world, "Attacker");
            var enemy = new ArmyStack
            {
                Id = "enemy:stack",
                FactionId = TestFactionB,
                SiteId = TestNodeA,
                IsBattlefieldRemnant = true,
                IncapacitatedMemberCount = 2,
                MemberCount = 2
            };
            world.Strategic.Armies.Register(enemy);
            SetupLingeringEncounter(world, enemy);
            WarGateService.DeclareWar(world, TestFactionA, TestFactionB);
            world.WorldPresence.SetAtSite(attacker, TestNodeA);

            Assert.IsTrue(BattleOfferService.TryBuildOfferForArmy(
                world,
                new[] { attacker },
                enemy,
                "残留战场"));
            Assert.IsTrue(world.Strategic.HasBattleOffer);
        }
    }
}
