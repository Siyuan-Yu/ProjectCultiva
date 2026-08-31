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
    /// <summary>LINGER-ATTACK-01..06：敌方残�?Hex 攻击残留战场回归�?/summary>
    public sealed class LingeringAttackRightClickTests
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

        static void ConfirmDeath(SimulationWorld world, EntityId id)
        {
            Assert.IsTrue(world.Entities.TryGet(id, out var entity));
            Assert.IsTrue(CombatLifeStateService.TryConfirmDeath(world, EntityId.None, entity, out _));
        }

        /// <summary>
        /// 模拟 Auto Battle 结束 �?ResolveAndEnd �?FinishOfferResolution 后的真实状态：
        /// Character Residual �?Hex，BattlefieldLingering=true，锚点在 Encounter Runtime�?
        /// </summary>
        static void SeedAutoBattleEnemyDownedThenFinish(
            SimulationWorld world,
            HexCoord hex,
            bool executeOnWin)
        {
            var result = TestArmyFixtures.EnsureBanditPatrolArmy(world, NodeA);
            Assert.IsTrue(result.IsSuccess);
            ArmyHexTravelService.InitializeArmyAtHex(result.Value, hex);
            Assert.IsTrue(world.Strategic.Armies.TryGet(ArmyStackAdapter.BanditPatrolStackId, out var stack));

            var player = SpawnFriendly(world, "AutoLeader");
            var report = AutoBattleCasualtyService.ApplyPlayerVictory(
                world,
                new[] { player },
                stack,
                playerPower: 40,
                enemyPower: 5,
                executeOnWin: executeOnWin);
            Assert.IsNotNull(report);

            var snap = world.Strategic.Participants;
            snap.PrimaryEnemyStackId = ArmyStackAdapter.BanditPatrolStackId;
            ArmyHexBattleAnchorService.SetBattleAnchorHex(snap, hex);
            world.Strategic.Encounter.ArmyStackId = ArmyStackAdapter.BanditPatrolStackId;

            StrategicEncounterSpawner.EnsureMacroRemnantSpawns(world, snap);
            ArmyPostBattleSyncService.SyncEnemyArmyAfterBattle(world, snap);

            Assert.IsTrue(StrategicEncounterResolveService.HasLingeringBattlefieldRemnants(world));
            Assert.IsTrue(StrategicEncounterResolveService.ResolveAndEnd(world).IsSuccess);

            Assert.IsTrue(world.Strategic.Encounter.BattlefieldLingering);
            Assert.IsTrue(StrategicEncounterResolveService.TryGetLingeringBattleAnchorHex(
                world, out var anchor));
            Assert.AreEqual(hex, anchor);
        }

        static HexRightClickResolution Resolve(
            SimulationWorld world,
            HexCoord hex,
            bool hasSelectedArmy = true)
        {
            return HexRightClickResolver.Resolve(
                world,
                hex,
                PlayerFaction,
                hasSelectedArmy,
                hasSelectedArmy,
                true);
        }

        [Test]
        public void LINGER_ATTACK_01_EnemyDowned_AfterFinish_AttackLingering()
        {
            var world = CreateWorld();
            SpawnPlayerArmy(world, RemoteHex);
            SeedAutoBattleEnemyDownedThenFinish(world, BattleHex, executeOnWin: false);

            var counts = StrategicResidualPresentationQuery.CountAtHex(world, BattleHex);
            Assert.Greater(counts.EnemyDowned, 0);

            Assert.IsTrue(LingeringBattlefieldQueryService.TryGetLingeringBattlefieldAtHex(
                world, BattleHex, out var linger));
            Assert.IsTrue(linger.HasAttackableEnemyLingering);

            var resolution = Resolve(world, BattleHex);
            Assert.AreNotEqual(HexRightClickResolvedAction.DirectMove, resolution.Action);
            Assert.Contains(
                HexStrategicContextActionKind.AttackLingeringBattlefield,
                resolution.MenuActions);
            Assert.IsFalse(resolution.MenuActions.Contains(HexStrategicContextActionKind.MoveToHex));
        }

        [Test]
        public void LINGER_ATTACK_02_EnemyDead_AfterFinish_AttackLingering()
        {
            var world = CreateWorld();
            SpawnPlayerArmy(world, RemoteHex);
            SeedAutoBattleEnemyDownedThenFinish(world, BattleHex, executeOnWin: true);

            var counts = StrategicResidualPresentationQuery.CountAtHex(world, BattleHex);
            Assert.Greater(counts.EnemyDead + counts.EnemyDowned, 0);

            var resolution = Resolve(world, BattleHex);
            Assert.AreNotEqual(HexRightClickResolvedAction.DirectMove, resolution.Action);
            Assert.Contains(
                HexStrategicContextActionKind.AttackLingeringBattlefield,
                resolution.MenuActions);
        }

        [Test]
        public void LINGER_ATTACK_03_DownedAndDead_SameHex_OneAttackLingering()
        {
            var world = CreateWorld();
            SpawnPlayerArmy(world, RemoteHex);
            SeedAutoBattleEnemyDownedThenFinish(world, BattleHex, executeOnWin: false);

            var groups = StrategicResidualPresentationQuery.Query(world);
            EntityId corpseId = default;
            for (var i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                if (g.Relation != StrategicRelationBucket.Enemy || !g.Hex.Equals(BattleHex))
                    continue;
                if (g.Characters.Count == 0)
                    continue;
                corpseId = g.Characters[0].CharacterId;
                break;
            }

            if (!corpseId.IsNone)
                ConfirmDeath(world, corpseId);

            var resolution = Resolve(world, BattleHex);
            var lingeringActions = 0;
            for (var i = 0; i < resolution.MenuActions.Count; i++)
            {
                if (resolution.MenuActions[i] == HexStrategicContextActionKind.AttackLingeringBattlefield)
                    lingeringActions++;
            }

            Assert.AreEqual(1, lingeringActions);
            Assert.IsFalse(resolution.MenuActions.Contains(HexStrategicContextActionKind.MoveToHex));
        }

        [Test]
        public void LINGER_ATTACK_04_ResidualWithoutBattlefield_DoesNotMove()
        {
            var world = CreateWorld();
            SpawnPlayerArmy(world, RemoteHex);

            var enemy = SpawnActiveEnemyArmy(world, BattleHex);
            for (var i = 0; i < enemy.MemberCharacterIds.Count; i++)
            {
                var id = new EntityId(enemy.MemberCharacterIds[i]);
                EnterIncapacitated(world, id);
                StrategicResidualPresenceService.PlaceCharacterAtResidualHex(world, id, BattleHex);
            }

            ArmyService.DetachNonLivingMembersAtBattlefield(world, enemy);

            // 故意不设 BattlefieldLingering / Anchor
            world.Strategic.Encounter.BattlefieldLingering = false;
            world.Strategic.Encounter.ClearLingeringBattleAnchorHex();
            world.Strategic.Participants.Clear();

            Assert.IsTrue(StrategicResidualPresentationQuery.HasEnemyResidualAtHex(world, BattleHex));
            Assert.IsFalse(LingeringBattlefieldQueryService.TryGetLingeringBattlefieldAtHex(
                world, BattleHex, out _));

            var resolution = Resolve(world, BattleHex);
            Assert.AreEqual(HexRightClickResolvedAction.None, resolution.Action);
            Assert.IsFalse(string.IsNullOrEmpty(resolution.StatusHint));
            Assert.IsFalse(resolution.MenuActions.Contains(HexStrategicContextActionKind.MoveToHex));
        }

        [Test]
        public void LINGER_ATTACK_05_ActiveEnemy_StillAttackArmy()
        {
            var world = CreateWorld();
            SpawnPlayerArmy(world, RemoteHex);
            SpawnActiveEnemyArmy(world, BattleHex);

            var resolution = Resolve(world, BattleHex);
            Assert.Contains(HexStrategicContextActionKind.AttackArmy, resolution.MenuActions);
            Assert.IsFalse(resolution.MenuActions.Contains(HexStrategicContextActionKind.MoveToHex));
        }

        [Test]
        public void LINGER_ATTACK_06_EmptyHex_StillDirectMove()
        {
            var world = CreateWorld();
            SpawnPlayerArmy(world, RemoteHex);

            var resolution = Resolve(world, RemoteHex);
            Assert.AreEqual(HexRightClickResolvedAction.DirectMove, resolution.Action);
        }

        [Test]
        public void LINGER_ATTACK_FinishOffer_PreservesEncounterAnchor()
        {
            var world = CreateWorld();
            SeedAutoBattleEnemyDownedThenFinish(world, BattleHex, executeOnWin: false);

            Assert.IsTrue(world.Strategic.Encounter.BattlefieldLingering);
            Assert.IsTrue(world.Strategic.Encounter.TryGetLingeringBattleAnchorHex(out var hex));
            Assert.AreEqual(BattleHex, hex);
            Assert.IsFalse(ArmyHexBattleAnchorService.HasBattleAnchorHex(world.Strategic.Participants));
        }
    }
}
