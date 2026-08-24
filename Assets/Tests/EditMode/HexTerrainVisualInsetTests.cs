using NUnit.Framework;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Unity.Host;

namespace XianXia.Tests.EditMode
{
    public sealed class HexTerrainVisualInsetTests
    {
        [Test]
        public void HEX_RENDER_03_DebugStrong_UsesCellFillScale080()
        {
            Assert.AreEqual(0.80f, HexTerrainVisualInset.ResolveInsetScale(debugStrongSeparation: true), 0.0001f);
        }

        [Test]
        public void HEX_RENDER_03_Production_UsesCellFillScale096()
        {
            Assert.AreEqual(0.96f, HexTerrainVisualInset.ResolveInsetScale(debugStrongSeparation: false), 0.0001f);
        }

        [Test]
        public void HEX_RENDER_03_InsetCorners_CloserToCenterThanLogical()
        {
            var logicalX = new float[6];
            var logicalY = new float[6];
            var insetX = new float[6];
            var insetY = new float[6];
            var coord = new HexCoord(3, 5);
            const float hexSize = 1f;
            const float inset = HexTerrainVisualInset.ProductionInsetScale;

            HexMath.CollectCornerWorldPositions(coord, hexSize, logicalX, logicalY);
            HexTerrainVisualInset.CollectInsetCornerWorldPositions(coord, hexSize, inset, insetX, insetY);
            HexMath.ToWorldPosition(coord, hexSize, out var cx, out var cy);

            for (var i = 0; i < 6; i++)
            {
                var logicalDist = Dist(logicalX[i], logicalY[i], cx, cy);
                var insetDist = Dist(insetX[i], insetY[i], cx, cy);
                Assert.Less(insetDist, logicalDist);
                Assert.AreEqual(logicalDist * inset, insetDist, 0.0001f);
            }
        }

        [Test]
        public void HEX_RENDER_03_GutterAndPlainFill_AreDifferent()
        {
            var gutter = HexTerrainVisualInset.ProductionGutter;
            var fill = HexTerrainVisualInset.PlainCellFill;
            Assert.Greater(gutter.R + gutter.G + gutter.B, 0.01f);
            Assert.AreNotEqual(gutter.R, fill.R);
            Assert.AreNotEqual(gutter.G, fill.G);
            Assert.AreNotEqual(gutter.B, fill.B);
        }

        static float Dist(float x, float y, float cx, float cy)
        {
            var dx = x - cx;
            var dy = y - cy;
            return (float)System.Math.Sqrt(dx * dx + dy * dy);
        }
    }

    public sealed class HexWorldMapRenderBoundsTests
    {
        [Test]
        public void HEX_RENDER_01_FullCh01Map_IteratesApproximately5000Cells()
        {
            var world = new SimulationWorld();
            LoadCh01Graph(world);
            Ch01HexPrototypeMapBuilder.Build(world);

            HostHexWorldRenderer.ComputeWorldBounds(
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

            Assert.AreEqual(HexWorldScale.PlayableV1Width * HexWorldScale.PlayableV1Height, count);
            Assert.AreEqual(5000, count);
        }

        [Test]
        public void HEX_RENDER_02_VisibleCompactRange_HasValidRSpan()
        {
            var world = new SimulationWorld();
            LoadCh01Graph(world);
            Ch01HexPrototypeMapBuilder.Build(world);

            HostHexWorldRenderer.ComputeWorldBounds(
                world.HexWorld,
                out var minX,
                out var maxX,
                out var minY,
                out var maxY);

            HexWorldMapRenderBounds.ComputeVisibleCompactRange(
                world.HexWorld,
                minX,
                maxX,
                minY,
                maxY,
                world.HexWorld.HexSize * 1.2f,
                out _,
                out _,
                out var rMin,
                out var rMax);

            Assert.LessOrEqual(rMin, rMax);
            Assert.GreaterOrEqual(rMin, 0);
            Assert.Less(rMax, world.HexWorld.Height);
        }

        static void LoadCh01Graph(SimulationWorld world)
        {
            
            
            }
    }
}
