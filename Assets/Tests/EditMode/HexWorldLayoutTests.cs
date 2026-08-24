using NUnit.Framework;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests.EditMode
{
    public sealed class HexWorldLayoutTests
    {
        [Test]
        public void MAP_PRES_02_RectangularBounds_WiderThanTall_For100x50()
        {
            var world = new SimulationWorld();
            Ch01HexPrototypeMapBuilder.BuildMinimalTwoSitePrototype(world);

            HexWorldLayout.ComputeWorldBounds(world.HexWorld, out var minX, out var maxX, out var minY, out var maxY);
            var width = maxX - minX;
            var height = maxY - minY;

            Assert.Greater(width, height * 0.9f);
            Assert.Greater(width, 150f);
        }

        [Test]
        public void MAP_PRES_02_WorldToCoord_RoundTripsCornerCells()
        {
            var coords = new[]
            {
                new HexCoord(0, 0),
                new HexCoord(99, 0),
                new HexCoord(0, 49),
                new HexCoord(99, 49),
                new HexCoord(45, 22),
            };

            foreach (var coord in coords)
            {
                HexWorldLayout.CoordToWorldCenter(coord, HexWorldScale.DefaultHexOuterRadius, out var wx, out var wy);
                var back = HexWorldLayout.WorldToCoord(wx, wy, HexWorldScale.DefaultHexOuterRadius);
                Assert.AreEqual(coord, back, "round-trip failed at " + coord);
            }
        }

        [Test]
        public void MAP_PRES_03_FitViewHalf_UsesViewportAspect()
        {
            var world = new SimulationWorld();
            Ch01HexPrototypeMapBuilder.BuildMinimalTwoSitePrototype(world);

            var wideHalf = HexWorldLayout.ComputeFitViewHalf(1200f, 700f, world.HexWorld);
            var tallHalf = HexWorldLayout.ComputeFitViewHalf(700f, 1200f, world.HexWorld);
            Assert.Greater(wideHalf, 0f);
            Assert.Greater(tallHalf, 0f);
        }

        [Test]
        public void HEX_RENDER_01_FullCh01Map_IteratesApproximately5000Cells()
        {
            var world = new SimulationWorld();
            LoadCh01Graph(world);
            Ch01HexPrototypeMapBuilder.Build(world);

            HexWorldLayout.ComputeWorldBounds(
                world.HexWorld,
                out var minX,
                out var maxX,
                out var minY,
                out var maxY);

            var pad = world.HexWorld.HexSize * 1.2f;
            var count = HexWorldMapRenderBounds.CountVisibleCells(
                world.HexWorld,
                minX,
                maxX,
                minY,
                maxY,
                pad);

            Assert.AreEqual(5000, count);
        }

        static void LoadCh01Graph(SimulationWorld world)
        {
            
            
            }
    }
}
