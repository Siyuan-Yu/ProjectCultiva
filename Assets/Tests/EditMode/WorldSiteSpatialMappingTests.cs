using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests.EditMode
{
    /// <summary>
    /// Phase 5R-B1：WorldSiteSpatialMapping 纯 Core 数学测试（EditMode / 无运行时接线）。
    /// 覆盖：单 Hex 中心映射与 roundtrip、规则多 Hex 方向性、不同 LocalMap 尺寸的
    /// normalized 语义、irregular footprint 投影、DerivedPresenceHex 恒 ∈ footprint。
    /// </summary>
    public sealed class WorldSiteSpatialMappingTests
    {
        const float HexSize = HexWorldScale.DefaultHexOuterRadius; // 1f

        static WorldSite MakeSite(params HexCoord[] footprint)
        {
            var site = new WorldSite
            {
                SiteId = "test:site_spatial",
                DisplayName = "Spatial Test",
                AnchorHex = footprint.Length > 0 ? footprint[0] : default(HexCoord),
            };
            site.SetFootprint(footprint);
            return site;
        }

        static WorldSiteSpatialMapping.WorldSiteLocalMapBounds Bounds(
            float cellSize, int width, int height, float originX = 0f, float originY = 0f) =>
            WorldSiteSpatialMapping.WorldSiteLocalMapBounds.FromOriginSize(originX, originY, cellSize, width, height);

        static WorldSiteSpatialMapping.WorldSiteLocalMapBounds Bounds50x50 => Bounds(1f, 50, 50);
        static WorldSiteSpatialMapping.WorldSiteLocalMapBounds Bounds100x80 => Bounds(1f, 100, 80);

        // ---- 1. 单 Hex Site：Local 中心 → footprint world-domain 中心（= hex 中心附近） ----

        [Test]
        public void WSSM_01_SingleHex_LocalCenter_MapsToHexCenterDomain()
        {
            var site = MakeSite(new HexCoord(5, 7));
            HexMath.ToWorldPosition(new HexCoord(5, 7), HexSize, out var cx, out var cy);

            var ok = WorldSiteSpatialMapping.TryLocalToWorldSurface(
                site, Bounds50x50, new WorldVec2(25f, 25f), HexSize, out var world);
            Assert.IsTrue(ok, "local center must map to world");
            Assert.Less(WorldVec2.Distance(world, new WorldVec2(cx, cy)), 0.05f,
                "single-hex footprint domain center must equal hex center");

            Assert.IsTrue(site.OccupiesHex(HexMath.WorldToHex(world.X, world.Y, HexSize)),
                "mapped world must resolve back into the occupied hex");
        }

        [Test]
        public void WSSM_02_SingleHex_LocalWorldRoundtrip_SmallError()
        {
            var site = MakeSite(new HexCoord(5, 7));
            var samples = new[] { new WorldVec2(10f, 10f), new WorldVec2(25f, 25f), new WorldVec2(40f, 30f) };
            foreach (var local in samples)
            {
                Assert.IsTrue(WorldSiteSpatialMapping.TryLocalToWorldSurface(
                    site, Bounds50x50, local, HexSize, out var world), "L2W " + local);
                Assert.IsTrue(WorldSiteSpatialMapping.TryWorldSurfaceToLocal(
                    site, Bounds50x50, world, HexSize, out var back), "W2L " + world);
                Assert.Less(WorldVec2.Distance(local, back), 0.05f,
                    "roundtrip error too large for " + local);
            }
        }

        // ---- 2. 规则 2×2 多 Hex：Local 四象限 ↔ footprint world-domain 方向一致 ----

        [Test]
        public void WSSM_03_MultiHex_LocalQuadrants_PreserveDirection()
        {
            var site = MakeSite(
                new HexCoord(0, 0), new HexCoord(1, 0),
                new HexCoord(0, 1), new HexCoord(1, 1));

            Assert.IsTrue(WorldSiteSpatialMapping.TryLocalToWorldSurface(
                site, Bounds50x50, new WorldVec2(0f, 25f), HexSize, out var left));
            Assert.IsTrue(WorldSiteSpatialMapping.TryLocalToWorldSurface(
                site, Bounds50x50, new WorldVec2(49f, 25f), HexSize, out var right));
            Assert.IsTrue(WorldSiteSpatialMapping.TryLocalToWorldSurface(
                site, Bounds50x50, new WorldVec2(25f, 0f), HexSize, out var top));
            Assert.IsTrue(WorldSiteSpatialMapping.TryLocalToWorldSurface(
                site, Bounds50x50, new WorldVec2(25f, 49f), HexSize, out var bottom));

            Assert.Less(left.X, right.X, "Local 左 → footprint world 左");
            Assert.Less(top.Y, bottom.Y, "Local 上 → footprint world 上");
            Assert.Less(0f, WorldSiteSpatialMapping.TryComputeFootprintWorldDomain(
                site, HexSize, out var minX, out var maxX, out var minY, out var maxY) ? 1 : 0);
            Assert.Less(minX, maxX);
            Assert.Less(minY, maxY);
        }

        // ---- 3. 不同 LocalMap 尺寸：normalized 语义一致（中心恒映射到 footprint 域中心） ----

        [Test]
        public void WSSM_04_DifferentLocalMapSizes_SameNormalizedCenter()
        {
            var site = MakeSite(new HexCoord(2, 3));

            Assert.IsTrue(WorldSiteSpatialMapping.TryLocalToWorldSurface(
                site, Bounds(1f, 50, 50), new WorldVec2(25f, 25f), HexSize, out var worldA));
            Assert.IsTrue(WorldSiteSpatialMapping.TryLocalToWorldSurface(
                site, Bounds(1f, 100, 80), new WorldVec2(50f, 40f), HexSize, out var worldB));

            Assert.Less(WorldVec2.Distance(worldA, worldB), 0.05f,
                "50x50 center and 100x80 center must map to the same footprint center");
        }

        [Test]
        public void WSSM_04b_CellSizeScale_DoesNotChangeNormalizedSemantics()
        {
            var site = MakeSite(new HexCoord(0, 0));
            // 50x50 cell=2 的中心 (50,50) 与 50x50 cell=1 的中心 (25,25) 语义相同。
            Assert.IsTrue(WorldSiteSpatialMapping.TryLocalToWorldSurface(
                site, Bounds(1f, 50, 50), new WorldVec2(25f, 25f), HexSize, out var worldA));
            Assert.IsTrue(WorldSiteSpatialMapping.TryLocalToWorldSurface(
                site, Bounds(2f, 50, 50), new WorldVec2(50f, 50f), HexSize, out var worldB));
            Assert.Less(WorldVec2.Distance(worldA, worldB), 0.05f, "cellSize must not change relative semantics");
        }

        // ---- 4. irregular footprint：包络空洞 candidate → 投影到最近 occupied polygon ----

        [Test]
        public void WSSM_05_IrregularFootprint_ProjectToOccupiedPolygon()
        {
            // 占用 (0,0) 与 (2,0)，中间 (1,0) 是空洞。Local 中心 → bbox 中心 ≈ (1,0) 空洞。
            var site = MakeSite(new HexCoord(0, 0), new HexCoord(2, 0));

            Assert.IsTrue(WorldSiteSpatialMapping.TryLocalToWorldSurface(
                site, Bounds50x50, new WorldVec2(25f, 25f), HexSize, out var world),
                "irregular footprint must still map (via projection)");

            // 不能停留在空洞中心（要真正投影到 footprint polygon 上）。
            Assert.Greater(WorldVec2.Distance(world, new WorldVec2(1.732f, 0f)), 0.1f,
                "candidate must be projected away from the bbox hole");

            Assert.IsTrue(WorldSiteSpatialMapping.TryResolveDerivedFootprintHex(
                site, world, HexSize, out var derived));
            Assert.IsTrue(
                derived == new HexCoord(0, 0) || derived == new HexCoord(2, 0),
                "derived hex must be one of the occupied footprint hexes, got " + derived);

            // 映射后位置应落在某 occupied hex polygon 上（距其中心的距离 < hexSize）。
            HexMath.ToWorldPosition(derived, HexSize, out var dx, out var dy);
            Assert.Less(WorldVec2.Distance(world, new WorldVec2(dx, dy)), HexSize,
                "projected world must lie on/inside an occupied hex polygon");
        }

        // ---- 5. DerivedPresenceHex 恒 ∈ footprint（多采样点） ----

        [Test]
        public void WSSM_06_DerivedHex_AlwaysInsideFootprint()
        {
            var site = MakeSite(
                new HexCoord(0, 0), new HexCoord(1, 0),
                new HexCoord(0, 1), new HexCoord(1, 1));
            var footprint = new HashSet<HexCoord>(site.OccupiedHexes);

            var samples = new[]
            {
                new WorldVec2(0f, 0f), new WorldVec2(0f, 49f),
                new WorldVec2(49f, 0f), new WorldVec2(49f, 49f),
                new WorldVec2(10f, 10f), new WorldVec2(40f, 30f),
                new WorldVec2(25f, 25f),
            };

            foreach (var local in samples)
            {
                Assert.IsTrue(WorldSiteSpatialMapping.TryLocalToWorldSurface(
                    site, Bounds50x50, local, HexSize, out var world), "L2W " + local);
                Assert.IsTrue(WorldSiteSpatialMapping.TryResolveDerivedFootprintHex(
                    site, world, HexSize, out var derived), "derive " + local);
                Assert.IsTrue(footprint.Contains(derived),
                    "DerivedPresenceHex " + derived + " must be inside footprint for local " + local);
            }
        }

        // ---- 6. 无效输入防御 ----

        [Test]
        public void WSSM_07_InvalidInputs_ReturnFalse()
        {
            var site = MakeSite(new HexCoord(0, 0));
            Assert.IsFalse(WorldSiteSpatialMapping.TryLocalToWorldSurface(
                null, Bounds50x50, new WorldVec2(25f, 25f), HexSize, out _));
            Assert.IsFalse(WorldSiteSpatialMapping.TryLocalToWorldSurface(
                site, default(WorldSiteSpatialMapping.WorldSiteLocalMapBounds),
                new WorldVec2(25f, 25f), HexSize, out _));
            Assert.IsFalse(WorldSiteSpatialMapping.TryLocalToWorldSurface(
                site, Bounds50x50, new WorldVec2(25f, 25f), 0f, out _));
            Assert.IsFalse(WorldSiteSpatialMapping.TryResolveDerivedFootprintHex(
                site, new WorldVec2(0f, 0f), 0f, out _));
        }
        // ---- 7. UpdateWorldPositionWithinSite（Phase 5R-B2A，Context-preserving API） ----

        [Test]
        public void WSSM_08_UpdateWithinSite_KeepsContext_UpdatesWorldOnly()
        {
            var motion = new PlayerPartyWorldMotion();
            motion.SetAtWorldSite("test:site_guanai", new HexCoord(12, 11), HexSize);
            var target = new WorldVec2(3.5f, -2.25f);

            Assert.IsTrue(motion.TryUpdateWorldPositionWithinSite("test:site_guanai", target),
                "AtWorldSite 且 SiteId 匹配时必须返回 true");

            Assert.AreEqual(PlayerPartyLocationKind.AtWorldSite, motion.LocationKind,
                "LocationKind 必须保持 AtWorldSite");
            Assert.AreEqual("test:site_guanai", motion.SiteId, "SiteId 必须不变");
            Assert.AreEqual(target, motion.WorldPosition, "WorldPosition 必须更新");
            Assert.AreEqual(new HexCoord(12, 11), motion.CurrentHex,
                "CurrentHex 不得被本 API 改动（职责分类留 5R-C）");
            Assert.IsTrue(motion.HasPosition);
        }

        [Test]
        public void WSSM_09_UpdateWithinSite_WrongSiteId_Rejected()
        {
            var motion = new PlayerPartyWorldMotion();
            motion.SetAtWorldSite("test:site_guanai", new HexCoord(12, 11), HexSize);
            var before = motion.WorldPosition;

            Assert.IsFalse(motion.TryUpdateWorldPositionWithinSite("test:site_lingdi", new WorldVec2(99f, 99f)),
                "expectedSiteId 不匹配必须返回 false");

            Assert.AreEqual(before, motion.WorldPosition, "expectedSiteId 不匹配必须拒绝更新");
            Assert.AreEqual("test:site_guanai", motion.SiteId, "SiteId 必须保持");
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldSite, motion.LocationKind);
        }

        [Test]
        public void WSSM_10_UpdateWithinSite_AtWorldPosition_Rejected()
        {
            var motion = new PlayerPartyWorldMotion();
            motion.SetAtWorldPosition(new WorldVec2(5f, 5f), new HexCoord(0, 0));

            Assert.IsFalse(motion.TryUpdateWorldPositionWithinSite("test:site_guanai", new WorldVec2(99f, 99f)),
                "AtWorldPosition 时禁止 Site 内更新，必须返回 false");

            Assert.AreEqual(new WorldVec2(5f, 5f), motion.WorldPosition,
                "AtWorldPosition 时禁止 Site 内更新");
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldPosition, motion.LocationKind);
        }
    }
}
