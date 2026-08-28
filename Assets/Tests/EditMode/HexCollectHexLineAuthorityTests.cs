using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.World.Hex;

namespace XianXia.Tests
{
    /// <summary>
    /// CollectHexLine Authority：Odd-R → axial lerp → Odd-R；覆盖奇偶行与多方向。
    /// </summary>
    public sealed class HexCollectHexLineAuthorityTests
    {
        [Test]
        public void Horizontal_EvenRowStart_IsContiguousAndReversible()
        {
            AssertLine(new HexCoord(10, 20), new HexCoord(14, 20));
        }

        [Test]
        public void Horizontal_OddRowStart_IsContiguousAndReversible()
        {
            AssertLine(new HexCoord(10, 21), new HexCoord(14, 21));
        }

        [Test]
        public void DiagonalNE_EvenRowStart_IsContiguousAndReversible()
        {
            // 偶数行出发，沿 dir1（NE）走 4 步
            var from = new HexCoord(8, 10);
            var to = Walk(from, direction: 1, steps: 4);
            AssertLine(from, to);
        }

        [Test]
        public void DiagonalNW_OddRowStart_IsContiguousAndReversible()
        {
            // 奇数行出发，沿 dir2（NW）走 4 步
            var from = new HexCoord(12, 11);
            var to = Walk(from, direction: 2, steps: 4);
            AssertLine(from, to);
        }

        [Test]
        public void DiagonalSW_EvenRowStart_IsContiguousAndReversible()
        {
            var from = new HexCoord(15, 16);
            var to = Walk(from, direction: 4, steps: 3);
            AssertLine(from, to);
        }

        [Test]
        public void SameHex_ReturnsSingleCell()
        {
            var hex = new HexCoord(7, 9);
            var line = new List<HexCoord>();
            HexMath.CollectHexLine(hex, hex, line);
            Assert.AreEqual(1, line.Count);
            Assert.AreEqual(hex, line[0]);
        }

        [Test]
        public void NeighborPair_ReturnsExactlyTwoCells()
        {
            var a = new HexCoord(5, 7); // odd
            var b = HexMath.Neighbor(a, 0);
            var line = new List<HexCoord>();
            HexMath.CollectHexLine(a, b, line);
            Assert.AreEqual(2, line.Count);
            Assert.AreEqual(a, line[0]);
            Assert.AreEqual(b, line[1]);
            Assert.AreEqual(1, HexMath.Distance(line[0], line[1]));
        }

        [Test]
        public void DoesNotReturnFormerOffsetLerpArtifacts_OnOddRowHorizontal()
        {
            // 旧实现：对 Odd-R (Q,R) 直接 cube lerp，奇数行水平线会插入非共边格。
            var from = new HexCoord(10, 21);
            var to = new HexCoord(14, 21);
            var line = new List<HexCoord>();
            HexMath.CollectHexLine(from, to, line);

            Assert.AreEqual(HexMath.Distance(from, to) + 1, line.Count);
            for (var i = 1; i < line.Count; i++)
                Assert.AreEqual(1, HexMath.Distance(line[i - 1], line[i]), line[i - 1] + " -> " + line[i]);
        }

        static HexCoord Walk(HexCoord start, int direction, int steps)
        {
            var hex = start;
            for (var i = 0; i < steps; i++)
                hex = HexMath.Neighbor(hex, direction);
            return hex;
        }

        static void AssertLine(HexCoord from, HexCoord to)
        {
            var forward = new List<HexCoord>();
            HexMath.CollectHexLine(from, to, forward);

            Assert.GreaterOrEqual(forward.Count, 1);
            Assert.AreEqual(from, forward[0], "line start");
            Assert.AreEqual(to, forward[forward.Count - 1], "line end");
            Assert.AreEqual(
                HexMath.Distance(from, to) + 1,
                forward.Count,
                "Count must equal Distance+1");

            for (var i = 1; i < forward.Count; i++)
            {
                Assert.AreEqual(
                    1,
                    HexMath.Distance(forward[i - 1], forward[i]),
                    "consecutive must be DirectNeighbors: " + forward[i - 1] + " -> " + forward[i]);
            }

            var reverse = new List<HexCoord>();
            HexMath.CollectHexLine(to, from, reverse);
            Assert.AreEqual(forward.Count, reverse.Count, "reverse length");
            for (var i = 0; i < forward.Count; i++)
                Assert.AreEqual(forward[i], reverse[forward.Count - 1 - i], "reverse order at " + i);
        }
    }
}
