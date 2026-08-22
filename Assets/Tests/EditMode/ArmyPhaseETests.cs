using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
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
            world.WorldGraph.RegisterNode(new WorldNodeState
            {
                Id = NodeA,
                Name = "A",
                OwnerId = FactionA,
                WorldX = 0f,
                WorldY = 0f
            });
            WarGateService.DeclareWar(world, FactionA, FactionB);
            return world;
        }

        static EntityId Spawn(SimulationWorld world, string name, string factionId, string nodeId)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            created.Value.Get<FactionMembershipComponent>().Assign(factionId, FactionRoleKind.Member);
            world.WorldPresence.SetAtNode(created.Value.Id, nodeId);
            return created.Value.Id;
        }

        [Test]
        public void BattleOffer_BuildsFromArmyVsArmy()
        {
            var world = CreateWorld();
            var a = Spawn(world, "A", FactionA, NodeA);
            var b = Spawn(world, "B", FactionA, NodeA);
            var army = ArmyService.CreateArmy(world, FactionA, NodeA, new[] { a, b }).Value;

            TestStrategicBootstrap.EnsureBanditCharacters(world, NodeA);
            ArmyStackAdapter.EnsureBanditPatrolArmy(world, NodeA, string.Empty, string.Empty, -1f);
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
            TestStrategicBootstrap.EnsureBanditCharacters(world, NodeA);
            ArmyStackAdapter.EnsureBanditPatrolArmy(world, NodeA, string.Empty, string.Empty, -1f);
            Assert.IsTrue(world.Strategic.Armies.TryGet(ArmyStackAdapter.BanditPatrolStackId, out var enemy));

            StrategicPursuitService.BeginPursuitArmy(world, army.ArmyId, enemy);
            var pursue = ArmyStackAdapter.CollectLivingMemberIds(world, army);
            Assert.IsTrue(ArmyTravelCommandService.MoveArmyToStackAnchor(world, army.ArmyId, enemy).IsSuccess);
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
            TestStrategicBootstrap.EnsureBanditCharacters(world, NodeA);
            ArmyStackAdapter.EnsureBanditPatrolArmy(world, NodeA, string.Empty, string.Empty, -1f);
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
