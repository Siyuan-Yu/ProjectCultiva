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
    /// <summary>RIGHTCLICK-01..08 + HEX-CONTEXT 回归：Hex RTS 右键决策�?/summary>
    public sealed class HexRightClickResolverTests
    {
        const string PlayerFaction = StrategicFactionCatalog.PlayerFactionId;
        const string NodeA = "base:site_huangcun";
        const string SiteA = Ch01HexPrototypeMapBuilder.SiteHuangcun;
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

        static SimulationWorld CreateWorldAtPeace()
        {
            var world = new SimulationWorld();
            world.Strategic.PlayerFactionId = PlayerFaction;
            HexTestWorldBootstrap.EnsureMinimalHexMap(world);
            return world;
        }

        static EntityId SpawnFriendly(SimulationWorld world, string name)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            created.Value.Get<FactionMembershipComponent>().Assign(PlayerFaction, FactionRoleKind.Member);
            world.WorldPresence.SetAtSite(created.Value.Id, SiteA);
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

        static FormalArmy SpawnActiveEnemyArmy(SimulationWorld world, HexCoord hex)
        {
            var result = TestArmyFixtures.EnsureBanditPatrolArmy(world, NodeA);
            Assert.IsTrue(result.IsSuccess);
            ArmyHexTravelService.InitializeArmyAtHex(result.Value, hex);
            return result.Value;
        }

        static void EnterIncapacitated(SimulationWorld world, EntityId id)
        {
            Assert.IsTrue(world.Entities.TryGet(id, out var entity));
            CombatDamageRules.EnsureVitals(entity);
            if (entity.TryGet<CombatVitalsComponent>(out var vitals))
                vitals.CurrentHp = 0;
            Assert.IsTrue(CombatLifeStateService.TryEnterIncapacitated(world, entity));
        }

        static void SeedEnemyRemnantOnly(SimulationWorld world, HexCoord hex)
        {
            var result = TestArmyFixtures.EnsureBanditPatrolArmy(world, NodeA);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(world.Strategic.Armies.TryGet(ArmyStackAdapter.BanditPatrolStackId, out var stack));

            for (var i = 0; i < result.Value.MemberCharacterIds.Count; i++)
            {
                var id = new EntityId(result.Value.MemberCharacterIds[i]);
                EnterIncapacitated(world, id);
                StrategicResidualPresenceService.PlaceCharacterAtResidualHex(world, id, hex);
            }

            stack.IsBattlefieldRemnant = true;
            stack.IncapacitatedMemberCount = result.Value.MemberCharacterIds.Count;
            stack.MemberCount = result.Value.MemberCharacterIds.Count;

            world.Strategic.Encounter.BattlefieldLingering = true;
            world.Strategic.Encounter.ArmyStackId = stack.Id;
            world.Strategic.Participants.PrimaryEnemyStackId = stack.Id;
            ArmyHexBattleAnchorService.SetBattleAnchorHex(world.Strategic.Participants, hex);
        }

        static void SeedEnemyLingeringWithLivingArmy(SimulationWorld world, HexCoord hex)
        {
            var army = SpawnActiveEnemyArmy(world, hex);
            Assert.IsTrue(world.Strategic.Armies.TryGet(ArmyStackAdapter.BanditPatrolStackId, out var stack));

            var downedIndex = army.MemberCharacterIds.Count - 1;
            var downed = new EntityId(army.MemberCharacterIds[downedIndex]);
            EnterIncapacitated(world, downed);
            StrategicResidualPresenceService.PlaceCharacterAtResidualHex(world, downed, hex);

            stack.IsBattlefieldRemnant = true;
            stack.IncapacitatedMemberCount = 1;

            world.Strategic.Encounter.BattlefieldLingering = true;
            world.Strategic.Encounter.ArmyStackId = stack.Id;
            world.Strategic.Participants.PrimaryEnemyStackId = stack.Id;
            ArmyHexBattleAnchorService.SetBattleAnchorHex(world.Strategic.Participants, hex);
        }

        static void SeedSelfLingeringAtHex(SimulationWorld world, HexCoord hex)
        {
            var friendly = SpawnFriendly(world, "DownedFriend");
            EnterIncapacitated(world, friendly);
            StrategicResidualPresenceService.PlaceCharacterAtResidualHex(world, friendly, hex);

            world.Strategic.Encounter.BattlefieldLingering = true;
            ArmyHexBattleAnchorService.SetBattleAnchorHex(world.Strategic.Participants, hex);
        }

        static HexRightClickResolution Resolve(
            SimulationWorld world,
            HexCoord hex,
            bool hasSelectedArmy = true,
            bool hasMovableArmy = true)
        {
            return HexRightClickResolver.Resolve(
                world,
                hex,
                PlayerFaction,
                hasSelectedArmy,
                hasMovableArmy,
                true);
        }

        static void AssertMenuHasNoMove(HexRightClickResolution resolution)
        {
            Assert.IsFalse(resolution.MenuActions.Contains(HexStrategicContextActionKind.MoveToHex));
        }

        [Test]
        public void RIGHTCLICK_01_EmptyHex_DirectMove()
        {
            var world = CreateWorld();
            SpawnPlayerArmy(world, RemoteHex);

            var resolution = Resolve(world, RemoteHex);
            Assert.AreEqual(HexRightClickResolvedAction.DirectMove, resolution.Action);
            AssertMenuHasNoMove(resolution);
        }

        [Test]
        public void RIGHTCLICK_02_ActiveEnemy_DirectAttackArmy()
        {
            var world = CreateWorld();
            SpawnPlayerArmy(world, RemoteHex);
            SpawnActiveEnemyArmy(world, BattleHex);

            var resolution = Resolve(world, BattleHex);
            Assert.AreEqual(HexRightClickResolvedAction.ShowAttackTargetMenu, resolution.Action);
            Assert.Contains(HexStrategicContextActionKind.AttackArmy, resolution.MenuActions);
            Assert.AreEqual(
                ArmyStackAdapter.BanditPatrolFormalArmyId,
                resolution.Context.PrimaryActiveEnemyArmy.FormalArmyId);
        }

        [Test]
        public void RIGHTCLICK_03_EnemyResidual_AttackLingeringMenu()
        {
            var world = CreateWorld();
            SpawnPlayerArmy(world, RemoteHex);
            SeedEnemyRemnantOnly(world, BattleHex);

            var resolution = Resolve(world, BattleHex);
            Assert.AreEqual(HexRightClickResolvedAction.ShowAttackTargetMenu, resolution.Action);
            Assert.Contains(HexStrategicContextActionKind.AttackLingeringBattlefield, resolution.MenuActions);
            Assert.IsFalse(resolution.MenuActions.Contains(HexStrategicContextActionKind.MoveToHex));
            Assert.IsFalse(resolution.Context.HasActiveEnemyArmy);
            Assert.IsTrue(HexRightClickResolver.TryGetAttackableEnemyLingeringBattlefieldAtHex(
                world, BattleHex, out _));
        }

        [Test]
        public void RIGHTCLICK_04_SelfResidual_DirectEnterLingering()
        {
            var world = CreateWorld();
            SeedSelfLingeringAtHex(world, BattleHex);

            var resolution = Resolve(world, BattleHex, hasSelectedArmy: false, hasMovableArmy: false);
            Assert.AreEqual(HexRightClickResolvedAction.DirectEnterFriendlyLingering, resolution.Action);
            Assert.IsTrue(HexRightClickResolver.TryGetFriendlyLingeringBattlefieldAtHex(
                world, BattleHex, out _));
        }

        [Test]
        public void RIGHTCLICK_05_ActiveEnemyAndResidual_ShowAttackTargetMenu()
        {
            var world = CreateWorld();
            SpawnPlayerArmy(world, RemoteHex);
            SeedEnemyLingeringWithLivingArmy(world, BattleHex);

            var resolution = Resolve(world, BattleHex);
            Assert.AreEqual(HexRightClickResolvedAction.ShowAttackTargetMenu, resolution.Action);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    HexStrategicContextActionKind.AttackArmy,
                    HexStrategicContextActionKind.AttackLingeringBattlefield
                },
                resolution.MenuActions);
            AssertMenuHasNoMove(resolution);
        }

        [Test]
        public void RIGHTCLICK_06_MovingEnemyAttack_BindsEnemyArmyId()
        {
            var world = CreateWorld();
            var player = SpawnPlayerArmy(world, RemoteHex);
            var enemy = SpawnActiveEnemyArmy(world, BattleHex);

            var result = ArmyHexCommandService.AttackArmy(world, player.ArmyId, enemy.ArmyId);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(enemy.ArmyId, world.Strategic.Encounter.PursueDefenderArmyId);
            Assert.IsFalse(world.Strategic.Encounter.HasPendingLingeringAttack);
        }

        [Test]
        public void RIGHTCLICK_07_MarkerPenetratesToHex_AttackLingering()
        {
            var world = CreateWorld();
            SpawnPlayerArmy(world, RemoteHex);
            SeedEnemyRemnantOnly(world, BattleHex);

            var resolution = Resolve(world, BattleHex);
            Assert.AreEqual(HexRightClickResolvedAction.ShowAttackTargetMenu, resolution.Action);
        }

        [Test]
        public void RIGHTCLICK_08_NoSelectedArmy_NoMoveOnEnemyResidual()
        {
            var world = CreateWorld();
            SeedEnemyRemnantOnly(world, BattleHex);

            var resolution = Resolve(world, BattleHex, hasSelectedArmy: false, hasMovableArmy: false);
            Assert.AreEqual(HexRightClickResolvedAction.None, resolution.Action);
            Assert.AreEqual("请先左键选中我方军团", resolution.StatusHint);
        }

        [Test]
        public void HEX_CONTEXT_ATTACK_07_PeaceBlocksAttackArmy()
        {
            var world = CreateWorldAtPeace();
            SpawnActiveEnemyArmy(world, BattleHex);

            var ctx = HexResidualContextQuery.Build(world, BattleHex, PlayerFaction);
            Assert.IsTrue(ctx.HasActiveEnemyArmy);
            Assert.IsFalse(ctx.CanAttackActiveEnemyArmy);
        }

        [Test]
        public void RIGHTCLICK_ActiveEnemyAndSelfLingering_ShowBothTargets()
        {
            var world = CreateWorld();
            SpawnPlayerArmy(world, RemoteHex);
            SpawnActiveEnemyArmy(world, BattleHex);
            SeedSelfLingeringAtHex(world, BattleHex);

            var resolution = Resolve(world, BattleHex);
            Assert.AreEqual(HexRightClickResolvedAction.ShowAttackTargetMenu, resolution.Action);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    HexStrategicContextActionKind.AttackArmy,
                    HexStrategicContextActionKind.EnterLingeringBattlefield
                },
                resolution.MenuActions);
            AssertMenuHasNoMove(resolution);
        }
    }
}
