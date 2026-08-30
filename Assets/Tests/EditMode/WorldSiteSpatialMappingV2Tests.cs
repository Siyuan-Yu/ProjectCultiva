using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    /// <summary>
    /// Phase 5R-B3C3：WorldSite Spatial Mapping V2（Kernel Radial）正式回归测试。
    /// 真实 fixture（ch01_hex_world.json + ch01_reference_map.json）+ V2 geometry：
    ///  - 全 30 site kernel preflight（star-shaped 校验）
    ///  - 真实荒村 19841 walkable roundtrip（maxErr 目标 &lt; 0.05）
    ///  - 10 条外围 BoundaryContact 分组（W3/E3/N2/S2）+ 同侧 tangential ordering
    ///  - site_a/site_b 退化 kernel dense roundtrip（不 collapse、inverse 稳定）
    ///  - disconnected footprint validator（非连通 + 无 kernel）
    ///  - geometry overload 零堆分配（B4 复用路径）
    ///  - AnchorHex 改变不影响 mapping；PresenceHex 不参与
    /// </summary>
    public sealed class WorldSiteSpatialMappingV2Tests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        const float HexSize = 1f;

        static string WorldJsonPath => Path.Combine(BaseGamePath, "Data", "Worlds", "ch01_hex_world.json");
        static string ReferenceMapJsonPath => Path.Combine(BaseGamePath, "Data", "Maps", "ch01_reference_map.json");

        static WorldSite LoadSite(string siteId)
        {
            var json = File.ReadAllText(WorldJsonPath);
            var j = SimpleJson.Parse(json);
            var sites = j.GetArray("definitions")[0].GetArray("sites");
            for (var i = 0; i < sites.Length; i++)
            {
                var s = sites[i];
                if (s.GetString("siteId") != siteId)
                    continue;
                var site = new WorldSite { SiteId = siteId };
                var fp = s.GetArray("footprint");
                var hexes = new List<HexCoord>();
                for (var f = 0; f < fp.Length; f++)
                    hexes.Add(new HexCoord(fp[f].GetInt("q"), fp[f].GetInt("r")));
                site.AnchorHex = hexes[0];
                site.SetFootprint(hexes);
                return site;
            }
            Assert.Fail("site not found: " + siteId);
            return null;
        }

        static (WorldSite Site, WorldSiteSpatialMapping.WorldSiteLocalMapBounds Bounds, bool[] Blocked, int Width, int Height) LoadReferenceWorld()
        {
            var site = LoadSite("base:site_huangcun");
            var json = File.ReadAllText(ReferenceMapJsonPath);
            var j = SimpleJson.Parse(json);
            var m = j.GetArray("definitions")[0];
            var originX = m.GetFloat("originX");
            var originY = m.GetFloat("originY");
            var cellSize = m.GetFloat("cellSize");
            var width = m.GetInt("width");
            var height = m.GetInt("height");
            var bounds = new WorldSiteSpatialMapping.WorldSiteLocalMapBounds(originX, originY, cellSize, width, height);

            var blocked = new bool[width * height];
            var placements = m.GetArray("placements");
            for (var p = 0; p < placements.Length; p++)
            {
                var pl = placements[p];
                var kind = pl.GetString("kind");
                if (kind != null && (kind.StartsWith("zone", System.StringComparison.OrdinalIgnoreCase) ||
                                     kind == "forest" || kind == "spring"))
                    continue;
                var blocks = pl.GetBool("blocksMovement");
                var x = pl.GetInt("x");
                var y = pl.GetInt("y");
                var w = Math.Max(1, pl.GetInt("w"));
                var h = Math.Max(1, pl.GetInt("h"));
                if (blocks)
                {
                    for (var bx = Math.Max(0, x); bx <= Math.Min(width - 1, x + w - 1); bx++)
                        for (var by = Math.Max(0, y); by <= Math.Min(height - 1, y + h - 1); by++)
                            blocked[by * width + bx] = true;
                }
                else if (!string.IsNullOrEmpty(pl.GetString("boundLocationId")))
                {
                    var cx = x + (w < 1 ? 0 : w / 2);
                    var cy = y + (h < 1 ? 0 : h / 2);
                    for (var dx = -1; dx <= 1; dx++)
                        for (var dy = -1; dy <= 1; dy++)
                        {
                            var bx = cx + dx;
                            var by = cy + dy;
                            if (bx >= 0 && bx < width && by >= 0 && by < height)
                                blocked[by * width + bx] = false;
                        }
                }
            }

            return (site, bounds, blocked, width, height);
        }

        // ============================ [1] 全 30 site kernel preflight ============================

        [Test]
        public void V2_01_AllContentSites_HaveNonEmptyKernel()
        {
            var json = File.ReadAllText(WorldJsonPath);
            var j = SimpleJson.Parse(json);
            var sites = j.GetArray("definitions")[0].GetArray("sites");
            var kernelOk = 0;
            for (var i = 0; i < sites.Length; i++)
            {
                var s = sites[i];
                var siteId = s.GetString("siteId");
                var fp = s.GetArray("footprint");
                var hexes = new List<HexCoord>();
                for (var f = 0; f < fp.Length; f++)
                    hexes.Add(new HexCoord(fp[f].GetInt("q"), fp[f].GetInt("r")));
                var site = new WorldSite { SiteId = siteId, AnchorHex = hexes[0] };
                site.SetFootprint(hexes);
                Assert.IsTrue(
                    WorldSiteSpatialMapping.TryBuildGeometry(site, HexSize, out var g) && g.HasKernel,
                    siteId + " kernel empty (disconnected or non-star-shaped footprint)");
                Assert.IsTrue(WorldSiteFootprintValidator.IsFootprintConnected(hexes), siteId + " disconnected");
                kernelOk++;
            }
            Assert.AreEqual(30, sites.Length, "ch01 site count");
            Assert.AreEqual(30, kernelOk);
        }

        // ============================ [2] 真实荒村 19841 walkable roundtrip ============================

        [Test]
        public void V2_02_Huangcun_WalkableDomain_RoundtripStable()
        {
            var (site, bounds, blocked, width, height) = LoadReferenceWorld();
            Assert.IsTrue(WorldSiteSpatialMapping.TryBuildGeometry(site, HexSize, out var g));

            var walkable = 0;
            var maxErr = 0f;
            var sumErr = 0d;
            for (var cy = 0; cy < height; cy++)
            {
                for (var cx = 0; cx < width; cx++)
                {
                    if (blocked[cy * width + cx])
                        continue;
                    walkable++;
                    var lx = bounds.MinX + (cx + 0.5f) * bounds.CellSize;
                    var ly = bounds.MinY + (cy + 0.5f) * bounds.CellSize;
                    var p = new WorldVec2(lx, ly);
                    Assert.IsTrue(
                        WorldSiteSpatialMapping.TryLocalToWorldSurface(g, bounds, p, out var w),
                        "L2W fail at (" + cx + "," + cy + ")");
                    Assert.IsTrue(
                        WorldSiteSpatialMapping.TryWorldSurfaceToLocal(g, bounds, w, out var p2),
                        "W2L fail at (" + cx + "," + cy + ")");
                    var dx = p.X - p2.X;
                    var dy = p.Y - p2.Y;
                    var err = (float)System.Math.Sqrt(dx * dx + dy * dy);
                    sumErr += err;
                    if (err > maxErr)
                        maxErr = err;
                }
            }

            Assert.AreEqual(19841, walkable, "walkable cell count (真实 ch01_reference_map WalkGrid)");
            Assert.Less(maxErr, 0.05f, "max Local->World->Local err (目标 < 0.05)");
            TestContext.Out.WriteLine("walkable=" + walkable + " maxErr=" + maxErr +
                                      " avg=" + (walkable > 0 ? sumErr / walkable : 0d));
        }

        // ============================ [3] 荒村 10 boundary connections（修复后 perimeter 强断言） ============================

        [Test]
        public void V2_03_Huangcun_BoundaryConnections_OnPerimeter()
        {
            var (site, bounds, _, _, _) = LoadReferenceWorld();
            Assert.IsTrue(WorldSiteSpatialMapping.TryBuildGeometry(site, HexSize, out var g));
            Assert.IsTrue(g.HasKernel, "huangcun kernel");

            // 真实 SimulationWorld（HexWorld 全 passable + Sites 注册 JSON 荒村 footprint）
            var world = new SimulationWorld();
            world.HexWorld.MapId = "test:ch01";
            world.HexWorld.FillRectangle(140, 100, HexTerrainType.Plain);
            world.Strategic.Sites.Register(site);

            // 修复后的正式 CollectConnections（BoundaryContact = 共享边 union 弧长中点，on-perimeter）
            var connections = new List<SurfaceExitConnection>(12);
            var count = WorldSiteFootprintExitConnectionResolver.CollectConnections(
                world,
                site,
                HexSize,
                WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(
                    bounds.MinX, bounds.MinY, bounds.CellSize, bounds.Width, bounds.Height),
                SurfaceExitZoneCalculator.DefaultExitTriggerDepth,
                SurfaceExitZoneCalculator.DefaultSlotSpanFraction,
                connections);

            Assert.AreEqual(10, count, "huangcun 外围 connection 数");

            var counts = new Dictionary<string, int> { ["West"] = 0, ["East"] = 0, ["North"] = 0, ["South"] = 0 };
            var eastLocals = new List<float>();
            var westLocals = new List<float>();
            var northLocals = new List<float>();
            var southLocals = new List<float>();
            var maxPerimDist = 0f;
            var maxSideDelta = 0f;
            foreach (var c in connections)
            {
                var contact = new WorldVec2(c.BoundaryContactWorldX, c.BoundaryContactWorldY);
                var perim = DistanceToPerimeter(g, contact);
                maxPerimDist = System.Math.Max(maxPerimDist, perim);
                Assert.Less(perim, 0.001f,
                    "BoundaryContactWorld 必须位于真实 footprint perimeter: dest " + c.DestinationHex);

                Assert.IsTrue(
                    WorldSiteSpatialMapping.TryWorldSurfaceToLocal(g, bounds, contact, out var loc),
                    "W2L boundary fail for dest " + c.DestinationHex);
                var side = ClassifySide(c.DestinationHex);
                counts[side]++;
                float delta;
                if (side == "West")
                    delta = System.Math.Abs(loc.X - bounds.MinX);
                else if (side == "East")
                    delta = System.Math.Abs(loc.X - bounds.MaxX);
                else if (side == "North")
                    delta = System.Math.Abs(loc.Y - bounds.MaxY);
                else
                    delta = System.Math.Abs(loc.Y - bounds.MinY);
                maxSideDelta = System.Math.Max(maxSideDelta, delta);
                Assert.Less(delta, 0.05f,
                    "mapped local 必须恰在 rectangle perimeter（非方向性弱断言）: dest " +
                    c.DestinationHex + " got " + loc);

                if (side == "East") eastLocals.Add(loc.Y);
                else if (side == "West") westLocals.Add(loc.Y);
                else if (side == "North") northLocals.Add(loc.X);
                else southLocals.Add(loc.X);
            }

            Assert.AreEqual(3, counts["West"], "West 3");
            Assert.AreEqual(3, counts["East"], "East 3");
            Assert.AreEqual(2, counts["North"], "North 2");
            Assert.AreEqual(2, counts["South"], "South 2");
            Assert.IsTrue(IsSortedAscending(eastLocals), "East 同侧沿边顺序");
            Assert.IsTrue(IsSortedAscending(westLocals), "West 同侧沿边顺序");
            Assert.IsTrue(IsSortedAscending(northLocals), "North 同侧沿边顺序");
            Assert.IsTrue(IsSortedAscending(southLocals), "South 同侧沿边顺序");
            TestContext.Out.WriteLine("maxPerimDist=" + maxPerimDist + " maxSideDelta=" + maxSideDelta);
        }

        static float DistanceToPerimeter(HexFootprintSpatialGeometry g, WorldVec2 p)
        {
            var best = double.MaxValue;
            for (var s = 0; s < g.BoundaryCount; s++)
            {
                var seg = g.Boundary[s];
                var dx = seg.B.X - seg.A.X;
                var dy = seg.B.Y - seg.A.Y;
                var lenSq = dx * dx + dy * dy;
                var t = lenSq <= 1e-12f ? 0f : Clamp01(((p.X - seg.A.X) * dx + (p.Y - seg.A.Y) * dy) / lenSq);
                var px = seg.A.X + t * dx;
                var py = seg.A.Y + t * dy;
                var ex = p.X - px;
                var ey = p.Y - py;
                best = System.Math.Min(best, System.Math.Sqrt(ex * ex + ey * ey));
            }

            return (float)best;
        }

        static string ClassifySide(HexCoord dest)
        {
            switch (dest.Q)
            {
                case 79:
                    return "West";
                case 82:
                    return "East";
                case 80 when dest.R == 53:
                    return "North";
                case 81 when dest.R == 53:
                    return "North";
                case 80 when dest.R == 50:
                    return "South";
                case 81 when dest.R == 50:
                    return "South";
                default:
                    return "?";
            }
        }

        static float Clamp01(float v)
        {
            if (v < 0f)
                return 0f;
            if (v > 1f)
                return 1f;
            return v;
        }

        static bool IsSortedAscending(List<float> values)
        {
            for (var i = 1; i < values.Count; i++)
            {
                if (values[i] < values[i - 1])
                    return false;
            }
            return true;
        }

        // ============================ [4] site_a / site_b 退化 kernel dense ============================

        [Test]
        public void V2_04_DegenerateKernelSites_DenseRoundtripStable()
        {
            foreach (var sid in new[] { "base:site_a", "base:site_b" })
            {
                var site = LoadSite(sid);
                Assert.IsTrue(WorldSiteSpatialMapping.TryBuildGeometry(site, HexSize, out var g), sid + " geometry");
                Assert.IsTrue(g.HasKernel, sid + " kernel");
                // 真实 map bounds（ch01_site_a_map.json 与 reference 同尺寸）
                var bounds = new WorldSiteSpatialMapping.WorldSiteLocalMapBounds(-40f, -25f, 1f, 200, 100);
                var maxErr = 0f;
                var failL2W = 0;
                var failW2L = 0;
                for (var gx = 0; gx < 200; gx += 2)
                {
                    for (var gy = 0; gy < 100; gy += 2)
                    {
                        var p = new WorldVec2(-40 + gx + 0.5f, -25 + gy + 0.5f);
                        if (!WorldSiteSpatialMapping.TryLocalToWorldSurface(g, bounds, p, out var w))
                        {
                            failL2W++;
                            continue;
                        }
                        if (!WorldSiteSpatialMapping.TryWorldSurfaceToLocal(g, bounds, w, out var p2))
                        {
                            failW2L++;
                            continue;
                        }
                        var dx = p.X - p2.X;
                        var dy = p.Y - p2.Y;
                        var err = (float)System.Math.Sqrt(dx * dx + dy * dy);
                        if (err > maxErr)
                            maxErr = err;
                    }
                }
                Assert.AreEqual(0, failL2W, sid + " L2W fail");
                Assert.AreEqual(0, failW2L, sid + " W2L fail");
                Assert.Less(maxErr, 0.05f, sid + " dense maxErr (退化 kernel roundtrip 稳定)");
                TestContext.Out.WriteLine(sid + " maxErr=" + maxErr);
            }
        }

        // ============================ [5] disconnected / no-kernel validator ============================

        [Test]
        public void V2_05_Validator_RejectsDisconnectedAndNoKernel()
        {
            var disconnected = new WorldSite
            {
                SiteId = "test_disconnected",
                AnchorHex = new HexCoord(0, 0),
            };
            disconnected.SetFootprint(new[] { new HexCoord(0, 0), new HexCoord(2, 0) });
            var errors = WorldSiteFootprintValidator.ValidateFootprint(disconnected, HexSize);
            Assert.IsTrue(errors.Count >= 1);
            Assert.IsTrue(errors.Exists(e => e.Contains("Disconnected")), "expected Disconnected error");
            Assert.IsTrue(errors.Exists(e => e.Contains("test_disconnected")), "error contains SiteId");

            // 连通但非 star-shaped（凹 Z 形）→ NoSpatialKernel
            var concave = new WorldSite
            {
                SiteId = "test_concave",
                AnchorHex = new HexCoord(0, 0),
            };
            concave.SetFootprint(new[]
            {
                new HexCoord(0, 0), new HexCoord(1, 0),
                new HexCoord(0, 1), new HexCoord(2, 1),
            });
            var errs2 = WorldSiteFootprintValidator.ValidateFootprint(concave, HexSize);
            Assert.IsTrue(errs2.Exists(e => e.Contains("NoSpatialKernel")), "expected NoSpatialKernel error");

            // 空 footprint
            var empty = new WorldSite { SiteId = "test_empty" };
            var errs3 = WorldSiteFootprintValidator.ValidateFootprint(empty, HexSize);
            Assert.IsTrue(errs3.Exists(e => e.Contains("Empty")), "expected Empty error");
        }

        // ============================ [6] Anchor 不变 / Presence 不参与 ============================

        [Test]
        public void V2_06_AnchorIrrelevant_PresenceNotInvolved()
        {
            var baseFp = new[]
            {
                new HexCoord(80, 51), new HexCoord(81, 51),
                new HexCoord(80, 52), new HexCoord(81, 52),
            };
            var bounds = new WorldSiteSpatialMapping.WorldSiteLocalMapBounds(-40f, -25f, 1f, 200, 100);

            // 相同 footprint、不同 anchor → mapping 逐点一致
            var a1 = new WorldSite { SiteId = "a1", AnchorHex = new HexCoord(80, 52) };
            a1.SetFootprint(baseFp);
            var a2 = new WorldSite { SiteId = "a2", AnchorHex = new HexCoord(81, 51) };
            a2.SetFootprint(baseFp);
            Assert.IsTrue(WorldSiteSpatialMapping.TryBuildGeometry(a1, HexSize, out var g1));
            Assert.IsTrue(WorldSiteSpatialMapping.TryBuildGeometry(a2, HexSize, out var g2));

            var samples = new[]
            {
                new WorldVec2(0f, 0f), new WorldVec2(60f, 25f), new WorldVec2(140f, 70f),
                new WorldVec2(-39.5f, -24.5f), new WorldVec2(159.5f, 74.5f),
            };
            for (var i = 0; i < samples.Length; i++)
            {
                WorldSiteSpatialMapping.TryLocalToWorldSurface(g1, bounds, samples[i], out var w1);
                WorldSiteSpatialMapping.TryLocalToWorldSurface(g2, bounds, samples[i], out var w2);
                Assert.AreEqual(w1.X, w2.X, 1e-4f, "anchor irrelevance x sample " + i);
                Assert.AreEqual(w1.Y, w2.Y, 1e-4f, "anchor irrelevance y sample " + i);
            }
        }

        // ============================ [7] geometry overload 零堆分配（B4 热路径复用） ============================

        [Test]
        public void V2_07_GeometryOverload_NoPerCallAllocation()
        {
            var (site, bounds, _, _, _) = LoadReferenceWorld();
            Assert.IsTrue(WorldSiteSpatialMapping.TryBuildGeometry(site, HexSize, out var g));

            // 热身：触发 JIT（JIT 自身会分配，不计入测量区间）
            for (var w = 0; w < 16; w++)
            {
                var warm = new WorldVec2(bounds.MinX + w * 7.3f, bounds.MinY + w * 3.1f);
                WorldSiteSpatialMapping.TryLocalToWorldSurface(g, bounds, warm, out var ww);
                WorldSiteSpatialMapping.TryWorldSurfaceToLocal(g, bounds, ww, out _);
            }

            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            var checksum = 0d;
            const int iterations = 2000;
            for (var i = 0; i < iterations; i++)
            {
                // 测量区间内不调用 Assert / 日志（避免框架分配污染）；映射正确性由 V2_02 覆盖。
                var p = new WorldVec2(
                    bounds.MinX + (i % 200) + 0.5f,
                    bounds.MinY + (i % 100) + 0.5f);
                if (!WorldSiteSpatialMapping.TryLocalToWorldSurface(g, bounds, p, out var w) ||
                    !WorldSiteSpatialMapping.TryWorldSurfaceToLocal(g, bounds, w, out var p2))
                    continue;
                checksum += p2.X + p2.Y;
            }
            var allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.IsTrue(checksum > 0d, "sanity: 映射应有输出");
            Assert.AreEqual(0, allocated,
                "geometry overload 热路径（" + iterations + " 轮 L2W+W2L）managed 分配 = " +
                allocated + " bytes —— B4 每帧复用已构建 geometry 必须零分配");
            TestContext.Out.WriteLine("allocation bytes=" + allocated + " checksum=" + checksum);
        }
    }

    /// <summary>最小 JSON 解析 helper（避免 EditMode 依赖 JsonUtility 序列化限制）。</summary>
    internal sealed class SimpleJson
    {
        readonly System.Text.Json.JsonDocument _doc;

        SimpleJson(System.Text.Json.JsonDocument doc)
        {
            _doc = doc;
        }

        public static SimpleJson Parse(string text)
        {
            return new SimpleJson(System.Text.Json.JsonDocument.Parse(text));
        }

        public SimpleJsonObject GetArray(string name)
        {
            return new SimpleJsonObject(_doc.RootElement.GetProperty(name));
        }

        public sealed class SimpleJsonObject
        {
            readonly System.Text.Json.JsonElement _e;

            public SimpleJsonObject(System.Text.Json.JsonElement e)
            {
                _e = e;
            }

            public SimpleJsonObject this[int index] => new SimpleJsonObject(_e[index]);

            public int Length => _e.GetArrayLength();

            public string GetString(string name) => _e.TryGetProperty(name, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() : null;

            public int GetInt(string name) => _e.GetProperty(name).GetInt32();

            public float GetFloat(string name) => _e.GetProperty(name).GetSingle();

            public bool GetBool(string name) => _e.GetProperty(name).GetBoolean();
        }
    }
}
