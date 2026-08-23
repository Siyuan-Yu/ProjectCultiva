using NUnit.Framework;
using XianXia.Core.World.Hex;

namespace XianXia.Tests.EditMode
{
    /// <summary>Runtime Hex layout contract — Editor Shared HexWorldLayoutShared must mirror these formulas.</summary>
    public sealed class HexWorldWysiwygLayoutTests
    {
        const float HexSize = 1f;

        [Test]
        public void WYSIWYG_02_All100x50Cells_CenterRoundTrip()
        {
            for (var r = 0; r < 50; r++)
            {
                for (var q = 0; q < 100; q++)
                {
                    var coord = new HexCoord(q, r);
                    Assert.IsTrue(
                        HexMetrics.ValidateCenterRoundTrip(coord, HexSize, out var back),
                        "round-trip failed at " + coord + " -> " + back);
                }
            }
        }

        [Test]
        public void WYSIWYG_03_GoldenProbeCoords_MatchOddROffsetFormula()
        {
            AssertWorldCenter(new HexCoord(0, 0), 0f, 0f);
            AssertWorldCenter(new HexCoord(1, 0), HorizontalPitch(), 0f);
            AssertWorldCenter(new HexCoord(0, 1), HorizontalPitch() * 0.5f, VerticalPitch());
            AssertWorldCenter(new HexCoord(1, 1), HorizontalPitch() * 1.5f, VerticalPitch());
            AssertWorldCenter(new HexCoord(50, 25), HorizontalPitch() * (50 + 0.5f), VerticalPitch() * 25f);
        }

        [Test]
        public void WYSIWYG_04_NeighborDistance_IsOneForAxialDirections()
        {
            var origin = new HexCoord(40, 20);
            for (var d = 0; d < HexMath.AxialDirections.Length; d++)
            {
                var neighbor = HexMath.Neighbor(origin, d);
                Assert.AreEqual(1, HexMath.Distance(origin, neighbor), "dir " + d);
            }
        }

        static void AssertWorldCenter(HexCoord coord, float expectedX, float expectedY)
        {
            HexWorldLayout.CoordToWorldCenter(coord, HexSize, out var wx, out var wy);
            Assert.AreEqual(expectedX, wx, 0.0001f, coord + ".x");
            Assert.AreEqual(expectedY, wy, 0.0001f, coord + ".y");
        }

        static float HorizontalPitch() => (float)System.Math.Sqrt(3) * HexSize;

        static float VerticalPitch() => 1.5f * HexSize;
    }
}
