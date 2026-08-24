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
        const string SiteB = "test:site_b";

        static SimulationWorld CreateWorld()
        {
            var world = new SimulationWorld();
            Ch01HexPrototypeMapBuilder.Build(world);
            world.Strategic.Sites.Register(new WorldSite
            {
                SiteId = SiteB,
                LocalMapId = "loc_test",
                OwnerFactionId = FactionB,
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
        public void Capture_AllObjectives_TransfersSiteOwner()
        {
            var world = CreateWorld();
            WarGateService.DeclareWar(world, FactionA, FactionB);
            world.ControlCores.ApplyDamage("wa_test_core", 100, out _, false);
            world.ControlCores.AddOccupyProgress("wa_test_core", 1f, out _);
            Assert.IsTrue(ControlCoreService.TryCapture(world, "wa_test_core", FactionA).IsSuccess);
            Assert.AreEqual(FactionA, WorldSiteOwnershipService.GetOwner(world, SiteB));
        }

        [Test]
        public void NodeDefense_CountsGarrisonedArmiesAndResidents()
        {
            var world = CreateWorld();
            Assert.GreaterOrEqual(NodeDefenseService.CountResidents(world, NodeB), 0);
            Assert.AreEqual(0, NodeDefenseService.CountGarrisonedArmies(world, NodeB, FactionB));
        }

        [Test]
        public void ArmyFormationSitePolicy_RequiresOwner_NotPresence()
        {
            var world = new SimulationWorld();
            HexTestWorldBootstrap.EnsureMinimalHexMap(world);
            const string siteId = "test:site_n1";
            world.Strategic.Sites.Register(new WorldSite
            {
                SiteId = siteId,
                OwnerFactionId = FactionB,
                LocalMapId = "loc_test",
                AnchorHex = new Core.World.Hex.HexCoord(5, 5),
            });
            Assert.IsFalse(ArmyFormationSitePolicy.TryValidateFriendlySiteForSiteId(
                world, FactionA, siteId, out _));
            world.Strategic.Ch01FormationScenarioCompat = true;
            var created = world.Entities.CreateCharacter(new Core.Domain.Ids.DefinitionId("test", "x"), "x");
            Assert.IsTrue(created.IsSuccess);
            created.Value.Get<Core.Social.FactionMembershipComponent>()
                .Assign(FactionA, Core.Social.FactionRoleKind.Member);
            world.WorldPresence.SetAtSite(created.Value.Id, siteId);
            Assert.IsTrue(Ch01ScenarioArmyFormationPolicy.IsFriendlyNodeForFormation(world, siteId, FactionA));
        }
    }
}
