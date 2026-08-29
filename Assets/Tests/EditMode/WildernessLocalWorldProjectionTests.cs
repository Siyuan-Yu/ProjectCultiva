using NUnit.Framework;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests.EditMode
{
    /// <summary>
    /// Phase 5R-B3A：Wilderness LocalMap ↔ HexWorld 连续世界表面映射测试。
    /// 验证与 WorldSite 共享 <see cref="HexFootprintSpatialMapping"/>（单 Hex footprint）：
    /// Local 中心 → Hex 中心；Local 边缘 → 真实 Hex polygon boundary；不同 LocalMap 尺寸
    /// normalized 语义一致；roundtrip 误差小；Local 角落经投影后仍位于合法 Hex polygon。
    /// WorldSiteSpatialMapping 原 B1/B1.1 回归见 <see cref="WorldSiteSpatialMappingTests"/>。
    /// 无 per-call 堆分配断言在独立 dotnet 验证工程执行（NUnit 不依赖 GC API，保持 Editor 编译面最小）。
    /// </summary>
    public sealed class WildernessLocalWorldProjectionTests
    {
        const float HexSize = HexWorldScale.DefaultHexOuterRadius; // 1f

        static WildernessLocalWorldProjection.WildernessLocalMapBounds Bounds(
            float cellSize, int width, int height, float originX = 0f, float originY = 0f) =>
            WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(
                originX, originY, cellSize, width, height);

        static WorldVec2 L2W(
            HexCoord hex,
            float localX,
            float localY,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds)
        {
            Assert.IsTrue(WildernessLocalWorldProjection.TryProjectLocalToWorld(
                hex, localX, localY, bounds, HexSize, out var world),
                "L2W must succeed for (" + localX + "," + localY + ")");
            return world;
        }

        static WorldVec2 W2L(
            HexCoord contextHex,
            WorldVec2 world,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds)
        {
            Assert.IsTrue(WildernessLocalWorldProjection.TryProjectWorldToLocal(
                contextHex, world, bounds, HexSize, out var lx, out var ly),
                "W2L must succeed for " + world);
            return new WorldVec2(lx, ly);
        }

        static void AssertOnHexPolygon(HexCoord hex, WorldVec2 world, string label)
        {
            Assert.IsTrue(HexFootprintSpatialMapping.TryResolveFootprintHex(
                hex, world, HexSize, out var derived) && derived == hex,
                label + " must resolve into the hex, derived=" + derived);
            HexMath.ToWorldPosition(hex, HexSize, out var cx, out var cy);
            Assert.LessOrEqual(WorldVec2.Distance(world, new WorldVec2(cx, cy)), HexSize + 0.0001f,
                label + " must stay within the hex circumradius");
        }

        // ---- 1. 单 Hex：Local center → Hex center ----

        [Test]
        public void WSSM_20_Wilderness_SingleHex_LocalCenter_ToHexCenter()
        {
            var hex = new HexCoord(5, 7);
            HexMath.ToWorldPosition(hex, HexSize, out var cx, out var cy);

            var world = L2W(hex, 25f, 25f, Bounds(1f, 50, 50));
            Assert.Less(WorldVec2.Distance(world, new WorldVec2(cx, cy)), 0.05f,
                "50×50 center must map to the hex center");
        }

        // ---- 2. Local 边缘 → 真实 Hex polygon boundary ----

        [Test]
        public void WSSM_21_Wilderness_LocalEdges_ToHexPolygonBoundary()
        {
            var hex = new HexCoord(5, 7);
            HexMath.ToWorldPosition(hex, HexSize, out var cx, out var cy);
            var bounds = Bounds(1f, 50, 50);
            const float edgeX = 0.8660254f; // pointy-top 角点 x 极值（hexSize=1）
            const float edgeY = 1f;         // pointy-top 角点 y 极值（hexSize=1）

            var east = L2W(hex, bounds.MaxX - 0.01f, 25f, bounds);
            Assert.AreEqual(cx + edgeX, east.X, 0.05f, "Local East edge → Hex polygon East boundary");
            Assert.AreEqual(cy, east.Y, 0.05f);

            var west = L2W(hex, bounds.MinX + 0.01f, 25f, bounds);
            Assert.AreEqual(cx - edgeX, west.X, 0.05f, "Local West edge → Hex polygon West boundary");
            Assert.AreEqual(cy, west.Y, 0.05f);

            var north = L2W(hex, 25f, bounds.MinY + 0.01f, bounds);
            Assert.AreEqual(cx, north.X, 0.05f);
            Assert.AreEqual(cy - edgeY, north.Y, 0.05f,
                "Local 上(localY=MinY) → Hex polygon 北界（cy - edgeY，世界 +Y 朝下）");

            var south = L2W(hex, 25f, bounds.MaxY - 0.01f, bounds);
            Assert.AreEqual(cx, south.X, 0.05f);
            Assert.AreEqual(cy + edgeY, south.Y, 0.05f,
                "Local 下(localY=MaxY) → Hex polygon 南界（cy + edgeY）");
        }

        // ---- 3. 不同 LocalMap 尺寸：normalized 语义一致 ----

        [Test]
        public void WSSM_22_Wilderness_DifferentLocalMapSizes_SameNormalized()
        {
            var hex = new HexCoord(2, 3);
            HexMath.ToWorldPosition(hex, HexSize, out var cx, out var cy);

            var a = L2W(hex, 20f, 20f, Bounds(1f, 40, 40));   // 40×40 中心
            var b = L2W(hex, 25f, 25f, Bounds(1f, 50, 50));   // 50×50 中心
            var c = L2W(hex, 50f, 40f, Bounds(1f, 100, 80));  // 100×80 中心

            Assert.Less(WorldVec2.Distance(a, new WorldVec2(cx, cy)), 0.05f);
            Assert.Less(WorldVec2.Distance(b, new WorldVec2(cx, cy)), 0.05f);
            Assert.Less(WorldVec2.Distance(c, new WorldVec2(cx, cy)), 0.05f);
            Assert.Less(WorldVec2.Distance(a, b), 0.05f, "40×40 与 50×50 中心必须同语义");
            Assert.Less(WorldVec2.Distance(b, c), 0.05f, "50×50 与 100×80 中心必须同语义");
        }

        // ---- 4. Local → World → Local roundtrip（内部点，误差应极小） ----

        [Test]
        public void WSSM_23_Wilderness_LocalWorldLocal_Roundtrip_SmallError()
        {
            var hex = new HexCoord(5, 7);
            var bounds = Bounds(1f, 50, 50);
            var samples = new[]
            {
                new WorldVec2(25f, 25f), new WorldVec2(22f, 25f), new WorldVec2(25f, 22f),
                new WorldVec2(28f, 28f), new WorldVec2(20f, 30f), new WorldVec2(23f, 27f),
            };

            foreach (var local in samples)
            {
                var world = L2W(hex, local.X, local.Y, bounds);
                var back = W2L(hex, world, bounds);
                Assert.Less(WorldVec2.Distance(local, back), 0.05f,
                    "interior roundtrip error too large for " + local + " -> " + back);
            }
        }

        // ---- 5. World → Local → World roundtrip（boundary / vertex / interior） ----

        [Test]
        public void WSSM_24_Wilderness_WorldLocalWorld_Roundtrip()
        {
            var hex = new HexCoord(5, 7);
            HexMath.ToWorldPosition(hex, HexSize, out var cx, out var cy);
            var bounds = Bounds(1f, 50, 50);
            var samples = new[]
            {
                new WorldVec2(cx, cy),                 // 中心
                new WorldVec2(cx + 0.866f, cy),        // East boundary 中点
                new WorldVec2(cx - 0.866f, cy),        // West boundary 中点
                new WorldVec2(cx, cy + 1f),            // North vertex
                new WorldVec2(cx, cy - 1f),            // South vertex
                new WorldVec2(cx + 0.3f, cy - 0.4f),   // 内部
            };

            foreach (var world in samples)
            {
                var local = W2L(hex, world, bounds);
                var back = L2W(hex, local.X, local.Y, bounds);
                Assert.Less(WorldVec2.Distance(world, back), 0.15f,
                    "W2L→L2W error too large for " + world + " -> " + back);
                AssertOnHexPolygon(hex, back, "W2L→L2W(" + world + ")");
            }
        }

        // ---- 6. Local 角落 / 边缘点：投影后仍位于合法 Hex polygon ----

        [Test]
        public void WSSM_25_Wilderness_LocalCorners_StayOnHexPolygon()
        {
            var hex = new HexCoord(5, 7);
            var bounds = Bounds(1f, 50, 50);
            var corners = new[]
            {
                new WorldVec2(0f, 0f), new WorldVec2(49f, 0f),
                new WorldVec2(0f, 49f), new WorldVec2(49f, 49f),
                new WorldVec2(0f, 25f), new WorldVec2(25f, 0f),
                new WorldVec2(49f, 25f), new WorldVec2(25f, 49f),
            };

            foreach (var corner in corners)
            {
                var world = L2W(hex, corner.X, corner.Y, bounds);
                AssertOnHexPolygon(hex, world, "local corner " + corner);
            }
        }
    }
}
