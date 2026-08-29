using System;
using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests.EditMode
{
    /// <summary>
    /// Phase 5R-B3B.2：Wilderness Reopen Context Integrity。
    /// 覆盖「正式 Wilderness Context 由 Context/Transition authority 提交，不由连续位置在
    /// Hex 边界中点反推」的守卫语义：
    ///  - B3A 把 LocalMap 边缘映射到真实 Hex polygon 边界中点（WorldToHex 的 Math.Round 平局点，
    ///    会按列/行奇偶与浮点噪声翻到邻格）；
    ///  - <see cref="WildernessLocalWorldProjection.ResolveAuthoritativeWildernessHex"/> 在该
    ///    歧义区保持已提交 Context，仅远程漂移时采纳派生格自愈。
    /// 验证链见 ADR-0027 / Phase 5R-B3B.2 汇报。
    /// </summary>
    public sealed class WildernessContextAuthorityTests
    {
        const float HexSize = HexWorldScale.DefaultHexOuterRadius; // 1f

        static WorldVec2 Midpoint(HexCoord a, HexCoord b)
        {
            HexMath.ToWorldPosition(a, HexSize, out var ax, out var ay);
            HexMath.ToWorldPosition(b, HexSize, out var bx, out var by);
            return new WorldVec2((ax + bx) * 0.5f, (ay + by) * 0.5f);
        }

        static bool IsNeighbor(HexCoord a, HexCoord b)
        {
            for (var i = 0; i < 6; i++)
            {
                if (HexMath.Neighbor(a, i).Equals(b))
                    return true;
            }

            return false;
        }

        // ---- 1. 边界中点 = WorldToHex 平局点（歧义存在性；这也是必须由 authority 提交 Context 的原因）----

        [Test]
        public void WCA_01_EdgeMidpoint_IsWorldToHexTie()
        {
            // 东 / 西 / 北 三个方向共享边中点：WorldToHex 必须落在 committed 或邻格（可能翻到邻格）。
            var committed = new HexCoord(5, 7);
            foreach (var dir in new[] { 1, 2, 4 })
            {
                var neighbor = HexMath.Neighbor(committed, dir);
                var mid = Midpoint(committed, neighbor);
                var derived = HexMath.WorldToHex(mid.X, mid.Y, HexSize);
                Assert.IsTrue(
                    derived.Equals(committed) || IsNeighbor(committed, derived),
                    "dir=" + dir + " boundary midpoint must resolve to committed or a neighbor, got " + derived);
            }
        }

        [Test]
        public void WCA_02_LocalMapEdge_MapsToBoundaryMidpoint_WhichIsTheAmbiguitySource()
        {
            // B3A：LocalMap 边缘 → 真实 Hex polygon 边界中点（East x = cx + 0.866·hexSize）。
            // 该点正是 WCA_01 的平局区——守卫必须在这一区保持 committed。
            var hex = new HexCoord(5, 7);
            var bounds = WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(
                0f, 0f, 1f, 50, 50);
            Assert.IsTrue(WildernessLocalWorldProjection.TryProjectLocalToWorld(
                hex, 49.99f, 25f, bounds, HexSize, out var eastEdge));
            HexMath.ToWorldPosition(hex, HexSize, out var cx, out _);
            Assert.Less(Math.Abs(eastEdge.X - (cx + HexSize * 0.8660254f)), 0.05f,
                "Local East edge must map to real polygon East boundary midpoint");

            var derived = HexMath.WorldToHex(eastEdge.X, eastEdge.Y, HexSize);
            Assert.IsTrue(derived.Equals(hex) || IsNeighbor(hex, derived),
                "East boundary midpoint must be ambiguous (committed or neighbor), got " + derived);
        }

        // ---- 2. 守卫：已提交 Context 不被边界中点翻转 ----

        [Test]
        public void WCA_03_Guard_KeepsCommitted_AtAllEdgeMidpoints()
        {
            var committed = new HexCoord(5, 7);
            foreach (var dir in new[] { 0, 1, 2, 3, 4, 5 })
            {
                var neighbor = HexMath.Neighbor(committed, dir);
                var mid = Midpoint(committed, neighbor);
                var authoritative = WildernessLocalWorldProjection.ResolveAuthoritativeWildernessHex(
                    committed, mid, HexSize);
                Assert.AreEqual(committed, authoritative,
                    "dir=" + dir + " edge midpoint must keep committed context, got " + authoritative);
            }
        }

        [Test]
        public void WCA_04_Guard_KeepsCommitted_Interior()
        {
            var committed = new HexCoord(5, 7);
            HexMath.ToWorldPosition(committed, HexSize, out var cx, out var cy);
            Assert.AreEqual(
                committed,
                WildernessLocalWorldProjection.ResolveAuthoritativeWildernessHex(
                    committed, new WorldVec2(cx + 0.3f, cy - 0.2f), HexSize));
        }

        // ---- 3. 正式跨格后 committed = B：守卫保持 B ----

        [Test]
        public void WCA_05_Guard_CommittedB_KeepsB_AfterFormalCross()
        {
            var b = new HexCoord(6, 7);
            HexMath.ToWorldPosition(b, HexSize, out var bx, out var by);
            Assert.AreEqual(
                b,
                WildernessLocalWorldProjection.ResolveAuthoritativeWildernessHex(
                    b, new WorldVec2(bx - 0.4f, by), HexSize));
        }

        // ---- 4. 远程漂移（异常，非边界歧义）：采纳派生格自愈 ----

        [Test]
        public void WCA_06_Guard_FarDrift_AdoptsDerived()
        {
            var a = new HexCoord(5, 7);
            var far = new HexCoord(8, 7); // 非邻格（+3 east）
            HexMath.ToWorldPosition(far, HexSize, out var fx, out var fy);
            Assert.AreEqual(
                far,
                WildernessLocalWorldProjection.ResolveAuthoritativeWildernessHex(
                    a, new WorldVec2(fx, fy), HexSize));
        }
    }
}
