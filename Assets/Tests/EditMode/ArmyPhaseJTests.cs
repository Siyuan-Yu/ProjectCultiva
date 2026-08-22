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
    public sealed class ArmyPhaseJTests
    {
        const string FactionA = "test:faction_a";
        const string NodeA = "test:node_a";

        [Test]
        public void BattleAftermath_AssignsCapturedState()
        {
            var world = new SimulationWorld();
            world.WorldGraph.RegisterNode(new WorldNodeState { Id = NodeA, OwnerId = FactionA });
            var created = world.Entities.CreateCharacter(new DefinitionId("test", "P"), "P");
            Assert.IsTrue(created.IsSuccess);
            var id = created.Value.Id;
            BattleAftermathService.TestCaptureChancePlaceholder = 1.0;
            Assert.IsTrue(BattleAftermathService.TryAssignCaptured(world, id, FactionA).IsSuccess);
            Assert.IsTrue(created.Value.TryGet<LifecycleComponent>(out var life));
            Assert.AreEqual(LifecycleState.Captured, life.State);
        }

        [Test]
        public void BattleAftermath_EscapedFormsRetreatingArmy()
        {
            var world = new SimulationWorld();
            world.WorldGraph.RegisterNode(new WorldNodeState { Id = NodeA, OwnerId = FactionA });
            var a = world.Entities.CreateCharacter(new DefinitionId("test", "A"), "A").Value;
            a.Get<FactionMembershipComponent>().Assign(FactionA, FactionRoleKind.Member);
            world.WorldPresence.SetAtNode(a.Id, NodeA);
            var b = world.Entities.CreateCharacter(new DefinitionId("test", "B"), "B").Value;
            b.Get<FactionMembershipComponent>().Assign(FactionA, FactionRoleKind.Member);
            world.WorldPresence.SetAtNode(b.Id, NodeA);
            var army = ArmyService.CreateArmy(world, FactionA, NodeA, new List<EntityId> { a.Id }).Value;
            Assert.IsTrue(BattleAftermathService.TryAssignEscapedAndRetreat(
                world, army.ArmyId, new List<EntityId> { a.Id, b.Id }, NodeA).IsSuccess);
            Assert.IsTrue(world.Strategic.RetreatingArmies.TryGet("retreat:1", out var retreat));
            Assert.AreEqual(2, retreat.MemberCharacterIds.Count);
        }

        [Test]
        public void LandlessFaction_DetectsNoOwnedNodes()
        {
            var world = new SimulationWorld();
            var created = world.Entities.CreateCharacter(new DefinitionId("test", "L"), "L");
            created.Value.Get<FactionMembershipComponent>().Assign(FactionA, FactionRoleKind.Member);
            Assert.IsTrue(LandlessFactionService.IsLandless(world, FactionA));
        }
    }
}
