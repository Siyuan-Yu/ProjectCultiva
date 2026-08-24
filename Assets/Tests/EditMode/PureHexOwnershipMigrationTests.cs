using NUnit.Framework;
using XianXia.Core.Npc;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Tests
{
    /// <summary>Pure Hex Ownership + Character Strategic Presence migration (decisions 1–10).</summary>
    public sealed class PureHexOwnershipMigrationTests
    {
        const string FactionA = "test:faction_a";
        const string FactionB = "test:faction_b";
        const string SiteB = "test:site_b";
        const string NodeB = "test:node_b";

        [Test]
        public void PHOM_01_AtSite_SetAtSite_CollectAtSite()
        {
            var world = new SimulationWorld();
            var a = world.Entities.CreateCharacter(new Core.Domain.Ids.DefinitionId("test", "a"), "A").Value;
            var b = world.Entities.CreateCharacter(new Core.Domain.Ids.DefinitionId("test", "b"), "B").Value;
            world.WorldPresence.SetAtSite(a.Id, SiteB);
            world.WorldPresence.SetAtSite(b.Id, SiteB);
            world.WorldPresence.SetAtNode(b.Id, "other:node");

            var atSite = new System.Collections.Generic.List<Core.Domain.Ids.EntityId>(4);
            world.WorldPresence.CollectAtSite(SiteB, atSite);
            Assert.AreEqual(1, atSite.Count);
            Assert.AreEqual(a.Id, atSite[0]);
            Assert.IsTrue(world.WorldPresence.TryGet(a.Id, out var presence));
            Assert.AreEqual(PartyWorldPresenceMode.AtSite, presence.Mode);
            Assert.AreEqual(SiteB, presence.SiteId);
            Assert.AreEqual(string.Empty, presence.NodeId);
        }

        [Test]
        public void PHOM_02_CaptureObjectiveBoard_IndexesBySiteId()
        {
            var board = new CaptureObjectiveBoard();
            board.Register(new CaptureObjectiveState { ObjectiveId = "capture:wa1", SiteId = SiteB });
            board.Register(new CaptureObjectiveState { ObjectiveId = "capture:wa2", SiteId = SiteB, Completed = true });

            Assert.AreEqual(2, board.GetObjectiveIdsForSite(SiteB).Count);
            Assert.IsFalse(board.AllCompletedForSite(SiteB));
            board.Register(new CaptureObjectiveState { ObjectiveId = "capture:wa1", SiteId = SiteB, Completed = true });
            Assert.IsTrue(board.AllCompletedForSite(SiteB));
        }

        [Test]
        public void PHOM_03_TryCompleteWorldSiteCapture_SetsSiteOwner()
        {
            var world = CreateCaptureWorld();
            WarGateService.DeclareWar(world, FactionA, FactionB);
            world.ControlCores.ApplyDamage("wa_test_core", 100, out _, false);
            world.ControlCores.AddOccupyProgress("wa_test_core", 1f, out _);
            Assert.IsTrue(ControlCoreService.TryCapture(world, "wa_test_core", FactionA).IsSuccess);
            Assert.AreEqual(FactionA, WorldSiteOwnershipService.GetOwner(world, SiteB));
            Assert.IsTrue(world.Strategic.Sites.TryGet(SiteB, out var site));
            Assert.IsTrue(string.IsNullOrEmpty(site.OwnerFactionId));
        }

        [Test]
        public void PHOM_04_ScenarioProgressionHooks_SiteCallback()
        {
            var world = new SimulationWorld();
            var fired = false;
            ScenarioProgressionHooks.OnAllCaptureObjectivesCompletedForSite = (w, siteId) =>
            {
                fired = true;
                Assert.AreEqual(SiteB, siteId);
            };
            ScenarioProgressionHooks.NotifyAllCaptureObjectivesCompletedForSite(world, SiteB);
            Assert.IsTrue(fired);
        }

        [Test]
        public void PHOM_05_Ch01ScenarioProgressionHooks_HuangcunSiteId()
        {
            var world = new SimulationWorld();
            Ch01ScenarioProgressionHooks.Register(world);
            ScenarioProgressionHooks.NotifyAllCaptureObjectivesCompletedForSite(
                world,
                Ch01ScenarioProgressionHooks.HuangcunSiteId);
            Assert.IsTrue(world.Flags.Has(Ch01ScenarioProgressionHooks.FlagPlayerFactionPoliticallyActive));
        }

        [Test]
        public void PHOM_06_RegisterWorkArea_ResolvesSiteIdFromLocalMap()
        {
            var world = new SimulationWorld();
            world.Strategic.Sites.Register(new WorldSite
            {
                SiteId = SiteB,
                LocalMapId = "loc_test",
                OwnerFactionId = FactionB
            });
            world.PartyWorld.SiteId = SiteB;
            world.RegisterWorkArea(new WorkAreaDefinition
            {
                Id = "wa_test_core",
                Name = "Core",
                LocationId = "loc_test",
                IsControlCore = true,
                MaxDurability = 50,
                OccupyHoldSeconds = 1f
            });
            Assert.IsTrue(world.Strategic.CaptureObjectives.TryGet("capture:wa_test_core", out var objective));
            Assert.AreEqual(SiteB, objective.SiteId);
        }

        [Test]
        public void PHOM_07_Ch01ScenarioSetup_DoesNotAssignSiteOwners()
        {
            var world = BootstrapCh01Graph();
            Ch01ScenarioStrategicSetup.Apply(world);
            Assert.IsTrue(world.Strategic.Sites.TryGet(Ch01HexPrototypeMapBuilder.SiteHuangcun, out var huangcun));
            Assert.IsTrue(string.IsNullOrEmpty(huangcun.OwnerFactionId));
        }

        [Test]
        public void PHOM_08_HexWorldContentLoader_DoesNotCopyNodeOwnerToSite()
        {
            var world = new SimulationWorld();
            var definition = new HexWorldContentDefinition
            {
                Id = Core.Domain.Ids.DefinitionId.Parse("test:hex_world").Value,
                Name = "Test",
                Width = 8,
                Height = 8,
                DefaultTerrain = "Plain",
                DefaultPassable = true,
                Sites =
                {
                    new HexWorldSiteDefinition
                    {
                        SiteId = SiteB,
                        DisplayName = "B",
                        SiteType = "Village",
                        AnchorQ = 2,
                        AnchorR = 2,
                        LocalMapId = "loc_test"
                    }
                }
            };
            Assert.IsTrue(HexWorldContentLoader.Apply(world, definition).IsSuccess);
            Assert.IsTrue(world.Strategic.Sites.TryGet(SiteB, out var site));
            Assert.AreEqual(string.Empty, site.OwnerFactionId);
        }

        [Test]
        [Ignore("CreateSiteFromNode removed in Pure Hex Phase B.")]
        public void PHOM_09_CreateSiteFromNode_DoesNotCopyNodeOwner()
        {
        }

        [Test]
        public void PHOM_10_WorldSiteOwnershipService_GetSetAndResolve()
        {
            var world = new SimulationWorld();
            world.Strategic.Sites.Register(new WorldSite
            {
                SiteId = SiteB,
                LocalMapId = "loc_test",
                OwnerFactionId = FactionB
            });
            world.PartyWorld.SiteId = SiteB;
            Assert.AreEqual(FactionB, WorldSiteOwnershipService.GetOwner(world, SiteB));
            WorldSiteOwnershipService.SetOwner(world, SiteB, FactionA);
            Assert.AreEqual(FactionA, WorldSiteOwnershipService.GetOwner(world, SiteB));
            Assert.IsTrue(WorldSiteOwnershipService.TryResolveSiteForLocalMapSession(world, "loc_test", out var site));
            Assert.AreEqual(SiteB, site.SiteId);
        }

        static SimulationWorld CreateCaptureWorld()
        {
            var world = new SimulationWorld();
            world.Strategic.Sites.Register(new WorldSite
            {
                SiteId = SiteB,
                LocalMapId = "loc_test",
                OwnerFactionId = FactionB
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

        static SimulationWorld BootstrapCh01Graph()
        {
            var world = new SimulationWorld();
            Ch01HexPrototypeMapBuilder.Build(world);
            return world;
        }
    }
}
