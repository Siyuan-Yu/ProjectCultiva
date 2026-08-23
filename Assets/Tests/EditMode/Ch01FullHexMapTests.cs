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
        public void PrototypeTestBandits_AreStationaryAtSouthAndEastOfHuangcun()
        {
            var world = new SimulationWorld();
            LoadCh01Graph(world);
            Ch01HexPrototypeMapBuilder.BuildFullFromWorldGraph(world);
            Ch01ScenarioStrategicSetup.Apply(world);

            Assert.IsTrue(world.Strategic.Sites.TryGet("base:site_huangcun", out var huangcun));
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(
                ArmyStackAdapter.BanditPatrolFormalArmyId,
                out var strongBandit));
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(
                ArmyStackAdapter.BanditWeakPatrolFormalArmyId,
                out var weakBandit));
            Assert.IsTrue(strongBandit.UsesHexStrategicPosition);
            Assert.IsTrue(weakBandit.UsesHexStrategicPosition);
            Assert.AreEqual(FormalArmyState.Idle, strongBandit.State);
            Assert.AreEqual(FormalArmyState.Idle, weakBandit.State);

            // 南侧红框：R 更大；东侧红框：Q 更大
            Assert.Greater(strongBandit.CurrentHex.R, huangcun.AnchorHex.R);
            Assert.Greater(weakBandit.CurrentHex.Q, huangcun.AnchorHex.Q);
            Assert.AreEqual(
                new HexCoord(huangcun.AnchorHex.Q + 2, huangcun.AnchorHex.R + 4),
                strongBandit.CurrentHex);
            Assert.AreEqual(
                new HexCoord(huangcun.AnchorHex.Q + 6, huangcun.AnchorHex.R),
                weakBandit.CurrentHex);
            Assert.IsFalse(huangcun.OccupiesHex(strongBandit.CurrentHex));
            Assert.IsFalse(huangcun.OccupiesHex(weakBandit.CurrentHex));
            Assert.IsTrue(world.Strategic.Armies.TryGet(ArmyStackAdapter.BanditPatrolStackId, out var strongStack));
            Assert.IsTrue(world.Strategic.Armies.TryGet(ArmyStackAdapter.BanditWeakPatrolStackId, out var weakStack));
            Assert.IsFalse(strongStack.IsTraveling);
            Assert.IsFalse(weakStack.IsTraveling);
            ArmyStackAdapter.RefreshDerivedPresentation(world, strongStack);
            ArmyStackAdapter.RefreshDerivedPresentation(world, weakStack);
            Assert.Greater(ArmyStackAdapter.GetCombatPower(world, strongStack),
                ArmyStackAdapter.GetCombatPower(world, weakStack));
            Assert.AreEqual(4, ArmyStackAdapter.GetMemberCount(world, strongStack));
            Assert.AreEqual(1, ArmyStackAdapter.GetMemberCount(world, weakStack));
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
