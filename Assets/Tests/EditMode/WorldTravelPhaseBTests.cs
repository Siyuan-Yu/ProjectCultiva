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
    }
}
