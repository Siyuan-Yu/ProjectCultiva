using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.World.Hex;

namespace XianXia.Tests
{
    /// <summary>
    /// Hex 邻接 Authority 专项：Odd-R offset 存储 + pointy-top 布局下，
    /// Neighbor / Distance / World 映射必须自洽；覆盖奇偶行。
    /// </summary>
    public sealed class HexAdjacencyAuthorityTests
    {
        const float HexSize = 1f;

        [Test]
        public void Layout_IsOddROffsetPointyTop()
        {
            Assert.AreEqual(HexOrientation.PointyTop, HexMetrics.Orientation);

            // Odd-R：奇数行中心相对偶数行右移半个水平间距
            HexMath.ToWorldPosition(new HexCoord(0, 0), HexSize, out var x0, out var y0);
            HexMath.ToWorldPosition(new HexCoord(0, 1), HexSize, out var x1, out var y1);
            Assert.AreEqual(0f, x0, 0.0001f);
            Assert.AreEqual(0f, y0, 0.0001f);
            Assert.AreEqual((float)System.Math.Sqrt(3) * 0.5f, x1, 0.0001f);
            Assert.AreEqual(1.5f, y1, 0.0001f);
        }

        [Test]
        public void EvenRow_SixNeighbors_AreWorldEdgeAdjacent_AndDistanceOne()
        {
            AssertNeighborRing(new HexCoord(10, 20)); // R even
        }

        [Test]
        public void OddRow_SixNeighbors_AreWorldEdgeAdjacent_AndDistanceOne()
        {
            AssertNeighborRing(new HexCoord(10, 21)); // R odd
        }

        [Test]
        public void OddRow_DoesNotIncludeFormerAxialFalseUpperLeftNeighbor()
        {
            // 根因复现：对 Odd-R 存储直接加 axial delta 时，奇数行会把 (q-1,r+1)
            // 误当成邻居；该格在 +R=+Y 的地图上落在左上方向，且世界距离 ≠ √3。
            var center = new HexCoord(10, 21);
            var falseUpperLeft = new HexCoord(center.Q - 1, center.R + 1);

            var neighbors = new List<HexCoord>(6);
            HexMath.CollectNeighbors(center, neighbors);

            CollectionAssert.DoesNotContain(neighbors, falseUpperLeft);
            Assert.AreEqual(2, HexMath.Distance(center, falseUpperLeft));
            Assert.IsFalse(HexMath.AreWorldEdgeAdjacent(center, falseUpperLeft, HexSize));
        }

        [Test]
        public void EvenRow_DoesNotIncludeFormerAxialFalseUpperRightNeighbor()
        {
            var center = new HexCoord(10, 20);
            var falseUpperRight = new HexCoord(center.Q + 1, center.R - 1);

            var neighbors = new List<HexCoord>(6);
            HexMath.CollectNeighbors(center, neighbors);

            CollectionAssert.DoesNotContain(neighbors, falseUpperRight);
            Assert.AreEqual(2, HexMath.Distance(center, falseUpperRight));
            Assert.IsFalse(HexMath.AreWorldEdgeAdjacent(center, falseUpperRight, HexSize));
        }

        [Test]
        public void DistanceOne_Iff_InNeighborSet_ForBothParities()
        {
            AssertDistanceNeighborBijection(new HexCoord(12, 18));
            AssertDistanceNeighborBijection(new HexCoord(12, 19));
        }

        [Test]
        public void WorldRoundTrip_Centers_EvenAndOddRows()
        {
            for (var r = 0; r < 8; r++)
            {
                for (var q = 0; q < 8; q++)
                {
                    var coord = new HexCoord(q, r);
                    Assert.IsTrue(
                        HexMetrics.ValidateCenterRoundTrip(coord, HexSize, out var back),
                        "round-trip failed " + coord + " -> " + back);
                }
            }
        }

        [Test]
        public void SupportArea_SingleBattleHex_OnlyIncludesEdgeAdjacentRing()
        {
            AssertSupportRing(new HexCoord(8, 10));
            AssertSupportRing(new HexCoord(8, 11));
        }

        static void AssertNeighborRing(HexCoord center)
        {
            var neighbors = new List<HexCoord>(6);
            HexMath.CollectNeighbors(center, neighbors);
            Assert.AreEqual(6, neighbors.Count, "center=" + center);

            var set = new HashSet<HexCoord>(neighbors);
            Assert.AreEqual(6, set.Count, "duplicate neighbors at " + center);

            for (var i = 0; i < neighbors.Count; i++)
            {
                var n = neighbors[i];
                Assert.AreEqual(1, HexMath.Distance(center, n), "Distance " + center + " -> " + n);
                Assert.IsTrue(
                    HexMath.AreWorldEdgeAdjacent(center, n, HexSize),
                    "World edge adjacency failed " + center + " -> " + n);
            }
        }

        static void AssertDistanceNeighborBijection(HexCoord center)
        {
            var neighbors = new List<HexCoord>(6);
            HexMath.CollectNeighbors(center, neighbors);
            var neighborSet = new HashSet<HexCoord>(neighbors);

            for (var dq = -3; dq <= 3; dq++)
            {
                for (var dr = -3; dr <= 3; dr++)
                {
                    var candidate = new HexCoord(center.Q + dq, center.R + dr);
                    if (candidate.Equals(center))
                        continue;

                    var dist = HexMath.Distance(center, candidate);
                    var inNeighbors = neighborSet.Contains(candidate);
                    if (dist == 1)
                    {
                        Assert.IsTrue(inNeighbors, candidate + " Distance==1 but not Neighbor of " + center);
                        Assert.IsTrue(HexMath.AreWorldEdgeAdjacent(center, candidate, HexSize));
                    }
                    else
                    {
                        Assert.IsFalse(inNeighbors, candidate + " is Neighbor but Distance=" + dist);
                    }
                }
            }
        }

        static void AssertSupportRing(HexCoord battleHex)
        {
            var support = new HashSet<HexCoord> { battleHex };
            for (var d = 0; d < HexMath.DirectionCount; d++)
                support.Add(HexMath.Neighbor(battleHex, d));

            Assert.AreEqual(7, support.Count);
            foreach (var hex in support)
            {
                if (hex.Equals(battleHex))
                    continue;
                Assert.AreEqual(1, HexMath.Distance(battleHex, hex));
                Assert.IsTrue(HexMath.AreWorldEdgeAdjacent(battleHex, hex, HexSize));
            }

            // 距离 2 的格不得进入 Support
            for (var d = 0; d < 6; d++)
            {
                var ring1 = HexMath.Neighbor(battleHex, d);
                for (var e = 0; e < 6; e++)
                {
                    var ring2 = HexMath.Neighbor(ring1, e);
                    if (HexMath.Distance(battleHex, ring2) != 2)
                        continue;
                    Assert.IsFalse(support.Contains(ring2), "dist-2 " + ring2 + " leaked into support of " + battleHex);
                }
            }
        }
    }
}
