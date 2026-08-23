using NUnit.Framework;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    public sealed class HexWorldScaleTests
    {
        [Test]
        public void PLAYABLE_V1_Is100x50_CompactStorage()
        {
            var world = new SimulationWorld();
            Ch01HexPrototypeMapBuilder.BuildMinimalTwoSitePrototype(world);

            Assert.AreEqual(HexWorldScale.PlayableV1Width, world.HexWorld.Width);
            Assert.AreEqual(HexWorldScale.PlayableV1Height, world.HexWorld.Height);
            Assert.AreEqual(5000, world.HexWorld.CellCount);
            Assert.IsTrue(world.HexWorld.UsesCompactStorage);
            Assert.AreEqual(HexWorldScale.DefaultHexOuterRadius, world.HexWorld.HexSize, 0.001f);
        }

        [Test]
        public void STRESS_Map_Is20kCells()
        {
            var world = new SimulationWorld();
            HexWorldStressMapBuilder.Build(world);

            Assert.AreEqual(HexWorldScale.StressTestWidth, world.HexWorld.Width);
            Assert.AreEqual(HexWorldScale.StressTestHeight, world.HexWorld.Height);
            Assert.AreEqual(20_000, world.HexWorld.CellCount);
        }

        [Test]
        public void COMPACT_IndexRoundTrip()
        {
            var grid = new HexWorld();
            grid.FillRectangle(10, 8);
            var coord = new HexCoord(3, 5);
            var index = grid.CoordToIndex(coord);
            Assert.IsTrue(grid.TryIndexToCoord(index, out var round));
            Assert.AreEqual(coord, round);
        }
    }
}
