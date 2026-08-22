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
    public sealed class ArmyPhaseBTests
    {
        const string TestFactionA = "test:faction_a";
        const string TestNodeA = "test:node_a";

        static SimulationWorld CreateWorld()
        {
            var world = new SimulationWorld();
            world.WorldGraph.RegisterNode(new WorldNodeState
            {
                Id = TestNodeA,
                Name = "A",
                OwnerId = TestFactionA,
                WorldX = 1f,
                WorldY = 2f
            });
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

        [Test]
        public void ArmyWorldMap_ArmyUsesLeaderPortrait()
        {
            var world = CreateWorld();
            var a = SpawnCharacter(world, "A", TestNodeA);
            var b = SpawnCharacter(world, "B", TestNodeA);
            var army = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { a, b });
            Assert.IsTrue(army.IsSuccess);

            Assert.AreEqual(a, ArmyWorldMapPresentation.ResolvePortraitLeader(army.Value));
            Assert.IsTrue(ArmyWorldMapPresentation.ShouldDrawFormalArmyPortrait(world, army.Value));
        }

        [Test]
        public void ArmyWorldMap_GroupedCharacter_NotShownAsIndependentFormalUnit()
        {
            var world = CreateWorld();
            var a = SpawnCharacter(world, "A", TestNodeA);
            var b = SpawnCharacter(world, "B", TestNodeA);
            var c = SpawnCharacter(world, "C", TestNodeA);
            Assert.IsTrue(ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { a, b }).IsSuccess);

            Assert.IsFalse(ArmyWorldMapPresentation.ShouldDrawIndependentCharacterPortrait(world, a));
            Assert.IsFalse(ArmyWorldMapPresentation.ShouldDrawIndependentCharacterPortrait(world, b));
            Assert.IsFalse(ArmyWorldMapPresentation.ShouldDrawIndependentCharacterPortrait(world, c));
        }

        [Test]
        public void ArmyWorldMap_LeaderChange_UpdatesDerivedPortrait()
        {
            var world = CreateWorld();
            var a = SpawnCharacter(world, "A", TestNodeA);
            var b = SpawnCharacter(world, "B", TestNodeA);
            var created = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { a, b });
            Assert.IsTrue(created.IsSuccess);
            var armyId = created.Value.ArmyId;

            Assert.IsTrue(ArmyService.ChangeLeader(world, armyId, b).IsSuccess);
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(armyId, out var army));
            Assert.AreEqual(b, ArmyWorldMapPresentation.ResolvePortraitLeader(army));
        }

        [Test]
        public void Army_Garrisoned_RemainsVisibleAsArmy()
        {
            var world = CreateWorld();
            var a = SpawnCharacter(world, "A", TestNodeA);
            var created = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { a });
            Assert.IsTrue(created.IsSuccess);
            Assert.IsTrue(ArmyService.GarrisonArmy(world, created.Value.ArmyId).IsSuccess);
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(created.Value.ArmyId, out var army));
            Assert.AreEqual(FormalArmyState.Garrisoned, army.State);
            Assert.IsTrue(ArmyWorldMapPresentation.ShouldDrawFormalArmyPortrait(world, army));
        }

        [Test]
        public void Army_Disband_RemovesArmyPresentation()
        {
            var world = CreateWorld();
            var a = SpawnCharacter(world, "A", TestNodeA);
            var created = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { a });
            Assert.IsTrue(created.IsSuccess);
            var armyId = created.Value.ArmyId;
            Assert.IsTrue(ArmyService.DisbandArmy(world, armyId).IsSuccess);
            Assert.IsFalse(world.Strategic.FormalArmies.TryGet(armyId, out _));
            Assert.IsFalse(ArmyWorldMapPresentation.ShouldDrawFormalArmyPortrait(world, created.Value));
        }

        [Test]
        public void ArmyUi_CreateArmy_CallsDomainService()
        {
            var world = CreateWorld();
            var a = SpawnCharacter(world, "A", TestNodeA);
            var b = SpawnCharacter(world, "B", TestNodeA);
            var result = ArmyUiCommands.TryCreateArmy(world, TestNodeA, TestFactionA, new[] { a, b });
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, world.Strategic.FormalArmies.Armies.Count);
        }

        [Test]
        public void ArmyUi_CannotBypassArmyServiceInvariant()
        {
            var world = CreateWorld();
            var a = SpawnCharacter(world, "A", TestNodeA);
            var xFaction = world.Entities.CreateCharacter(new DefinitionId("test", "X"), "X").Value;
            xFaction.Get<FactionMembershipComponent>().Assign("test:faction_b", FactionRoleKind.Member);
            world.WorldPresence.SetAtNode(xFaction.Id, TestNodeA);

            var result = ArmyUiCommands.TryCreateArmy(
                world,
                TestNodeA,
                TestFactionA,
                new[] { a, xFaction.Id });
            Assert.IsTrue(result.IsFailure);
            Assert.AreEqual(0, world.Strategic.FormalArmies.Armies.Count);
        }

        [Test]
        public void ArmyFormation_PrototypeNode_AllowsPresenceBasedFriendlyNode()
        {
            var world = CreateWorld();
            Assert.IsTrue(world.WorldGraph.TryGetNode(TestNodeA, out var node));
            node.OwnerId = string.Empty;
            var a = SpawnCharacter(world, "A", TestNodeA);
            Assert.IsTrue(ArmyFormationNodePolicy.IsFriendlyNodeForFaction(world, TestNodeA, TestFactionA));
            Assert.IsTrue(ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { a }).IsSuccess);
        }
    }
}
