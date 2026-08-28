using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Data.Serialization;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    public sealed class FormalArmyOrderSnapshotTests
    {
        const string FactionPlayer = StrategicFactionCatalog.PlayerFactionId;
        const string FactionBandit = StrategicFactionCatalog.BanditId;
        const string NodeHuangcun = "base:site_huangcun";
        const string NodeQingyunLu = "base:site_qingyun_lu";

        static readonly HexCoord HexA = Ch01HexPrototypeMapBuilder.HuangcunHex;
        static readonly HexCoord HexB = Ch01HexPrototypeMapBuilder.QingyunLuHex;

        static SimulationWorld CreateWorld()
        {
            var world = new SimulationWorld();
            Ch01HexPrototypeMapBuilder.Build(world);
            world.Strategic.PlayerFactionId = FactionPlayer;
            WarGateService.DeclareWar(world, FactionPlayer, FactionBandit);
            return world;
        }

        static EntityId SpawnCharacter(
            SimulationWorld world,
            string name,
            string factionId,
            string nodeId)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            var entity = created.Value;
            entity.Get<FactionMembershipComponent>().Assign(factionId, FactionRoleKind.Member);
            var siteId = string.Equals(nodeId, NodeHuangcun, System.StringComparison.Ordinal)
                ? Ch01HexPrototypeMapBuilder.SiteHuangcun
                : Ch01HexPrototypeMapBuilder.SiteQingyunLu;
            world.WorldPresence.SetAtSite(entity.Id, siteId);
            return entity.Id;
        }

        static (FormalArmy player, FormalArmy enemy) CreateOpposingArmies(SimulationWorld world)
        {
            var playerLeader = SpawnCharacter(world, "PlayerLeader", FactionPlayer, NodeHuangcun);
            var playerCreated = ArmyService.CreateArmy(
                world,
                FactionPlayer,
                NodeHuangcun,
                new[] { playerLeader });
            Assert.IsTrue(playerCreated.IsSuccess);
            var player = playerCreated.Value;
            ArmyHexTravelService.InitializeArmyAtHex(player, HexA);

            var enemyLeader = SpawnCharacter(world, "EnemyLeader", FactionBandit, NodeQingyunLu);
            var enemyCreated = ArmyService.CreateArmy(
                world,
                FactionBandit,
                NodeQingyunLu,
                new[] { enemyLeader });
            Assert.IsTrue(enemyCreated.IsSuccess);
            var enemy = enemyCreated.Value;
            ArmyHexTravelService.InitializeArmyAtHex(enemy, HexB);

            var stack = new ArmyStack
            {
                Id = "army:stack_test_enemy_for_attack_snap",
                FormalArmyId = enemy.ArmyId,
                FactionId = FactionBandit,
                DisplayName = "试炼敌军",
                SiteId = NodeQingyunLu,
            };
            world.Strategic.Armies.Register(stack);
            ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, stack);
            return (player, enemy);
        }

        [Test]
        public void ORDER_SNAP_01_AttackFormalArmy_SurvivesJsonRoundtripMidTravel()
        {
            var world = CreateWorld();
            var (player, enemy) = CreateOpposingArmies(world);
            Assert.IsTrue(ArmyHexCommandService.AttackArmy(world, player.ArmyId, enemy.ArmyId).IsSuccess);
            Assert.AreEqual(FormalArmyOrderKind.AttackFormalArmy, player.WorldMotion.CurrentOrderKind);
            Assert.AreEqual(enemy.ArmyId, player.WorldMotion.OrderTargetArmyId);
            Assert.AreEqual(FormalArmyState.Moving, player.State);

            ArmyHexTravelService.AdvanceAll(world, 2);
            Assert.AreEqual(FormalArmyOrderKind.AttackFormalArmy, player.WorldMotion.CurrentOrderKind);

            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, new SimulationLoop(world));
            Assert.IsTrue(json.IsSuccess);
            StringAssert.Contains("\"orderTargetArmyId\":\"" + enemy.ArmyId + "\"", json.Value);
            StringAssert.Contains("\"currentOrderKind\":3", json.Value);

            var restored = service.RestoreJson(json.Value);
            Assert.IsTrue(restored.IsSuccess);
            var world2 = restored.Value.world;
            StrategicSnapshotHelper.FinalizeRuntimeLinks(world2);

            Assert.IsTrue(world2.Strategic.FormalArmies.TryGet(player.ArmyId, out var playerAfter));
            Assert.AreEqual(FormalArmyOrderKind.AttackFormalArmy, playerAfter.WorldMotion.CurrentOrderKind);
            Assert.AreEqual(enemy.ArmyId, playerAfter.WorldMotion.OrderTargetArmyId);
            Assert.IsTrue(playerAfter.State == FormalArmyState.Moving || playerAfter.WorldMotion.IsMoving);
            Assert.AreEqual(enemy.ArmyId, world2.Strategic.Encounter.PursueDefenderArmyId);
        }

        [Test]
        public void ORDER_SNAP_02_TravelOrder_DoesNotKeepAttackTarget()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "TravelLeader", FactionPlayer, NodeHuangcun);
            var created = ArmyService.CreateArmy(world, FactionPlayer, NodeHuangcun, new[] { leader });
            Assert.IsTrue(created.IsSuccess);
            var army = created.Value;
            ArmyHexTravelService.InitializeArmyAtHex(army, HexA);
            Assert.IsTrue(ArmyHexCommandService.MoveArmy(world, army.ArmyId, HexB).IsSuccess);
            Assert.AreEqual(FormalArmyOrderKind.TravelToHex, army.WorldMotion.CurrentOrderKind);
            Assert.IsTrue(string.IsNullOrEmpty(army.WorldMotion.OrderTargetArmyId));

            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, new SimulationLoop(world));
            Assert.IsTrue(json.IsSuccess);

            var restored = service.RestoreJson(json.Value);
            Assert.IsTrue(restored.IsSuccess);
            StrategicSnapshotHelper.FinalizeRuntimeLinks(restored.Value.world);
            Assert.IsTrue(restored.Value.world.Strategic.FormalArmies.TryGet(army.ArmyId, out var after));
            Assert.AreEqual(FormalArmyOrderKind.TravelToHex, after.WorldMotion.CurrentOrderKind);
            Assert.IsTrue(string.IsNullOrEmpty(after.WorldMotion.OrderTargetArmyId));
        }

        [Test]
        public void ORDER_SNAP_03_AttackTargetMissing_FallsBackToIdle()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "Solo", FactionPlayer, NodeHuangcun);
            var created = ArmyService.CreateArmy(world, FactionPlayer, NodeHuangcun, new[] { leader });
            Assert.IsTrue(created.IsSuccess);
            var army = created.Value;
            ArmyHexTravelService.InitializeArmyAtHex(army, HexA);
            army.WorldMotion.SetAttackOrder("army:missing_enemy");

            StrategicSnapshotHelper.FinalizeRuntimeLinks(world);

            Assert.AreEqual(FormalArmyOrderKind.None, army.WorldMotion.CurrentOrderKind);
            Assert.IsTrue(string.IsNullOrEmpty(army.WorldMotion.OrderTargetArmyId));
            Assert.AreEqual(FormalArmyState.Idle, army.State);
        }
    }

    public sealed class HostWorldMapSelectionAuthorityTests
    {
        [Test]
        public void SEL_01_SingleClickFormalArmySelection_IsAuthoritative()
        {
            var authority = new HostWorldMapSelectionAuthority();
            authority.SelectPlayerParty();
            authority.SelectFormalArmy("army:test_player_1");

            Assert.AreEqual(HostWorldMapSelectionKind.FormalArmy, authority.Kind);
            Assert.AreEqual("army:test_player_1", authority.FormalArmyId);
            Assert.IsTrue(authority.IsFormalArmySelected("army:test_player_1"));
        }

        [Test]
        public void SEL_02_PlayerPartySelection_ClearsFormalArmyVisualPredicate()
        {
            var authority = new HostWorldMapSelectionAuthority();
            authority.SelectFormalArmy("army:test_player_1");
            authority.SelectPlayerParty();

            Assert.AreEqual(HostWorldMapSelectionKind.PlayerParty, authority.Kind);
            Assert.IsFalse(authority.IsFormalArmySelected("army:test_player_1"));
        }
    }
}
