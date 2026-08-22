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
            world.WorldGraph.RegisterNode(new WorldNodeState { Id = NodeA });
            StrategicBootstrap.ApplyCh01Defaults(world);
            Assert.IsTrue(world.Strategic.Ch01FormationScenarioCompat);
        }

        [Test]
        public void Ch01ScenarioSetup_PrototypeWar_IsBanditRegressionOnly()
        {
            var world = new SimulationWorld();
            world.WorldGraph.RegisterNode(new WorldNodeState { Id = "base:node_huangcun" });
            world.WorldGraph.RegisterNode(new WorldNodeState { Id = "base:node_linjian" });
            world.WorldGraph.RegisterRoute(new WorldRouteState
            {
                Id = "r1",
                FromNodeId = "base:node_huangcun",
                ToNodeId = "base:node_linjian",
                TravelCost = 1
            });
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
        public void Ch01ScenarioSetup_AssignsRegionalTerritoryOwners()
        {
            var world = BootstrapCh01Graph();
            Ch01ScenarioStrategicSetup.Apply(world);

            Assert.AreEqual(3, StrategicAcceptanceInspector.CountOwnedNodes(
                world, StrategicFactionCatalog.HuangcunLaborId));

            Assert.IsTrue(world.WorldGraph.TryGetNode("base:node_huangcun", out var huangcun));
            Assert.AreEqual(StrategicFactionCatalog.HuangcunLaborId, huangcun.OwnerId);

            Assert.AreEqual(2, StrategicAcceptanceInspector.CountOwnedNodes(
                world, StrategicFactionCatalog.NanYanLeagueId));
            Assert.AreEqual(3, StrategicAcceptanceInspector.CountOwnedNodes(
                world, StrategicFactionCatalog.FisherVillageId));
            Assert.AreEqual(2, StrategicAcceptanceInspector.CountOwnedNodes(
                world, StrategicFactionCatalog.ShuoFengFortId));
            Assert.AreEqual(3, StrategicAcceptanceInspector.CountOwnedNodes(
                world, StrategicFactionCatalog.DongLinGuildId));
            Assert.AreEqual(2, StrategicAcceptanceInspector.CountOwnedNodes(
                world, StrategicFactionCatalog.XiJinGuildId));

            Assert.IsTrue(world.WorldGraph.TryGetNode("base:node_cunzhuang_nan", out var nanCun));
            Assert.AreEqual(StrategicFactionCatalog.NanYanLeagueId, nanCun.OwnerId);
            Assert.IsTrue(world.WorldGraph.TryGetNode("base:node_haijiao", out var haijiao));
            Assert.AreEqual(StrategicFactionCatalog.FisherVillageId, haijiao.OwnerId);
            Assert.IsTrue(world.WorldGraph.TryGetNode("base:node_dukou_xi", out var xidu));
            Assert.AreEqual(StrategicFactionCatalog.XiJinGuildId, xidu.OwnerId);
        }

        static SimulationWorld BootstrapCh01Graph()
        {
            var world = new SimulationWorld();
            var nodeIds = new[]
            {
                "base:node_huangcun", "base:node_qingyun_lu", "base:node_lingdi",
                "base:node_cunzhuang_nan", "base:node_zhuangyuan",
                "base:node_haijiao", "base:node_shuizhai", "base:node_yucun",
                "base:node_cunzhuang_bei", "base:node_shankou",
                "base:node_shulin_dong", "base:node_miao", "base:node_gudao",
                "base:node_dukou_xi", "base:node_yaotian",
                "base:node_linjian", "base:node_guanai"
            };
            for (var i = 0; i < nodeIds.Length; i++)
                world.WorldGraph.RegisterNode(new WorldNodeState { Id = nodeIds[i] });
            world.WorldGraph.RegisterRoute(new WorldRouteState
            {
                Id = "base:route_huangcun_linjian",
                FromNodeId = "base:node_huangcun",
                ToNodeId = "base:node_linjian",
                TravelCost = 1
            });
            world.WorldGraph.RegisterRoute(new WorldRouteState
            {
                Id = "base:route_huangcun_guanai",
                FromNodeId = "base:node_huangcun",
                ToNodeId = "base:node_guanai",
                TravelCost = 1
            });
            return world;
        }

        [Test]
        public void PlayerUngroupedCharacter_CannotUseMacroTravelPathService()
        {
            var world = new SimulationWorld();
            world.WorldGraph.RegisterNode(new WorldNodeState { Id = NodeA });
            world.WorldGraph.RegisterNode(new WorldNodeState { Id = NodeB });
            world.WorldGraph.RegisterRoute(new WorldRouteState
            {
                Id = "r",
                FromNodeId = NodeA,
                ToNodeId = NodeB,
                TravelCost = 1
            });
            var created = world.Entities.CreateCharacter(new DefinitionId("test", "solo"), "Solo");
            Assert.IsTrue(created.IsSuccess);
            world.WorldPresence.SetAtNode(created.Value.Id, NodeA);

            var started = WorldTravelPathService.StartAgentTravelToTarget(
                world,
                created.Value.Id,
                WorldTravelTarget.AtNode(NodeB));
            Assert.IsTrue(started.IsFailure);
            StringAssert.Contains("Formal Army", started.Error.Message);
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
            ScenarioProgressionHooks.NotifyAllCaptureObjectivesCompletedForNode(
                world,
                Ch01ScenarioProgressionHooks.HuangcunNodeId);
            Assert.IsTrue(world.Flags.Has(Ch01ScenarioProgressionHooks.FlagPlayerFactionPoliticallyActive));
        }

        [Test]
        public void ArmyFormationNodePolicy_NoPresenceBasedFriendlyNode()
        {
            var world = new SimulationWorld();
            world.WorldGraph.RegisterNode(new WorldNodeState { Id = NodeA, OwnerId = string.Empty });
            var c = world.Entities.CreateCharacter(new DefinitionId("test", "c"), "C").Value;
            c.Get<FactionMembershipComponent>().Assign(FactionA, FactionRoleKind.Member);
            world.WorldPresence.SetAtNode(c.Id, NodeA);

            Assert.IsFalse(ArmyFormationNodePolicy.TryValidateFriendlyNode(world, FactionA, NodeA, out _));
        }
    }
}
