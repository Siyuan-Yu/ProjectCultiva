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
            const string siteId = Ch01HexPrototypeMapBuilder.SiteHuangcun;
            HexTestWorldBootstrap.EnsureMinimalHexMap(world);
            var a = world.Entities.CreateCharacter(new DefinitionId("test", "A"), "A").Value;
            a.Get<FactionMembershipComponent>().Assign(FactionA, FactionRoleKind.Member);
            world.WorldPresence.SetAtSite(a.Id, siteId);
            var army = ArmyService.CreateArmy(world, FactionA, siteId, new[] { a.Id }).Value;
            WorldSiteOwnershipService.SetOwner(world, siteId, FactionA);
            WarGateService.DeclareWar(world, FactionA, "test:faction_b");

            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, new SimulationLoop(world));
            Assert.IsTrue(json.IsSuccess);
            var captured = new JsonSnapshotSerializer().Deserialize(json.Value);
            Assert.IsTrue(captured.IsSuccess);
            Assert.AreEqual(WorldSnapshot.CurrentSchemaVersion, captured.Value.SchemaVersion);
            Assert.AreEqual(1, captured.Value.Strategic.FormalArmies.Count);
            Assert.AreEqual(army.ArmyId, captured.Value.Strategic.FormalArmies[0].ArmyId);
            Assert.GreaterOrEqual(captured.Value.Strategic.Wars.Count, 1);
            Assert.AreEqual(1, captured.Value.Strategic.WorldSiteOwners.Count);

            var restored = service.RestoreJson(json.Value);
            Assert.IsTrue(restored.IsSuccess);
            var rw = restored.Value.world;
            HexTestWorldBootstrap.EnsureMinimalHexMap(rw);
            Assert.IsTrue(StrategicSnapshotHelper.Restore(rw, captured.Value.Strategic).IsSuccess);
            Assert.IsTrue(StrategicSnapshotHelper.RestoreHexPoliticalState(rw, captured.Value.Strategic).IsSuccess);
            Assert.IsTrue(StrategicSnapshotHelper.RestoreFormalArmyMotions(rw, captured.Value.Strategic).IsSuccess);
            Assert.IsTrue(rw.Strategic.FormalArmies.TryGet(army.ArmyId, out var restoredArmy));
            Assert.AreEqual(FactionA, restoredArmy.FactionId);
            Assert.IsTrue(WarGateService.IsAtWar(rw, FactionA, "test:faction_b"));
            Assert.AreEqual(FactionA, WorldSiteOwnershipService.GetOwner(rw, siteId));
        }
    }
}
