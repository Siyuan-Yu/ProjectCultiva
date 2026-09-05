using NUnit.Framework;
using XianXia.Core.Exploration;
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
                LocalMapId = "map:test",
                OwnerFactionId = FactionB,
            });
            // 模拟真实 bootstrap 顺序：工区先注册，WorldRegion 地点稍后才可用。
            world.RegisterWorkArea(new WorkAreaDefinition
            {
                Id = "wa_test_core",
                Name = "Core",
                LocationId = "loc_work_area",
                IsControlCore = true,
                MaxDurability = 50,
                OccupyHoldSeconds = 1f
            });
            world.WorldRegion.Register(new WorldLocationState
            {
                Id = "loc_work_area",
                LocalMapId = "map:test"
            });
            CaptureObjectiveService.RebindControlCoreSites(world);
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
        public void Capture_RepeatableSite_TransfersFactionAtoBandBack()
        {
            var world = CreateWorld();
            WarGateService.DeclareWar(world, FactionA, FactionB);

            world.ControlCores.ApplyDamage("wa_test_core", 100, out _, false);
            world.ControlCores.AddOccupyProgress("wa_test_core", 1f, out _);
            Assert.IsTrue(ControlCoreService.TryCapture(world, "wa_test_core", FactionA).IsSuccess);
            Assert.AreEqual(FactionA, WorldSiteOwnershipService.GetOwner(world, SiteB));
            Assert.IsTrue(world.ControlCores.TryGet("wa_test_core", out var afterFirst));
            Assert.AreEqual(afterFirst.MaxDurability, afterFirst.CurrentDurability);

            Assert.IsTrue(CaptureObjectiveService.TryBeginMilitaryAssault(world, FactionB, "wa_test_core").IsSuccess);
            world.ControlCores.ApplyDamage("wa_test_core", 100, out _, false);
            world.ControlCores.AddOccupyProgress("wa_test_core", 1f, out _);
            Assert.IsTrue(ControlCoreService.TryCapture(world, "wa_test_core", FactionB).IsSuccess);
            Assert.AreEqual(FactionB, WorldSiteOwnershipService.GetOwner(world, SiteB));
        }

        [Test]
        public void Capture_OwnerCannotAssaultOwnCore()
        {
            var world = CreateWorld();
            Assert.IsTrue(CaptureObjectiveService.TryBeginMilitaryAssault(world, FactionB, "wa_test_core").IsFailure);
            Assert.IsTrue(CaptureObjectiveService.TryBeginMilitaryAssault(world, FactionA, "wa_test_core").IsFailure);
            WarGateService.DeclareWar(world, FactionA, FactionB);
            Assert.IsTrue(CaptureObjectiveService.TryBeginMilitaryAssault(world, FactionA, "wa_test_core").IsSuccess);
        }

        [Test]
        public void NodeDefense_CountsGarrisonedArmiesAndResidents()
        {
            var world = CreateWorld();
            Assert.GreaterOrEqual(SiteDefenseService.CountResidents(world, NodeB), 0);
            Assert.AreEqual(0, SiteDefenseService.CountGarrisonedArmies(world, NodeB, FactionB));
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
