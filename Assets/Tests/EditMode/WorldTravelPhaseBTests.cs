using System;
using System.IO;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.World;
using XianXia.Data.Bootstrap;
using XianXia.Data.Content;

namespace XianXia.Tests
{
    public sealed class WorldTravelPhaseBTests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        [Test]
        public void Ch01_Party_Starts_At_Huangcun_And_Travel_Linjian()
        {
            var started = new PlayableDayBootstrap().Start(
                BaseGamePath,
                new PlayableDayOptions { OpeningScenarioId = "base:scenario_ch01_reference" });
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");

            var world = started.Value.World;
            Assert.IsTrue(world.WorldGraph.HasGraph);
            Assert.GreaterOrEqual(world.WorldGraph.Nodes.Count, 30);
            Assert.AreEqual("base:node_huangcun", world.PartyWorld.NodeId);
            Assert.AreEqual("base:map_ch01_reference", world.PartyWorld.LocalMapId);

            var party = started.Value.CharacterIds;
            Assert.GreaterOrEqual(party.Count, 1);
            for (var i = 0; i < party.Count; i++)
            {
                Assert.IsTrue(world.WorldPresence.TryGet(party[i], out var p));
                Assert.AreEqual("base:node_huangcun", p.NodeId);
            }

            var travel = WorldTravelService.StartTravel(world, party, "base:node_linjian");
            Assert.IsTrue(travel.IsSuccess, travel.IsFailure ? travel.Error.ToString() : "");
            Assert.IsTrue(world.WorldPresence.TryGet(party[0], out var mid));
            Assert.AreEqual(PartyWorldPresenceMode.Traveling, mid.Mode);
            Assert.Greater(mid.TravelTotalTicks, 1);

            var adv = WorldTravelService.AdvanceTravel(world, 500);
            Assert.IsTrue(adv.IsSuccess, adv.IsFailure ? adv.Error.ToString() : "");
            for (var i = 0; i < party.Count; i++)
            {
                Assert.IsTrue(world.WorldPresence.TryGet(party[i], out var p));
                Assert.AreEqual("base:node_linjian", p.NodeId);
                Assert.AreEqual(PartyWorldPresenceMode.AtNode, p.Mode);
            }
        }

        [Test]
        public void Pass_To_Mine_Needs_No_Permit()
        {
            var started = new PlayableDayBootstrap().Start(
                BaseGamePath,
                new PlayableDayOptions { OpeningScenarioId = "base:scenario_ch01_reference" });
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");
            var world = started.Value.World;
            var party = started.Value.CharacterIds;

            Assert.IsTrue(WorldTravelService.StartTravel(world, party, "base:node_guanai").IsSuccess);
            WorldTravelService.AdvanceTravel(world, 100);
            Assert.IsTrue(WorldTravelService.StartTravel(world, party, "base:node_kuangshan").IsSuccess);
            WorldTravelService.AdvanceTravel(world, 100);
            Assert.IsTrue(world.WorldPresence.TryGet(party[0], out var p));
            Assert.AreEqual("base:node_kuangshan", p.NodeId);
        }

        [Test]
        public void MacroPath_MultiHop_ReachesDistantNode()
        {
            var started = new PlayableDayBootstrap().Start(
                BaseGamePath,
                new PlayableDayOptions { OpeningScenarioId = "base:scenario_ch01_reference" });
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");
            var world = started.Value.World;
            var agent = started.Value.CharacterIds[0];
            var target = WorldTravelTarget.AtNode("base:node_kuangshan");

            Assert.IsTrue(WorldTravelPathService.StartAgentTravelToTarget(world, agent, target).IsSuccess);
            WorldTravelService.AdvanceTravel(world, 5000);
            Assert.IsTrue(world.WorldPresence.TryGet(agent, out var p));
            Assert.AreEqual("base:node_kuangshan", p.NodeId);
            Assert.AreEqual(PartyWorldPresenceMode.AtNode, p.Mode);
        }

        [Test]
        public void MacroPath_MidAb_To_MidAc_GoesViaSharedEndpoint_NoDetourOrTeleport()
        {
            WorldTravelPathService.ClearAllQueues();
            var started = new PlayableDayBootstrap().Start(
                BaseGamePath,
                new PlayableDayOptions { OpeningScenarioId = "base:scenario_ch01_reference" });
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");
            var world = started.Value.World;
            var agent = started.Value.CharacterIds[0];

            // 人在荒村→林间 中段
            Assert.IsTrue(world.WorldPresence.TryGet(agent, out var p));
            p.NodeId = "base:node_huangcun";
            p.DestNodeId = "base:node_linjian";
            p.RouteId = "base:route_huangcun_linjian";
            p.AnchorOnRoute(0.4f);

            var target = WorldTravelTarget.OnRoute(
                "base:route_huangcun_guanai",
                "base:node_huangcun",
                "base:node_guanai",
                0.55f);

            Assert.IsTrue(
                WorldTravelPathService.StartAgentTravelToTarget(world, agent, target).IsSuccess);

            // 第一步必须仍在原路上往荒村走，禁止瞬移到关隘路
            Assert.IsTrue(world.WorldPresence.TryGet(agent, out p));
            Assert.AreEqual("base:route_huangcun_linjian", p.RouteId);
            Assert.AreEqual(PartyWorldPresenceMode.Traveling, p.Mode);

            string visitedGuanai = null;
            for (var step = 0; step < 80; step++)
            {
                WorldTravelService.AdvanceTravel(world, 8);
                Assert.IsTrue(world.WorldPresence.TryGet(agent, out p));

                // 全程不得出现「关隘节点已到站却还要再往回走」的绕远
                if (p.Mode == PartyWorldPresenceMode.AtNode &&
                    p.NodeId == "base:node_guanai")
                    visitedGuanai = p.NodeId;

                if (p.Mode == PartyWorldPresenceMode.RouteAnchored &&
                    p.RouteId == "base:route_huangcun_guanai" &&
                    Math.Abs(p.RouteAnchorProgress - 0.55f) <= 0.08f)
                    break;
            }

            Assert.IsNull(visitedGuanai, "不应先走到关隘端再折返到路中");
            Assert.IsTrue(world.WorldPresence.TryGet(agent, out p));
            Assert.AreEqual("base:route_huangcun_guanai", p.RouteId);
            Assert.AreEqual(PartyWorldPresenceMode.RouteAnchored, p.Mode);
            Assert.That(p.RouteAnchorProgress, Is.EqualTo(0.55f).Within(0.08f));
        }
    }
}
