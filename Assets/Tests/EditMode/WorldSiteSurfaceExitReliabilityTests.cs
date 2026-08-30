using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using XianXia.Core.Navigation;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    /// <summary>
    /// Phase 5R-B6.3：WorldSite SurfaceExit Crossing Reliability。
    /// 目标：荒村 10 条正式 connection 的 departure approach 全部确定性可靠（人工 ~20% 失败
    /// 已定位 = 2 条角 connection 的 approach 贴 SlotRect 内边缘 / 沿边坐标 OOB）。
    /// 验证链（全部真实 Core）：resolver → approach（SlotRect 深度中点 + 沿边 ∩ bounds 中点）→
    /// predicate → WalkGrid cell → A* 多起点 → 停点余量（arriveEpsilon≈0.2）。
    /// </summary>
    public sealed class WorldSiteSurfaceExitReliabilityTests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        const float HexSize = 1f;
        const float RealDepth = 1.25f;
        const float ArriveEpsilon = 0.2f;
        const float StopMargin = 0.3f;

        static readonly string WorldJsonPath =
            Path.Combine(BaseGamePath, "Data", "Worlds", "ch01_hex_world.json");

        static readonly WildernessLocalWorldProjection.WildernessLocalMapBounds RealBounds =
            WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(-40f, -25f, 1f, 80, 50);

        static readonly WorldSiteSpatialMapping.WorldSiteLocalMapBounds RealSiteBounds =
            WorldSiteSpatialMapping.WorldSiteLocalMapBounds.FromOriginSize(-40f, -25f, 1f, 80, 50);

        static WorldSite LoadHuangcun()
        {
            var json = File.ReadAllText(WorldJsonPath);
            var j = JsonLite.Parse(json);
            var sites = j.GetArray("definitions")[0].GetArray("sites");
            for (var i = 0; i < sites.Length; i++)
            {
                var s = sites[i];
                if (s.GetString("siteId") != "base:site_huangcun")
                    continue;
                var site = new WorldSite { SiteId = "base:site_huangcun" };
                var fp = s.GetArray("footprint");
                var hexes = new List<HexCoord>();
                for (var f = 0; f < fp.Length; f++)
                    hexes.Add(new HexCoord(fp[f].GetInt("q"), fp[f].GetInt("r")));
                site.AnchorHex = hexes[0];
                site.SetFootprint(hexes);
                return site;
            }

            Assert.Fail("huangcun site not found");
            return null;
        }

        static (SimulationWorld World, WorldSite Site, List<SurfaceExitConnection> Connections) Build()
        {
            var site = LoadHuangcun();
            var world = new SimulationWorld();
            world.HexWorld.MapId = "test:ch01";
            world.HexWorld.HexSize = 1f;
            world.HexWorld.FillRectangle(200, 100, HexTerrainType.Plain);
            world.Strategic.Sites.Register(site);

            var conns = new List<SurfaceExitConnection>(16);
            WorldSiteFootprintExitConnectionResolver.CollectConnections(
                world, site, HexSize, RealBounds,
                SurfaceExitZoneCalculator.DefaultExitTriggerDepth,
                SurfaceExitZoneCalculator.DefaultSlotSpanFraction,
                conns);
            return (world, site, conns);
        }

        static WalkGrid BuildHuangcunGrid()
        {
            // huangcun_01 placements 只有 zoneHousing（MapLayoutWalkGridBuilder.IsZoneKind → 不 block）→ 全 walkable。
            return new WalkGrid(-40f, -25f, 1f, 80, 50);
        }

        static bool InRect(SurfaceExitCoverageRect r, float x, float y, float eps = 1e-4f) =>
            x >= r.MinX - eps && x <= r.MaxX + eps && y >= r.MinY - eps && y <= r.MaxY + eps;

        static string Side(SurfaceExitConnection c)
        {
            var lx = c.LocalDirectionX;
            var ly = c.LocalDirectionY;
            return System.Math.Abs(lx) >= System.Math.Abs(ly) ? (lx > 0f ? "E" : "W") : (ly > 0f ? "N" : "S");
        }

        [Test]
        public void B6_3_01_Huangcun_Exactly10Connections_W3E3N2S2()
        {
            var (_, _, conns) = Build();
            Assert.AreEqual(10, conns.Count, "荒村正式 connection 恰好 10 条");
            var w = 0;
            var e = 0;
            var n = 0;
            var s = 0;
            foreach (var c in conns)
            {
                var side = Side(c);
                if (side == "W") w++;
                else if (side == "E") e++;
                else if (side == "N") n++;
                else s++;
            }

            Assert.AreEqual(3, w, "West 3");
            Assert.AreEqual(3, e, "East 3");
            Assert.AreEqual(2, n, "North 2");
            Assert.AreEqual(2, s, "South 2");
        }

        [Test]
        public void B6_3_02_AllApproachPoints_InSlot_PredicateTrue_WalkableCell()
        {
            var (_, _, conns) = Build();
            var grid = BuildHuangcunGrid();
            Assert.Greater(conns.Count, 0, "connections exist");
            foreach (var c in conns)
            {
                PlayerPartyLocalVisibleAutoTravelService.ResolveWorldSiteExitApproachLocalPoint(
                    c, RealSiteBounds, RealDepth, out var ax, out var ay);
                Assert.IsTrue(
                    SurfaceExitZoneCalculator.PointBelongsToConnection(ax, ay, c, RealDepth),
                    "[" + c.DestinationHex + "] approach ∈ SlotRect（predicate true）@" + ax + "," + ay);
                var cellOk = grid.TryWorldToCell(ax, ay, out var cx, out var cy) && grid.IsWalkable(cx, cy);
                Assert.IsTrue(cellOk, "[" + c.DestinationHex + "] approach cell walkable 且在 grid 内 cell=(" + cx + "," + cy + ")");
            }
        }

        [Test]
        public void B6_3_03_StopInterval_InSlotIntersectBounds_ForAll10()
        {
            // §五：stopping distance（arriveEpsilon=0.2）最坏停点（沿 -LocalDirection 朝带外 0.2）
            // 必须仍 ∈ SlotRect ∩ bounds——这是 B6.2 角 connection 失败的根因回归。
            var (_, _, conns) = Build();
            Assert.AreEqual(10, conns.Count, "10 connections");
            foreach (var c in conns)
            {
                PlayerPartyLocalVisibleAutoTravelService.ResolveWorldSiteExitApproachLocalPoint(
                    c, RealSiteBounds, RealDepth, out var ax, out var ay);
                // 最坏停点：停在距目标 arriveEpsilon 处、朝带外（-LocalDirection）方向。
                var wx = ax - c.LocalDirectionX * ArriveEpsilon;
                var wy = ay - c.LocalDirectionY * ArriveEpsilon;
                Assert.IsTrue(InRect(c.SlotRect, wx, wy, 0f),
                    "[" + c.DestinationHex + "] 最坏停点 ∈ SlotRect ((" + wx + "," + wy + "))");
                Assert.IsTrue(
                    wx >= RealBounds.MinX && wx <= RealBounds.MaxX &&
                    wy >= RealBounds.MinY && wy <= RealBounds.MaxY,
                    "[" + c.DestinationHex + "] 最坏停点 ∈ bounds（无 OOB）");
                // approach 自身距 SlotRect 深度/沿边边缘 ≥ StopMargin（保证停止余量）。
                var edgeDist = System.Math.Min(
                    System.Math.Min(System.Math.Abs(ax - c.SlotRect.MinX), System.Math.Abs(ax - c.SlotRect.MaxX)),
                    System.Math.Min(System.Math.Abs(ay - c.SlotRect.MinY), System.Math.Abs(ay - c.SlotRect.MaxY)));
                Assert.GreaterOrEqual(edgeDist, StopMargin - 1e-4f,
                    "[" + c.DestinationHex + "] approach 距 SlotRect 边缘 ≥ 0.3（不再贴边）edgeDist=" + edgeDist);
            }
        }

        [Test]
        public void B6_3_04_AStar_Reachability_FromMultipleStarts_EndInSlot()
        {
            // §四：真实 WalkGrid + GridPathfinder，5 个代表起点 → approach；A* 终点 ∈ SlotRect。
            var (_, _, conns) = Build();
            var grid = BuildHuangcunGrid();
            var starts = new[]
            {
                new[] { 0f, 0f }, new[] { -30f, 0f }, new[] { 30f, 0f },
                new[] { 0f, 18f }, new[] { 0f, -18f },
            };

            foreach (var c in conns)
            {
                PlayerPartyLocalVisibleAutoTravelService.ResolveWorldSiteExitApproachLocalPoint(
                    c, RealSiteBounds, RealDepth, out var ax, out var ay);
                for (var i = 0; i < starts.Length; i++)
                {
                    var path = new List<float>(64);
                    Assert.IsTrue(
                        GridPathfinder.TryFindWorldPath(grid, starts[i][0], starts[i][1], ax, ay, path),
                        "[" + c.DestinationHex + "] A* 起点" + i + " 可达");
                    var ex = path[path.Count - 2];
                    var ey = path[path.Count - 1];
                    Assert.IsTrue(InRect(c.SlotRect, ex, ey, 0f),
                        "[" + c.DestinationHex + "] A* 终点 ∈ SlotRect");
                }
            }
        }

        [Test]
        public void B6_3_05_CornerConnections_Regression_NoEdgeClampNoOob()
        {
            // §三/§九 root cause：角 connection（双邻接，LocalDirection 斜对角 → SlotRect 沿边
            // span 跨 map 角）在 B6.2 下 approach 贴内边缘 / 沿边 OOB → 永不 crossing。
            var (_, _, conns) = Build();
            var corners = new List<SurfaceExitConnection>();
            foreach (var c in conns)
            {
                if (c.DestinationHex.Equals(new HexCoord(79, 53)) ||
                    c.DestinationHex.Equals(new HexCoord(82, 50)))
                    corners.Add(c);
            }

            Assert.AreEqual(2, corners.Count, "两条角 connection：(79,53) 与 (82,50)");
            var grid = BuildHuangcunGrid();
            foreach (var c in corners)
            {
                PlayerPartyLocalVisibleAutoTravelService.ResolveWorldSiteExitApproachLocalPoint(
                    c, RealSiteBounds, RealDepth, out var ax, out var ay);
                Assert.IsTrue(InRect(c.SlotRect, ax, ay, 0f),
                    "[" + c.DestinationHex + "] approach ∈ SlotRect");
                var cellOk = grid.TryWorldToCell(ax, ay, out var cx, out var cy) && grid.IsWalkable(cx, cy);
                Assert.IsTrue(cellOk, "[" + c.DestinationHex + "] approach cell walkable（不 OOB）cell=(" + cx + "," + cy + ")");
                // 沿边坐标必须远离 bounds 边缘（不再取 ExitCenter 的越界沿边分量）。
                // 深度方向中点距 bounds 0.625（depth/2）是正常设计，只检查<b>沿边</b>方向。
                var alongDist = c.SlotRect.Width <= c.SlotRect.Height
                    ? System.Math.Min(System.Math.Abs(ay - RealBounds.MinY), System.Math.Abs(ay - RealBounds.MaxY))
                    : System.Math.Min(System.Math.Abs(ax - RealBounds.MinX), System.Math.Abs(ax - RealBounds.MaxX));
                Assert.GreaterOrEqual(alongDist, 0.9f,
                    "[" + c.DestinationHex + "] 沿边坐标距 bounds 边缘 ≥ 0.9（无 OOB）");
            }
        }

        [Test]
        public void B6_3_06_AllSlots_IntersectBounds_NonEmpty()
        {
            // §三：没有任何 connection 的 SlotRect 完全越出 playable bounds（沿边 span 跨角时
            // approach 用 ∩ bounds 中点兜底）。
            var (_, _, conns) = Build();
            Assert.AreEqual(10, conns.Count, "10 connections");
            foreach (var c in conns)
            {
                var loX = System.Math.Max(c.SlotRect.MinX, RealBounds.MinX);
                var hiX = System.Math.Min(c.SlotRect.MaxX, RealBounds.MaxX);
                var loY = System.Math.Max(c.SlotRect.MinY, RealBounds.MinY);
                var hiY = System.Math.Min(c.SlotRect.MaxY, RealBounds.MaxY);
                Assert.IsTrue(loX < hiX && loY < hiY,
                    "[" + c.DestinationHex + "] SlotRect ∩ bounds 非空");
            }
        }

        /// <summary>最小 JSON helper（与既有 EditMode 测试同模式，独立命名防冲突）。</summary>
        internal sealed class JsonLite
        {
            readonly TestJsonValue _v;

            JsonLite(TestJsonValue v)
            {
                _v = v;
            }

            public static JsonLite Parse(string text) => new JsonLite(TestJson.Parse(text));

            public JsonLiteObject GetArray(string name) => new JsonLiteObject(_v.Get(name));

            public sealed class JsonLiteObject
            {
                readonly TestJsonValue _v;

                public JsonLiteObject(TestJsonValue v)
                {
                    _v = v;
                }

                public JsonLiteObject this[int index] => new JsonLiteObject(_v.At(index));

                public int Length => _v.ArrayCount;

                public JsonLiteObject GetArray(string name) => new JsonLiteObject(_v.Get(name));

                public string GetString(string name) => _v.Get(name).IsString ? _v.Get(name).Str : null;

                public int GetInt(string name) => (int)_v.Get(name).Num;
            }
        }
    }
}
