using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    public sealed class ArmyPhaseETests
    {
        const string FactionA = "test:faction_a";
        const string FactionB = "test:faction_b";
        const string NodeA = "test:node_a";

        static SimulationWorld CreateWorld()
        {
            var world = new SimulationWorld();
            Ch01HexPrototypeMapBuilder.Build(world);
            // 测试敌军 assembly site（CreateAuthoredArmy 要求 site 已注册）。
            world.Strategic.Sites.Register(new WorldSite
            {
                SiteId = NodeA,
                DisplayName = "Test Node A",
                SiteType = "Road",
                AnchorHex = new HexCoord(24, 24),
            });
            WarGateService.DeclareWar(world, FactionA, FactionB);
            return world;
        }

        static EntityId Spawn(SimulationWorld world, string name, string factionId, string nodeId)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            created.Value.Get<FactionMembershipComponent>().Assign(factionId, FactionRoleKind.Member);
            world.WorldPresence.SetAtSite(created.Value.Id, nodeId);
            return created.Value.Id;
        }

        static EntityId SpawnBanditNpc(SimulationWorld world, string name)
        {
            var created = world.Entities.CreateNpc(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            created.Value.Get<FactionMembershipComponent>().Assign(FactionB, FactionRoleKind.Member);
            world.WorldPresence.SetAtSite(created.Value.Id, NodeA);
            return created.Value.Id;
        }

        /// <summary>替代已删除的 TestStrategicBootstrap：Content 迁移后由
        /// FormalArmyContentBootstrap 负责；EditMode 测试用等价 authored army。</summary>
        static void EnsureTestBanditPatrolArmy(SimulationWorld world)
        {
            var members = new List<EntityId>
            {
                SpawnBanditNpc(world, "BanditLeader"),
                SpawnBanditNpc(world, "BanditA"),
                SpawnBanditNpc(world, "BanditB"),
                SpawnBanditNpc(world, "BanditC"),
            };
            var created = ArmyService.CreateAuthoredArmy(
                world,
                ArmyStackAdapter.BanditPatrolFormalArmyId,
                FactionB,
                NodeA,
                members,
                members[0]);
            Assert.IsTrue(created.IsSuccess);
            ArmyStackAdapter.EnsureLinkedStackView(
                world, created.Value, ArmyStackAdapter.BanditPatrolStackId, "荒村山匪");
        }

        [Test]
        public void BattleOffer_BuildsFromArmyVsArmy()
        {
            var world = CreateWorld();
            var a = Spawn(world, "A", FactionA, NodeA);
            var b = Spawn(world, "B", FactionA, NodeA);
            var army = ArmyService.CreateArmy(world, FactionA, NodeA, new[] { a, b }).Value;

            EnsureTestBanditPatrolArmy(world);
            Assert.IsTrue(world.Strategic.Armies.TryGet(ArmyStackAdapter.BanditPatrolStackId, out var enemy));

            var party = ArmyStackAdapter.CollectLivingMemberIds(world, army);
            Assert.IsTrue(BattleOfferService.TryBuildOfferForArmyVsArmy(
                world, army.ArmyId, party, enemy, "ArmyVsArmy"));
            Assert.AreEqual(army.ArmyId, world.Strategic.BattleOffer.AttackerArmyId);
            Assert.AreEqual(ArmyStackAdapter.BanditPatrolFormalArmyId, world.Strategic.BattleOffer.DefenderArmyId);
        }

        [Test]
        public void Pursuit_ArmyChase_ArrivesOffer()
        {
            var world = CreateWorld();
            var a = Spawn(world, "A", FactionA, NodeA);
            var army = ArmyService.CreateArmy(world, FactionA, NodeA, new[] { a }).Value;
            EnsureTestBanditPatrolArmy(world);
            Assert.IsTrue(world.Strategic.Armies.TryGet(ArmyStackAdapter.BanditPatrolStackId, out var enemy));

            StrategicPursuitService.BeginPursuitArmy(world, army.ArmyId, enemy);
            var pursue = ArmyStackAdapter.CollectLivingMemberIds(world, army);
            Assert.IsTrue(ArmyHexCommandService.AttackStack(world, army.ArmyId, enemy).IsSuccess);
            StrategicPursuitService.SyncPursuersToStack(world, pursue, enemy);
            StrategicPursuitService.AfterTravelTick(world);
            Assert.IsTrue(world.Strategic.HasBattleOffer || world.Strategic.Encounter.HasPursueParty);
        }

        [Test]
        public void BattleParticipantSnapshot_RecordsMemberIds()
        {
            var world = CreateWorld();
            var a = Spawn(world, "A", FactionA, NodeA);
            var b = Spawn(world, "B", FactionA, NodeA);
            var army = ArmyService.CreateArmy(world, FactionA, NodeA, new[] { a, b }).Value;
            EnsureTestBanditPatrolArmy(world);
            Assert.IsTrue(world.Strategic.Armies.TryGet(ArmyStackAdapter.BanditPatrolStackId, out var enemy));

            var snap = BattleParticipantSnapshotBuilder.BuildArmyVsArmy(
                world, army.ArmyId, enemy, "snap");
            var friendly = 0;
            var enemyMembers = 0;
            for (var i = 0; i < snap.Records.Count; i++)
            {
                if (snap.Records[i].Kind == BattleParticipantKind.MandatoryFriendly)
                    friendly++;
                if (snap.Records[i].Kind == BattleParticipantKind.EnemyPrimary &&
                    !snap.Records[i].EntityId.IsNone)
                    enemyMembers++;
            }

            Assert.AreEqual(2, friendly);
            Assert.AreEqual(4, enemyMembers);
        }
    }
}
