using NUnit.Framework;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    public sealed class Ch01FullHexMapTests
    {
        [Test]
        public void PrototypeBanditPatrol_IsSevenOrMoreHexesFromHuangcun()
        {
            var world = new SimulationWorld();
            LoadCh01Graph(world);
            Ch01HexPrototypeMapBuilder.BuildFullFromWorldGraph(world);
            Ch01ScenarioStrategicSetup.Apply(world);

            Assert.IsTrue(world.Strategic.Sites.TryGet("base:site_huangcun", out var huangcun));
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(
                ArmyStackAdapter.BanditPatrolFormalArmyId,
                out var bandit));
            Assert.IsTrue(bandit.UsesHexStrategicPosition);
            Assert.GreaterOrEqual(HexMath.Distance(huangcun.AnchorHex, bandit.CurrentHex), 7);
            Assert.IsFalse(huangcun.OccupiesHex(bandit.CurrentHex));
        }

        [Test]
        public void H9_FullCh01Map_RegistersAllWorldGraphNodesAsSites()
        {
            var world = new SimulationWorld();
            LoadCh01Graph(world);
            Ch01HexPrototypeMapBuilder.BuildFullFromWorldGraph(world);

            Assert.GreaterOrEqual(world.Strategic.Sites.Sites.Count, 3);
            Assert.IsTrue(world.Strategic.Sites.TryGet("base:site_huangcun", out var huangcun));
            Assert.AreEqual(HexWorldScale.PlayableV1Width, world.HexWorld.Width);
            Assert.AreEqual(HexWorldScale.PlayableV1Height, world.HexWorld.Height);
            Assert.AreEqual(HexWorldScale.PlayableV1Width * HexWorldScale.PlayableV1Height, world.HexWorld.CellCount);
            Assert.IsTrue(world.HexWorld.UsesCompactStorage);
            Assert.IsTrue(huangcun.OccupiesHex(huangcun.AnchorHex));
        }

        static void LoadCh01Graph(SimulationWorld world)
        {
            world.WorldGraph.RegisterNode(new WorldNodeState
            {
                Id = "base:node_huangcun",
                Name = "青石荒村",
                WorldX = 0f,
                WorldY = 0f
            });
            world.WorldGraph.RegisterNode(new WorldNodeState
            {
                Id = "base:node_qingyun_lu",
                Name = "青云路",
                WorldX = -1f,
                WorldY = 1f
            });
            world.WorldGraph.RegisterNode(new WorldNodeState
            {
                Id = "base:node_guanai",
                Name = "关隘",
                WorldX = 2f,
                WorldY = 1f
            });
            world.WorldGraph.RegisterRoute(new WorldRouteState
            {
                Id = "test:route",
                FromNodeId = "base:node_huangcun",
                ToNodeId = "base:node_guanai"
            });
        }
    }
}
