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
    /// <summary>Manual Acceptance UI command wrappers â€?Domain wiring only (not IMGUI).</summary>
    public sealed class StrategicAcceptanceTests
    {
        const string FactionA = "test:faction_a";
        const string FactionB = "test:faction_b";
        const string FactionC = "test:faction_c";
        const string NodeA = "test:node_a";

        [Test]
        public void Acceptance_DeclareWar_UsesWarDomain()
        {
            var world = new SimulationWorld();
            Assert.IsTrue(StrategicAcceptanceCommands.TryDeclareWar(world, FactionA, FactionB).IsSuccess);
            Assert.IsTrue(StrategicAcceptanceCommands.IsAtWar(world, FactionA, FactionB));
            Assert.AreEqual(1, world.Strategic.Wars.All.Count);
        }

        [Test]
        public void Acceptance_CreateAlliance_UsesAllianceDomain()
        {
            var world = new SimulationWorld();
            Assert.IsTrue(StrategicAcceptanceCommands.TryFormAlliance(world, FactionA, FactionB).IsSuccess);
            Assert.IsTrue(world.Strategic.Alliances.TryGetAllianceId(FactionA, out var allianceId));
            Assert.IsFalse(string.IsNullOrEmpty(allianceId));
            Assert.IsFalse(StrategicAcceptanceCommands.TryFormAlliance(world, FactionA, FactionC).IsSuccess);
        }

        [Test]
        public void Acceptance_CreateVassalage_UsesVassalageDomain()
        {
            var world = new SimulationWorld();
            Assert.IsTrue(StrategicAcceptanceCommands.TryBindVassalage(world, FactionB, FactionA).IsSuccess);
            Assert.IsTrue(world.Strategic.Vassalages.TryGetOverlord(FactionA, out var overlord));
            Assert.AreEqual(FactionB, overlord);
            Assert.IsFalse(StrategicAcceptanceCommands.TryFormAlliance(world, FactionA, FactionC).IsSuccess);
        }

        [Test]
        public void Acceptance_ArmyAddMember_UsesArmyService()
        {
            var world = BootstrapArmyNode(out var leader, out var recruit);
            var create = ArmyUiCommands.TryCreateArmy(world, NodeA, FactionA, new[] { leader });
            Assert.IsTrue(create.IsSuccess);
            var add = StrategicAcceptanceCommands.TryAddArmyMember(world, create.Value.ArmyId, recruit);
            Assert.IsTrue(add.IsSuccess);
            Assert.IsTrue(create.Value.ContainsMember(recruit));
        }

        [Test]
        public void Acceptance_ArmyChangeLeader_UsesArmyService()
        {
            var world = BootstrapArmyNode(out var leader, out var recruit);
            var create = ArmyUiCommands.TryCreateArmy(world, NodeA, FactionA, new[] { leader, recruit });
            Assert.IsTrue(create.IsSuccess);
            var change = StrategicAcceptanceCommands.TryChangeArmyLeader(world, create.Value.ArmyId, recruit);
            Assert.IsTrue(change.IsSuccess);
            Assert.AreEqual(recruit, create.Value.LeaderCharacterId);
        }

        [Test]
        public void Acceptance_AftermathView_ReadsActualAftermathState()
        {
            var world = new SimulationWorld();
            var cap = world.Entities.CreateCharacter(new DefinitionId("test", "cap"), "Cap");
            var esc = world.Entities.CreateCharacter(new DefinitionId("test", "esc"), "Esc");
            Assert.IsTrue(cap.IsSuccess);
            Assert.IsTrue(esc.IsSuccess);
            Assert.IsTrue(BattleAftermathService.TryAssignCaptured(world, cap.Value.Id, FactionA).IsSuccess);
            Assert.IsTrue(BattleAftermathService.TryAssignEscapedAndRetreat(
                world,
                "army:source",
                new[] { esc.Value.Id },
                NodeA).IsSuccess);

            var report = StrategicAcceptanceInspector.BuildAftermathReport(world);
            Assert.AreEqual(1, report.Captured.Count);
            Assert.AreEqual(1, report.RetreatingArmies.Count);
        }

        [Test]
        public void Acceptance_SiteOwner_ReadsOwnerFactionId()
        {
            var world = new SimulationWorld();
            Ch01HexPrototypeMapBuilder.Build(world);
            var site = new WorldSite
            {
                SiteId = NodeA,
                DisplayName = "NodeA",
                OwnerFactionId = FactionB,
                AnchorHex = Ch01HexPrototypeMapBuilder.HuangcunHex,
            };
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, site);
            var line = StrategicAcceptanceInspector.BuildSiteOwnerLine(world, site);
            StringAssert.Contains(FactionB, line);
            Assert.AreEqual(1, StrategicAcceptanceInspector.CountOwnedSites(world, FactionB));
        }

        [Test]
        public void Acceptance_Snapshot_UsesSchemaV2()
        {
            var world = new SimulationWorld();
            var service = new SnapshotService(new JsonSnapshotSerializer());
            var captured = service.Capture(world, new SimulationLoop(world));
            Assert.AreEqual(WorldSnapshot.CurrentSchemaVersion, captured.SchemaVersion);

            var legacy = new WorldSnapshot { SchemaVersion = WorldSnapshot.LegacySchemaVersion };
            var restore = service.Restore(legacy);
            Assert.IsTrue(restore.IsFailure);
            StringAssert.Contains("schema v1", restore.Error.Message.ToLowerInvariant());
        }

        static SimulationWorld BootstrapArmyNode(out EntityId leader, out EntityId recruit)
        {
            var world = new SimulationWorld();
            var l = world.Entities.CreateCharacter(new DefinitionId("test", "leader"), "Leader");
            var r = world.Entities.CreateCharacter(new DefinitionId("test", "recruit"), "Recruit");
            Assert.IsTrue(l.IsSuccess);
            Assert.IsTrue(r.IsSuccess);
            leader = l.Value.Id;
            recruit = r.Value.Id;
            l.Value.Get<FactionMembershipComponent>().Assign(FactionA, FactionRoleKind.Member);
            r.Value.Get<FactionMembershipComponent>().Assign(FactionA, FactionRoleKind.Member);
            world.WorldPresence.SetAtSite(leader, NodeA);
            world.WorldPresence.SetAtSite(recruit, NodeA);
            return world;
        }
    }
}
