using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Data.Serialization;

namespace XianXia.Tests
{
    public sealed class ArmyPhaseKTests
    {
        const string FactionA = "test:faction_a";
        const string NodeA = "test:node_a";

        [Test]
        public void StrategicSnapshot_RoundTripFormalArmyAndWar()
        {
            var world = new SimulationWorld();
            world.WorldGraph.RegisterNode(new WorldNodeState { Id = NodeA, OwnerId = FactionA });
            var a = world.Entities.CreateCharacter(new DefinitionId("test", "A"), "A").Value;
            a.Get<FactionMembershipComponent>().Assign(FactionA, FactionRoleKind.Member);
            world.WorldPresence.SetAtNode(a.Id, NodeA);
            var army = ArmyService.CreateArmy(world, FactionA, NodeA, new[] { a.Id }).Value;
            WarGateService.DeclareWar(world, FactionA, "test:faction_b");

            var service = new SnapshotService(new JsonSnapshotSerializer());
            var captured = service.Capture(world, new SimulationLoop(world));
            Assert.AreEqual(WorldSnapshot.CurrentSchemaVersion, captured.SchemaVersion);
            Assert.AreEqual(1, captured.Strategic.FormalArmies.Count);
            Assert.AreEqual(army.ArmyId, captured.Strategic.FormalArmies[0].ArmyId);
            Assert.GreaterOrEqual(captured.Strategic.Wars.Count, 1);

            var restored = service.Restore(captured);
            Assert.IsTrue(restored.IsSuccess);
            var rw = restored.Value.world;
            Assert.IsTrue(rw.Strategic.FormalArmies.TryGet(army.ArmyId, out var restoredArmy));
            Assert.AreEqual(FactionA, restoredArmy.FactionId);
            Assert.IsTrue(WarGateService.IsAtWar(rw, FactionA, "test:faction_b"));
            Assert.IsTrue(rw.WorldGraph.TryGetNode(NodeA, out var node));
            Assert.AreEqual(FactionA, node.OwnerId);
        }
    }
}
