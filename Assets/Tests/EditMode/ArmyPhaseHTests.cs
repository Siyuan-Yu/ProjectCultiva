using NUnit.Framework;
using XianXia.Core.Npc;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    public sealed class ArmyPhaseHTests
    {
        const string FactionA = "test:faction_a";
        const string FactionB = "test:faction_b";
        const string NodeB = "test:node_b";

        static SimulationWorld CreateWorld()
        {
            var world = new SimulationWorld();
            world.WorldGraph.RegisterNode(new WorldNodeState
            {
                Id = NodeB,
                Name = "B",
                OwnerId = FactionB,
                WorldX = 1f,
                WorldY = 0f
            });
            world.RegisterWorkArea(new WorkAreaDefinition
            {
                Id = "wa_test_core",
                Name = "Core",
                LocationId = "loc_test",
                IsControlCore = true,
                MaxDurability = 50,
                OccupyHoldSeconds = 1f
            });
            CaptureObjectiveService.RegisterControlCore(world, world.ControlCores.All["wa_test_core"], NodeB);
            return world;
        }

        [Test]
        public void Capture_BlockedWithoutWar()
        {
            var world = CreateWorld();
            var assault = CaptureObjectiveService.TryBeginMilitaryAssault(world, FactionA, "wa_test_core");
            Assert.IsTrue(assault.IsFailure);
        }

        [Test]
        public void Capture_AllowedWhenAtWar()
        {
            var world = CreateWorld();
            WarGateService.DeclareWar(world, FactionA, FactionB);
            Assert.IsTrue(CaptureObjectiveService.TryBeginMilitaryAssault(world, FactionA, "wa_test_core").IsSuccess);
        }

        [Test]
        public void Capture_AllObjectives_TransfersNodeOwner()
        {
            var world = CreateWorld();
            WarGateService.DeclareWar(world, FactionA, FactionB);
            world.ControlCores.ApplyDamage("wa_test_core", 100, out _, false);
            world.ControlCores.AddOccupyProgress("wa_test_core", 1f, out _);
            Assert.IsTrue(ControlCoreService.TryCapture(world, "wa_test_core", FactionA).IsSuccess);
            Assert.IsTrue(world.WorldGraph.TryGetNode(NodeB, out var node));
            Assert.AreEqual(FactionA, node.OwnerId);
        }

        [Test]
        public void NodeDefense_CountsGarrisonedArmiesAndResidents()
        {
            var world = CreateWorld();
            Assert.GreaterOrEqual(NodeDefenseService.CountResidents(world, NodeB), 0);
            Assert.AreEqual(0, NodeDefenseService.CountGarrisonedArmies(world, NodeB, FactionB));
        }

        [Test]
        public void ArmyFormationNodePolicy_RequiresOwner_NotPresence()
        {
            var world = new SimulationWorld();
            world.WorldGraph.RegisterNode(new WorldNodeState { Id = "n1", Name = "N1" });
            Assert.IsFalse(ArmyFormationNodePolicy.IsFriendlyNodeForFaction(world, "n1", FactionA));
            world.Strategic.Ch01FormationScenarioCompat = true;
            var created = world.Entities.CreateCharacter(new Core.Domain.Ids.DefinitionId("test", "x"), "x");
            Assert.IsTrue(created.IsSuccess);
            created.Value.Get<Core.Social.FactionMembershipComponent>()
                .Assign(FactionA, Core.Social.FactionRoleKind.Member);
            world.WorldPresence.SetAtNode(created.Value.Id, "n1");
            Assert.IsTrue(Ch01ScenarioArmyFormationPolicy.IsFriendlyNodeForFormation(world, "n1", FactionA));
        }
    }
}
