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

            Assert.AreEqual(HexWorldScale.PlayableV1Width, world.LegacyHexWorld.Width);
            Assert.AreEqual(HexWorldScale.PlayableV1Height, world.LegacyHexWorld.Height);
            Assert.AreEqual(5000, world.LegacyHexWorld.CellCount);
            Assert.IsTrue(world.LegacyHexWorld.UsesCompactStorage);
            Assert.AreEqual(HexWorldScale.DefaultHexOuterRadius, world.LegacyHexWorld.HexSize, 0.001f);
        }

        [Test]
        public void STRESS_Map_Is20kCells()
        {
            var world = new SimulationWorld();
            HexWorldStressMapBuilder.Build(world);

            Assert.AreEqual(HexWorldScale.StressTestWidth, world.LegacyHexWorld.Width);
            Assert.AreEqual(HexWorldScale.StressTestHeight, world.LegacyHexWorld.Height);
            Assert.AreEqual(20_000, world.LegacyHexWorld.CellCount);
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
