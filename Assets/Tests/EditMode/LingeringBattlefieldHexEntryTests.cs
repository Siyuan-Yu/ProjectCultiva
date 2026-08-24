using System.Collections.Generic;
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
    /// <summary>LINGER-01..11：Hex 残留战场再进�?/summary>
    public sealed class LingeringBattlefieldHexEntryTests
    {
        const string PlayerFaction = StrategicFactionCatalog.PlayerFactionId;
        const string NodeA = "base:site_huangcun";
        static readonly HexCoord BattleHex = Ch01HexPrototypeMapBuilder.HuangcunHex;
        static readonly HexCoord RemoteHex = Ch01HexPrototypeMapBuilder.QingyunLuHex;

        static SimulationWorld CreateWorld()
        {
            var world = new SimulationWorld();
            world.Strategic.PlayerFactionId = PlayerFaction;
            HexTestWorldBootstrap.EnsureMinimalHexMap(world);
            WarGateService.DeclareWar(world, PlayerFaction, StrategicFactionCatalog.BanditId);
            return world;
        }

        static EntityId SpawnFriendly(SimulationWorld world, string name)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            created.Value.Get<FactionMembershipComponent>().Assign(PlayerFaction, FactionRoleKind.Member);
            world.WorldPresence.SetAtSite(created.Value.Id, NodeA);
            return created.Value.Id;
        }

        static FormalArmy SpawnPlayerArmy(SimulationWorld world, HexCoord hex)
        {
            var leader = SpawnFriendly(world, "Leader");
            var created = ArmyService.CreateArmy(
                world,
                PlayerFaction,
                NodeA,
                new[] { leader });
            Assert.IsTrue(created.IsSuccess);
            ArmyHexTravelService.InitializeArmyAtHex(created.Value, hex);
            return created.Value;
        }

        static void EnterIncapacitated(SimulationWorld world, EntityId id)
        {
            Assert.IsTrue(world.Entities.TryGet(id, out var entity));
            CombatDamageRules.EnsureVitals(entity);
            if (entity.TryGet<CombatVitalsComponent>(out var vitals))
                vitals.CurrentHp = 0;
            Assert.IsTrue(CombatLifeStateService.TryEnterIncapacitated(world, entity));
        }

        static (ArmyStack stack, List<EntityId> members) SeedEnemyRemnant(
            SimulationWorld world,
            HexCoord hex)
        {
            var result = ArmyStackAdapter.EnsureBanditPatrolArmy(world, NodeA);
            Assert.IsTrue(result.IsSuccess);
            var army = result.Value;
            ArmyHexTravelService.InitializeArmyAtHex(army, hex);
            Assert.IsTrue(world.Strategic.Armies.TryGet(ArmyStackAdapter.BanditPatrolStackId, out var stack));

            var members = new List<EntityId>(army.MemberCharacterIds.Count);
            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var id = new EntityId(army.MemberCharacterIds[i]);
                members.Add(id);
                EnterIncapacitated(world, id);
                StrategicResidualPresenceService.PlaceCharacterAtResidualHex(world, id, hex);
            }

            stack.IsBattlefieldRemnant = true;
            stack.IncapacitatedMemberCount = members.Count;
            stack.MemberCount = members.Count;
            stack.CorpseMemberCount = 0;

            world.Strategic.Encounter.BattlefieldLingering = true;
            world.Strategic.Encounter.ArmyStackId = stack.Id;
            world.Strategic.Participants.PrimaryEnemyStackId = stack.Id;
            ArmyHexBattleAnchorService.SetBattleAnchorHex(world.Strategic.Participants, hex);

            return (stack, members);
        }

        [Test]
        public void LINGER_01_BattlefieldLingering_FoundByBattleAnchorHex()
        {
            var world = CreateWorld();
            SeedEnemyRemnant(world, BattleHex);
            Assert.IsTrue(LingeringBattlefieldQueryService.TryGetLingeringBattlefieldAtHex(
                world, BattleHex, out var ctx));
            Assert.IsNotNull(ctx);
            Assert.AreEqual(ArmyStackAdapter.BanditPatrolStackId, ctx.EnemyStackId);
        }

        [Test]
        public void LINGER_02_DestroyedBattlefield_NotFound()
        {
            var world = CreateWorld();
            SeedEnemyRemnant(world, BattleHex);
            world.Strategic.Encounter.BattlefieldLingering = false;
            world.Strategic.Encounter.ClearTrackedIds();
            Assert.IsFalse(LingeringBattlefieldQueryService.TryGetLingeringBattlefieldAtHex(
                world, BattleHex, out _));
        }

        [Test]
        public void LINGER_03_SelfResidualHex_CanEnterFriendly()
        {
            var world = CreateWorld();
            var (_, _) = SeedEnemyRemnant(world, BattleHex);
            var friendly = SpawnFriendly(world, "DownedFriend");
            EnterIncapacitated(world, friendly);
            StrategicResidualPresenceService.PlaceCharacterAtResidualHex(world, friendly, BattleHex);

            var ctx = HexResidualContextQuery.Build(world, BattleHex);
            Assert.IsTrue(ctx.CanEnterFriendlyLingering);
            Assert.IsTrue(BattleOfferService.TryEnterFriendlyLingeringAtHex(
                world, BattleHex, new[] { friendly }));
        }

        [Test]
        public void LINGER_04_EnemyResidual_WithoutArmy_CannotAttackEnter()
        {
            var world = CreateWorld();
            SeedEnemyRemnant(world, BattleHex);
            var ctx = HexResidualContextQuery.Build(world, BattleHex);
            Assert.IsTrue(ctx.CanAttackEnemyLingering);
            Assert.IsFalse(BattleOfferService.TryAttackEnemyLingeringAtHex(
                world, string.Empty, BattleHex, out _));
        }

        [Test]
        public void LINGER_05_EnemyResidual_WithArmy_BuildsOfferAtSameHex()
        {
            var world = CreateWorld();
            SeedEnemyRemnant(world, BattleHex);
            var playerArmy = SpawnPlayerArmy(world, BattleHex);
            Assert.IsTrue(BattleOfferService.TryAttackEnemyLingeringAtHex(
                world, playerArmy.ArmyId, BattleHex, out var hint));
            Assert.IsTrue(world.Strategic.HasBattleOffer, hint);
        }

        [Test]
        public void LINGER_06_RemoteArmy_AttackLingering_StartsMoveNotTeleport()
        {
            var world = CreateWorld();
            SeedEnemyRemnant(world, BattleHex);
            var playerArmy = SpawnPlayerArmy(world, RemoteHex);
            var startHex = playerArmy.CurrentHex;
            Assert.IsTrue(BattleOfferService.TryAttackEnemyLingeringAtHex(
                world, playerArmy.ArmyId, BattleHex, out _));
            Assert.AreEqual(startHex, playerArmy.CurrentHex);
            Assert.AreEqual(FormalArmyState.Moving, playerArmy.State);
            Assert.IsTrue(world.Strategic.Encounter.HasPendingLingeringAttack);
        }

        [Test]
        public void LINGER_07_ArrivalAtHex_OpensRemnantOffer()
        {
            var world = CreateWorld();
            SeedEnemyRemnant(world, BattleHex);
            var playerArmy = SpawnPlayerArmy(world, RemoteHex);
            BattleOfferService.TryAttackEnemyLingeringAtHex(world, playerArmy.ArmyId, BattleHex, out _);
            while (playerArmy.State == FormalArmyState.Moving)
                ArmyHexTravelService.AdvanceHexTravel(world, playerArmy, 1);
            Assert.AreEqual(BattleHex, playerArmy.CurrentHex);
            ArmyHexLingeringArrivalService.AfterTravelTick(world);
            Assert.IsTrue(world.Strategic.HasBattleOffer);
        }

        [Test]
        public void LINGER_08_TargetLostBeforeArrival_CancelsPending()
        {
            var world = CreateWorld();
            SeedEnemyRemnant(world, BattleHex);
            var playerArmy = SpawnPlayerArmy(world, RemoteHex);
            BattleOfferService.TryAttackEnemyLingeringAtHex(world, playerArmy.ArmyId, BattleHex, out _);
            world.Strategic.Encounter.BattlefieldLingering = false;
            world.Strategic.Encounter.ClearPendingLingeringAttack();
            while (playerArmy.State == FormalArmyState.Moving)
                ArmyHexTravelService.AdvanceHexTravel(world, playerArmy, 1);
            ArmyHexLingeringArrivalService.AfterTravelTick(world);
            Assert.IsFalse(world.Strategic.HasBattleOffer);
            Assert.IsFalse(world.Strategic.Encounter.HasPendingLingeringAttack);
        }
    }
}
