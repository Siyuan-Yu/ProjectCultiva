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

        // ---- 8. Empty footprint：Physical Mapping 必须明确失败（Anchor fallback 已移除，ADR-0027） ----

        [Test]
        public void WSSM_11_EmptyFootprint_AllPhysicalMappingFails_EvenWithAnchor()
        {
            // AnchorHex 有效但无合法 footprint（OccupiedHexes 为空；不调用 SetFootprint）。
            // ADR-0027：AnchorHex 不是 Physical Position / Spatial Mapping authority，
            // 即使有效也绝不能回退为 fake single-hex physical domain。
            var site = new WorldSite
            {
                SiteId = "test:site_empty",
                DisplayName = "Empty Test",
                AnchorHex = new HexCoord(3, 4),
                PresenceHex = new HexCoord(3, 4),
            };

            Assert.IsTrue(site.AnchorHex.Equals(new HexCoord(3, 4)), "precondition: anchor must be valid");
            Assert.AreEqual(0, site.OccupiedHexes.Count, "precondition: no legal footprint");

            Assert.IsFalse(
                WorldSiteSpatialMapping.TryLocalToWorldSurface(
                    site, Bounds50x50, new WorldVec2(25f, 25f), HexSize, out _),
                "LocalToWorld 空 footprint 必须 false");
            Assert.IsFalse(
                WorldSiteSpatialMapping.TryLocalToWorldSurface(
                    site, Bounds50x50, new WorldVec2(25f, 25f), out _),
                "LocalToWorld（默认 hexSize 重载）空 footprint 必须 false");
            Assert.IsFalse(
                WorldSiteSpatialMapping.TryWorldSurfaceToLocal(
                    site, Bounds50x50, new WorldVec2(0f, 0f), HexSize, out _),
                "WorldToLocal 空 footprint 必须 false");
            Assert.IsFalse(
                WorldSiteSpatialMapping.TryResolveDerivedFootprintHex(
                    site, new WorldVec2(0f, 0f), HexSize, out _),
                "DerivedFootprintHex 空 footprint 必须 false");
            Assert.IsFalse(
                WorldSiteSpatialMapping.TryComputeFootprintWorldDomain(
                    site, HexSize, out _, out _, out _, out _),
                "ComputeDomain 空 footprint 必须 false");
        }

        // ---- Phase 5R-B3B：Context-preserving Final Arrival API + Boundary Direction Mapping ----

        [Test]
        public void WSSM_12_PreservePosition_AtWorldPosition_To_AtSite_WorldPositionUnchanged()
        {
            var motion = new PlayerPartyWorldMotion();
            var continuous = new WorldVec2(7.4f, -3.1f); // 跨边界连续位置（非任何 hex center）
            motion.SetAtWorldPosition(continuous, new HexCoord(11, 11));

            Assert.IsTrue(
                motion.TrySetAtWorldSitePreservingWorldPosition("test:site_guanai", continuous),
                "AtWorldPosition 有可信 Canonical 时必须成功进入 AtSite");

            Assert.AreEqual(PlayerPartyLocationKind.AtWorldSite, motion.LocationKind,
                "LocationKind 必须切换为 AtWorldSite");
            Assert.AreEqual("test:site_guanai", motion.SiteId, "SiteId 必须写入");
            Assert.AreEqual(continuous, motion.WorldPosition,
                "Physical Position 必须保持不变（Context change 不 snap）");
            Assert.AreEqual(new HexCoord(11, 11), motion.CurrentHex,
                "CurrentHex 不得被本 API 改动（三职责分类留 5R-C）");
            Assert.IsTrue(motion.HasPosition);
        }

        [Test]
        public void WSSM_13_PreservePosition_InvalidArgs_RejectedNoChange()
        {
            var motion = new PlayerPartyWorldMotion();
            motion.SetAtWorldPosition(new WorldVec2(5f, 5f), new HexCoord(0, 0));
            var before = motion.WorldPosition;

            Assert.IsFalse(
                motion.TrySetAtWorldSitePreservingWorldPosition("", new WorldVec2(1f, 1f)),
                "空 siteId 必须拒绝");
            Assert.AreEqual(before, motion.WorldPosition, "拒绝时 WorldPosition 不得改变");
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldPosition, motion.LocationKind);

            Assert.IsFalse(
                motion.TrySetAtWorldSitePreservingWorldPosition(
                    "test:site_guanai", new WorldVec2(float.NaN, 0f)),
                "NaN 位置必须拒绝");
            Assert.AreEqual(before, motion.WorldPosition, "拒绝时 WorldPosition 不得改变");
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldPosition, motion.LocationKind);
        }

        [Test]
        public void WSSM_14_BoundaryWorldPoint_MapsToMatchingLocalSide()
        {
            // 单 Hex Site：Local 西/东/北/南 edge ↔ footprint polygon 西/东/北/南 boundary。
            // （world +Y 朝下：Local 上=MinY ↔ 北 boundary cy-edgeY；Local 下=MaxY ↔ 南 boundary cy+edgeY）
            var site = MakeSite(new HexCoord(5, 7));
            HexMath.ToWorldPosition(new HexCoord(5, 7), HexSize, out var cx, out var cy);
            var edgeX = HexSize * 0.8660254f;
            var edgeY = HexSize;

            Assert.IsTrue(WorldSiteSpatialMapping.TryWorldSurfaceToLocal(
                site, Bounds50x50, new WorldVec2(cx - edgeX, cy), HexSize, out var westLocal), "west W2L");
            Assert.Less(westLocal.X, 0.5f, "West boundary → Local 西侧");

            Assert.IsTrue(WorldSiteSpatialMapping.TryWorldSurfaceToLocal(
                site, Bounds50x50, new WorldVec2(cx + edgeX, cy), HexSize, out var eastLocal), "east W2L");
            Assert.Greater(eastLocal.X, 49.5f, "East boundary → Local 东侧");

            Assert.IsTrue(WorldSiteSpatialMapping.TryWorldSurfaceToLocal(
                site, Bounds50x50, new WorldVec2(cx, cy - edgeY), HexSize, out var northLocal), "north W2L");
            Assert.Less(northLocal.Y, 0.5f, "North boundary → Local 北侧（MinY）");

            Assert.IsTrue(WorldSiteSpatialMapping.TryWorldSurfaceToLocal(
                site, Bounds50x50, new WorldVec2(cx, cy + edgeY), HexSize, out var southLocal), "south W2L");
            Assert.Greater(southLocal.Y, 49.5f, "South boundary → Local 南侧（MaxY）");
        }

        [Test]
        public void WSSM_15_IngressBoundaryRoundtrip_WorldLocalWorld_CloseToOriginal()
        {
            // 进入 Site 后 World→Local→World roundtrip 保持接近同一 boundary position（B3B 验收）。
            var site = MakeSite(new HexCoord(5, 7));
            HexMath.ToWorldPosition(new HexCoord(5, 7), HexSize, out var cx, out var cy);
            var boundary = new WorldVec2(cx - HexSize * 0.8660254f, cy + 0.3f); // 西侧 boundary 附近

            Assert.IsTrue(WorldSiteSpatialMapping.TryWorldSurfaceToLocal(
                site, Bounds50x50, boundary, HexSize, out var local), "ingress W2L");
            Assert.IsTrue(WorldSiteSpatialMapping.TryLocalToWorldSurface(
                site, Bounds50x50, local, HexSize, out var back), "materialize L2W");
            Assert.Less(WorldVec2.Distance(boundary, back), 0.05f,
                "进入 Site 后 roundtrip 必须接近原 boundary position，got " + back);
        }

        // ---- Phase 5R-B3B.1：Formal BoundaryContact Ingress（Wilderness→WorldSite）----

        static SurfaceExitConnection MakeConnection(HexCoord source, HexCoord dest, int dir)
        {
            return new SurfaceExitConnection(
                source,
                dest,
                dir,
                SurfaceExitDestinationKind.WildernessHex,
                string.Empty,
                1f,
                0f,
                0f,
                0f,
                new SurfaceExitCoverageRect(0f, 1f, 0f, 1f),
                (source.Q + dest.Q) * 0.5f,
                (source.R + dest.R) * 0.5f);
        }

        [Test]
        public void WSSM_16_TryMatchIngressConnection_MatchesByCanonicalIdentity()
        {
            // 按 canonical identity（SourceHex==footprint 格 && DestinationHex==外部荒野格）匹配，
            // 不按最近距离 / direction 猜。
            var fp = new HexCoord(5, 7);
            var extWest = new HexCoord(5, 6);
            var extEast = new HexCoord(6, 7);
            var list = new List<SurfaceExitConnection>
            {
                MakeConnection(fp, extWest, 2),
                MakeConnection(fp, extEast, 3),
                MakeConnection(new HexCoord(0, 0), new HexCoord(0, 1), 1),
            };

            Assert.IsTrue(
                WorldSiteFootprintExitConnectionResolver.TryMatchIngressConnection(
                    list, fp, extEast, out var matched),
                "必须按 identity 匹配到 (fp→extEast)");
            Assert.AreEqual(fp, matched.SourceHex, "匹配到的 SourceHex 必须是 footprint 格");
            Assert.AreEqual(extEast, matched.DestinationHex, "匹配到的 DestinationHex 必须是外部格");

            Assert.IsFalse(
                WorldSiteFootprintExitConnectionResolver.TryMatchIngressConnection(
                    list, fp, new HexCoord(9, 9), out _),
                "无匹配 connection 必须 false");
            Assert.IsFalse(
                WorldSiteFootprintExitConnectionResolver.TryMatchIngressConnection(
                    null, fp, extWest, out _),
                "null 列表必须 false");
        }

        [Test]
        public void WSSM_17_FormalIngressConnection_ResolvesBoundaryContact_AllDirections()
        {
            // LocalVisible Wilderness→Site：Physical ingress point == 正式 SurfaceExitConnection
            // BoundaryContactWorld ==（footprint 格中心 + 外部格中心）/2（真实 Hex 共享边中点）。
            var world = new XianXia.Core.Simulation.SimulationWorld();
            world.HexWorld.HexSize = HexSize;
            var fp = new HexCoord(5, 7);
            var site = new WorldSite
            {
                SiteId = "test:site_ingress",
                DisplayName = "Ingress Test",
                AnchorHex = fp,
                PresenceHex = fp,
            };
            site.SetFootprint(new[] { fp });
            world.Strategic.Sites.Register(site);

            HexMath.ToWorldPosition(fp, HexSize, out var fx, out var fy);
            for (var d = 0; d < 6; d++)
            {
                var ext = HexMath.Neighbor(fp, d);
                world.HexWorld.SetTile(new HexCell { Coord = ext });

                Assert.IsTrue(
                    WorldSiteFootprintExitConnectionResolver.TryResolveFormalIngressConnection(
                        world, site, fp, ext, HexSize, out var conn),
                    "方向 " + d + " 必须有正式 ingress connection");

                HexMath.ToWorldPosition(ext, HexSize, out var ex, out var ey);
                Assert.AreEqual(
                    (fx + ex) * 0.5f, conn.BoundaryContactWorldX, 0.001f,
                    "方向 " + d + " BoundaryContactWorldX 必须为 Hex 共享边中点 X");
                Assert.AreEqual(
                    (fy + ey) * 0.5f, conn.BoundaryContactWorldY, 0.001f,
                    "方向 " + d + " BoundaryContactWorldY 必须为 Hex 共享边中点 Y");
            }
        }

        [Test]
        public void WSSM_18_FormalIngressConnection_Fails_WhenNoValidConnection()
        {
            var world = new XianXia.Core.Simulation.SimulationWorld();
            world.HexWorld.HexSize = HexSize;
            var fp = new HexCoord(5, 7);
            var site = new WorldSite
            {
                SiteId = "test:site_ingress_none",
                DisplayName = "Ingress None",
                AnchorHex = fp,
                PresenceHex = fp,
            };
            site.SetFootprint(new[] { fp });
            world.Strategic.Sites.Register(site);

            // footprintHex 不在 site footprint → false
            Assert.IsFalse(
                WorldSiteFootprintExitConnectionResolver.TryResolveFormalIngressConnection(
                    world, site, new HexCoord(0, 0), new HexCoord(0, 1), HexSize, out _),
                "footprintHex 不属于 Site 必须 false");

            // fromWildernessHex ∈ footprint（不是外部格）→ false
            Assert.IsFalse(
                WorldSiteFootprintExitConnectionResolver.TryResolveFormalIngressConnection(
                    world, site, fp, fp, HexSize, out _),
                "fromWildernessHex ∈ footprint 必须 false");

            // 外部格无 tile（不可通行/未知）→ 无 connection → false
            var noTileExt = HexMath.Neighbor(fp, 0);
            Assert.IsFalse(
                WorldSiteFootprintExitConnectionResolver.TryResolveFormalIngressConnection(
                    world, site, fp, noTileExt, HexSize, out _),
                "外部格无 tile 必须 false（不静默回退中心点）");

            // null world → false
            Assert.IsFalse(
                WorldSiteFootprintExitConnectionResolver.TryResolveFormalIngressConnection(
                    null, site, fp, new HexCoord(5, 6), HexSize, out _),
                "null world 必须 false");
        }
    }
}
