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
    public sealed class StrategicFinalClosureTests
    {
        const string FactionA = "test:faction_a";
        const string NodeA = "test:node_a";
        const string NodeB = "test:node_b";

        [Test]
        public void SnapshotV1_ExplicitlyRejected()
        {
            var service = new SnapshotService(new JsonSnapshotSerializer());
            var snap = new WorldSnapshot { SchemaVersion = WorldSnapshot.LegacySchemaVersion };
            var restore = service.Restore(snap);
            Assert.IsTrue(restore.IsFailure);
            Assert.IsTrue(restore.Error.Message.IndexOf("unsupported", System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        [Test]
        public void GenericBootstrap_DelegatesToCh01ScenarioSetup()
        {
            var world = new SimulationWorld();
            StrategicBootstrap.ApplyCh01Defaults(world);
            Assert.IsTrue(world.Strategic.Ch01FormationScenarioCompat);
        }

        [Test]
        public void Ch01ScenarioSetup_PrototypeWar_IsBanditRegressionOnly()
        {
            var world = new SimulationWorld();
            Ch01ScenarioStrategicSetup.Apply(world);
            Assert.IsTrue(WarGateService.IsAtWar(world, StrategicFactionCatalog.PlayerFactionId, StrategicFactionCatalog.BanditId));
        }

        [Test]
        public void Ch01ScenarioSetup_BindsProtagonistFactionAsVassalOfHuangcunLabor()
        {
            var world = new SimulationWorld();
            Ch01ScenarioStrategicSetup.Apply(world);
            Assert.AreEqual(StrategicFactionCatalog.PlayerFactionId, world.Strategic.PlayerFactionId);
            Assert.IsTrue(world.Strategic.Vassalages.TryGetOverlord(
                StrategicFactionCatalog.PlayerFactionId,
                out var overlord));
            Assert.AreEqual(StrategicFactionCatalog.HuangcunLaborId, overlord);
        }

        [Test]
        public void Ch01ScenarioSetup_AssignsRegionalTerritoryOwnersViaContentSites()
        {
            var world = BootstrapCh01Graph();
            RegisterCh01TerritorySites(world);
            Ch01ScenarioStrategicSetup.Apply(world);

            Assert.AreEqual(3, StrategicAcceptanceInspector.CountOwnedSites(
                world, StrategicFactionCatalog.HuangcunLaborId));
            Assert.AreEqual(StrategicFactionCatalog.HuangcunLaborId,
                WorldSiteOwnershipService.GetOwner(world, "base:site_huangcun"));

            Assert.AreEqual(2, StrategicAcceptanceInspector.CountOwnedSites(
                world, StrategicFactionCatalog.NanYanLeagueId));
            Assert.AreEqual(3, StrategicAcceptanceInspector.CountOwnedSites(
                world, StrategicFactionCatalog.FisherVillageId));
            Assert.AreEqual(2, StrategicAcceptanceInspector.CountOwnedSites(
                world, StrategicFactionCatalog.ShuoFengFortId));
            Assert.AreEqual(3, StrategicAcceptanceInspector.CountOwnedSites(
                world, StrategicFactionCatalog.DongLinGuildId));
            Assert.AreEqual(2, StrategicAcceptanceInspector.CountOwnedSites(
                world, StrategicFactionCatalog.XiJinGuildId));

            Assert.IsTrue(world.Strategic.Sites.TryGet("base:node_huangcun", out var huangcun));
            Assert.IsTrue(string.IsNullOrEmpty(huangcun.OwnerFactionId));
            Assert.AreEqual(StrategicFactionCatalog.NanYanLeagueId,
                WorldSiteOwnershipService.GetOwner(world, "base:site_nan"));
            Assert.AreEqual(StrategicFactionCatalog.FisherVillageId,
                WorldSiteOwnershipService.GetOwner(world, "base:site_haijiao"));
            Assert.AreEqual(StrategicFactionCatalog.XiJinGuildId,
                WorldSiteOwnershipService.GetOwner(world, "base:site_xi"));
        }

        static void RegisterCh01TerritorySites(SimulationWorld world)
        {
            HexTestWorldBootstrap.EnsureMinimalHexMap(world);
            RegisterSite(world, "base:site_huangcun", StrategicFactionCatalog.HuangcunLaborId);
            RegisterSite(world, "base:site_lu", StrategicFactionCatalog.HuangcunLaborId);
            RegisterSite(world, "base:site_lingdi", StrategicFactionCatalog.HuangcunLaborId);
            RegisterSite(world, "base:site_nan", StrategicFactionCatalog.NanYanLeagueId);
            RegisterSite(world, "base:site_zhuangyuan", StrategicFactionCatalog.NanYanLeagueId);
            RegisterSite(world, "base:site_haijiao", StrategicFactionCatalog.FisherVillageId);
            RegisterSite(world, "base:site_shuizhai", StrategicFactionCatalog.FisherVillageId);
            RegisterSite(world, "base:site_yucun", StrategicFactionCatalog.FisherVillageId);
            RegisterSite(world, "base:site_bei", StrategicFactionCatalog.ShuoFengFortId);
            RegisterSite(world, "base:site_shankou", StrategicFactionCatalog.ShuoFengFortId);
            RegisterSite(world, "base:site_dong", StrategicFactionCatalog.DongLinGuildId);
            RegisterSite(world, "base:site_miao", StrategicFactionCatalog.DongLinGuildId);
            RegisterSite(world, "base:site_gudao", StrategicFactionCatalog.DongLinGuildId);
            RegisterSite(world, "base:site_xi", StrategicFactionCatalog.XiJinGuildId);
            RegisterSite(world, "base:site_yaotian", StrategicFactionCatalog.XiJinGuildId);
        }

        static void RegisterSite(
            SimulationWorld world,
            string siteId,
            string ownerFactionId)
        {
            world.Strategic.Sites.Register(new WorldSite
            {
                SiteId = siteId,
                OwnerFactionId = ownerFactionId
            });
        }

        static SimulationWorld BootstrapCh01Graph()
        {
            var world = new SimulationWorld();
            HexTestWorldBootstrap.EnsureCh01HexMap(world);
            return world;
        }

        [Test]
        public void PlayerUngroupedCharacter_CannotUseMacroTravelPathService()
        {
            var world = new SimulationWorld();
            var created = world.Entities.CreateCharacter(new DefinitionId("test", "solo"), "Solo");
            Assert.IsTrue(created.IsSuccess);
            world.WorldPresence.SetAtSite(created.Value.Id, "test:site_a");

            Assert.IsFalse(WorldTravelService.CanReceivePlayerMacroTravelOrder(world, created.Value.Id));
        }

        [Test]
        public void PlayerLegacyPursuit_BlockedWithoutFormalArmy()
        {
            var world = new SimulationWorld();
            var player = world.Entities.CreateCharacter(new DefinitionId("test", "p"), "P").Value;
            world.WorldPresence.SetAtNode(player.Id, NodeA);
            var stack = new ArmyStack { Id = "enemy", FactionId = "enemy:faction", NodeId = NodeB };
            world.Strategic.Armies.Register(stack);

            StrategicPursuitService.BeginPursuit(world, new[] { player.Id }, stack);
            Assert.IsFalse(stack.IsTraveling);
        }

        [Test]
        public void CaptureCompletion_NotifiesScenarioHook()
        {
            var world = new SimulationWorld();
            Ch01ScenarioProgressionHooks.Register(world);
            ScenarioProgressionHooks.NotifyAllCaptureObjectivesCompletedForSite(
                world,
                Ch01ScenarioProgressionHooks.HuangcunSiteId);
            Assert.IsTrue(world.Flags.Has(Ch01ScenarioProgressionHooks.FlagPlayerFactionPoliticallyActive));
        }

        [Test]
        public void ArmyFormationSitePolicy_RequiresSiteOwner_NotPresenceOnly()
        {
            var world = new SimulationWorld();
            HexTestWorldBootstrap.EnsureMinimalHexMap(world);
            world.Strategic.Sites.Register(new WorldSite
            {
                SiteId = "test:site_a",
                OwnerFactionId = "test:faction_other",
                LocalMapId = "loc_test",
                AnchorHex = new XianXia.Core.World.Hex.HexCoord(1, 1)
            });
            var c = world.Entities.CreateCharacter(new DefinitionId("test", "c"), "C").Value;
            c.Get<FactionMembershipComponent>().Assign(FactionA, FactionRoleKind.Member);
            world.WorldPresence.SetAtSite(c.Id, "test:site_a");

            Assert.IsFalse(ArmyFormationSitePolicy.TryValidateFriendlySiteForSiteId(
                world, FactionA, "test:site_a", out _));
        }
    }
}
