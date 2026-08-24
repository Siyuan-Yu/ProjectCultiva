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
            const string siteId = "test:site_a";
            world.Strategic.Sites.Register(new WorldSite
            {
                SiteId = siteId,
                OwnerFactionId = FactionA,
            });
            var a = world.Entities.CreateCharacter(new DefinitionId("test", "A"), "A").Value;
            a.Get<FactionMembershipComponent>().Assign(FactionA, FactionRoleKind.Member);
            world.WorldPresence.SetAtSite(a.Id, siteId);
            var army = ArmyService.CreateArmy(world, FactionA, siteId, new[] { a.Id }).Value;
            WorldSiteOwnershipService.SetOwner(world, "test:site_a", FactionA);
            WarGateService.DeclareWar(world, FactionA, "test:faction_b");

            var service = new SnapshotService(new JsonSnapshotSerializer());
            var captured = service.Capture(world, new SimulationLoop(world));
            Assert.AreEqual(WorldSnapshot.CurrentSchemaVersion, captured.SchemaVersion);
            Assert.AreEqual(1, captured.Strategic.FormalArmies.Count);
            Assert.AreEqual(army.ArmyId, captured.Strategic.FormalArmies[0].ArmyId);
            Assert.GreaterOrEqual(captured.Strategic.Wars.Count, 1);
            Assert.AreEqual(1, captured.Strategic.WorldSiteOwners.Count);

            var restored = service.Restore(captured);
            Assert.IsTrue(restored.IsSuccess);
            var rw = restored.Value.world;
            Assert.IsTrue(rw.Strategic.FormalArmies.TryGet(army.ArmyId, out var restoredArmy));
            Assert.AreEqual(FactionA, restoredArmy.FactionId);
            Assert.IsTrue(WarGateService.IsAtWar(rw, FactionA, "test:faction_b"));
            Assert.AreEqual(FactionA, WorldSiteOwnershipService.GetOwner(rw, "test:site_a"));
        }
    }
}
