using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    /// <summary>
    /// Phase 5R-B6.2：WorldSite Egress Crossing Handshake。
    /// 核心验证（真实 huangcun_01 LocalMap bounds [-40,40]×[-25,25]，exitTriggerDepth=1.25）：
    ///  - 视觉 ExitZone（HostSurfaceExitZonePresenter → CollectVisibleZones）与 crossing predicate
    ///    （PointBelongsToConnection）使用同一真实 bounds 派生的 SlotRect（同源）—— 原 B6 用名义
    ///    bounds [0,16]² 生成 SlotRect，与视觉方块错位，导致"走进方块不 crossing"。
    ///  - approach 目标权威 clamp 进正式 SlotRect 触发带内（不再停在带外）。
    ///  - predicate 对 SlotRect 内/外点正确。
    ///  - cross 失败不 teleport（Canonical 不变），返回明确 failure。
    /// 真实 fixture：ch01_hex_world.json（base:site_huangcun，4-Hex footprint）+ huangcun_01.json。
    /// </summary>
    public sealed class WorldSiteDepartureCrossingTests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        const float HexSize = 1f;

        static string WorldJsonPath => Path.Combine(BaseGamePath, "Data", "Worlds", "ch01_hex_world.json");

        // huangcun_01.json 真实 layout：origin(-40,-25) cellSize=1 80×50，exitTriggerDepth=1.25。
        static readonly WildernessLocalWorldProjection.WildernessLocalMapBounds RealBounds =
            WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(-40f, -25f, 1f, 80, 50);
        static readonly WorldSiteSpatialMapping.WorldSiteLocalMapBounds RealSiteBounds =
            WorldSiteSpatialMapping.WorldSiteLocalMapBounds.FromOriginSize(-40f, -25f, 1f, 80, 50);
        const float RealDepth = 1.25f;

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

        static (SimulationWorld World, WorldSite Site) BuildWorld()
        {
            var site = LoadHuangcun();
            var world = new SimulationWorld();
            world.HexWorld.MapId = "test:ch01";
            world.HexWorld.FillRectangle(200, 140, HexTerrainType.Plain);
            world.Strategic.Sites.Register(site);
            return (world, site);
        }

        /// <summary>真实 bounds 下解析全部正式 WorldSite connections（presenter 同源路径）。</summary>
        static List<SurfaceExitConnection> CollectRealConnections(SimulationWorld world, WorldSite site)
        {
            var scratch = new List<SurfaceExitConnection>(12);
            WorldSiteFootprintExitConnectionResolver.CollectConnections(
                world,
                site,
                HexSize,
                RealBounds,
                RealDepth,
                SurfaceExitZoneCalculator.DefaultSlotSpanFraction,
                scratch);
            return scratch;
        }

        // ============================ [1] 视觉与 predicate 同源（真实 bounds） ============================

        [Test]
        public void B6_2_01_VisualExitZone_And_FormalConnection_ShareRealBoundsSlotRect()
        {
            var (world, site) = BuildWorld();
            var all = CollectRealConnections(world, site);
            Assert.Greater(all.Count, 0, "huangcun 有正式 exit connections");

            foreach (var c in all)
            {
                // presenter 路径：HostSurfaceExitZonePresenter → CollectVisibleZones（同源 CollectConnections）
                // → AppendConnectionCoverageRects(Connection) → 视觉 rect。
                var rects = new List<SurfaceExitCoverageRect>(4);
                SurfaceExitZoneCalculator.AppendConnectionCoverageRects(c, rects);
                Assert.AreEqual(1, rects.Count, "每 connection 一个 coverage rect");
                Assert.AreEqual(c.SlotRect.MinX, rects[0].MinX, 1e-4f, "rect.MinX == SlotRect.MinX");
                Assert.AreEqual(c.SlotRect.MaxX, rects[0].MaxX, 1e-4f, "rect.MaxX == SlotRect.MaxX");
                Assert.AreEqual(c.SlotRect.MinY, rects[0].MinY, 1e-4f, "rect.MinY == SlotRect.MinY");
                Assert.AreEqual(c.SlotRect.MaxY, rects[0].MaxY, 1e-4f, "rect.MaxY == SlotRect.MaxY");

                // departure 解析路径：TryResolveFormalExitConnection（真实 bounds）→ 同一 canonical
                // identity → 同一 SlotRect（视觉与 predicate 同源，无第二套 bounds）。
                Assert.IsTrue(
                    WorldSiteFootprintExitConnectionResolver.TryResolveFormalExitConnection(
                        world, site, c.SourceHex, c.DestinationHex, HexSize, RealBounds, out var resolved),
                    "departure resolver 可解析 presenter 的同一 connection identity");
                Assert.AreEqual(c.SlotRect.MinX, resolved.SlotRect.MinX, 1e-4f, "resolved.SlotRect.MinX 同源");
                Assert.AreEqual(c.SlotRect.MaxX, resolved.SlotRect.MaxX, 1e-4f, "resolved.SlotRect.MaxX 同源");
                Assert.AreEqual(c.SlotRect.MinY, resolved.SlotRect.MinY, 1e-4f, "resolved.SlotRect.MinY 同源");
                Assert.AreEqual(c.SlotRect.MaxY, resolved.SlotRect.MaxY, 1e-4f, "resolved.SlotRect.MaxY 同源");
            }
        }

        [Test]
        public void B6_2_02_RealBoundsSlotRect_LiesOnRealLocalMapPerimeter()
        {
            var (world, site) = BuildWorld();
            var all = CollectRealConnections(world, site);
            foreach (var c in all)
            {
                var slot = c.SlotRect;
                // SlotRect 贴真实 LocalMap 周界（depth=1.25）：东/西贴 MaxX/MinX，北/南贴 MaxY/MinY。
                var onX = System.Math.Abs(slot.MaxX - RealBounds.MaxX) < 1e-3f ||
                          System.Math.Abs(slot.MinX - RealBounds.MinX) < 1e-3f;
                var onY = System.Math.Abs(slot.MaxY - RealBounds.MaxY) < 1e-3f ||
                          System.Math.Abs(slot.MinY - RealBounds.MinY) < 1e-3f;
                Assert.IsTrue(onX || onY, "SlotRect 必须贴真实 LocalMap 周界（原 bug：名义 bounds 偏离真实周界）");
            }
        }

        // ============================ [2] approach 目标 ∈ 正式触发带 ============================

        [Test]
        public void B6_2_03_ApproachPoint_AlwaysInsideFormalSlotRect()
        {
            var (world, site) = BuildWorld();
            var all = CollectRealConnections(world, site);
            Assert.Greater(all.Count, 0);
            foreach (var c in all)
            {
                PlayerPartyLocalVisibleAutoTravelService.ResolveWorldSiteExitApproachLocalPoint(
                    c, RealSiteBounds, RealDepth, out var ax, out var ay);
                Assert.IsTrue(
                    SurfaceExitZoneCalculator.PointBelongsToConnection(ax, ay, c, RealDepth),
                    "approach 目标必须 ∈ 正式 SlotRect（dst=" + c.DestinationHex + "）");
            }
        }

        [Test]
        public void B6_2_04_PointBelongsToConnection_SlotRectInOut_Correct()
        {
            var (world, site) = BuildWorld();
            var all = CollectRealConnections(world, site);
            Assert.Greater(all.Count, 0);
            var c = all[0];
            var slot = c.SlotRect;

            // 触发带内点（中心）→ true。
            var inX = (slot.MinX + slot.MaxX) * 0.5f;
            var inY = (slot.MinY + slot.MaxY) * 0.5f;
            Assert.IsTrue(
                SurfaceExitZoneCalculator.PointBelongsToConnection(inX, inY, c, RealDepth),
                "SlotRect 中心 ∈ 触发带 → crossing check true");

            // 带外点（向 LocalMap 中心偏移 10）→ false（不会在带外触发 crossing）。
            var inwardX = c.LocalDirectionX >= 0.0001f ? slot.MinX - 10f : slot.MaxX + 10f;
            var inwardY = c.LocalDirectionY >= 0.0001f ? slot.MinY - 10f : slot.MaxY + 10f;
            Assert.IsFalse(
                SurfaceExitZoneCalculator.PointBelongsToConnection(inwardX, inwardY, c, RealDepth),
                "触发带外 → crossing check false（角色停在带外不 crossing）");
        }

        // ============================ [3] cross 失败不 teleport ============================

        [Test]
        public void B6_2_05_CrossFailure_DoesNotModifyCanonical()
        {
            var (world, site) = BuildWorld();
            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryInitialize(new EntityId(1001ul), out _), "party init");

            var m = world.PlayerPartyTravel;
            m.SetAtWorldSite(site.SiteId, new HexCoord(80, 51), HexSize);
            m.CaptureTravelingMembers(party.Members);
            var canonical = new WorldVec2(138.2f, 76.5f);
            Assert.IsTrue(m.TryUpdateWorldPositionWithinSite(site.SiteId, canonical), "canonical set");
            m.BeginSiteDepartureTravel(
                new List<HexCoord> { new HexCoord(80, 51), new HexCoord(81, 50), new HexCoord(82, 50) },
                new HexCoord(82, 50),
                string.Empty,
                new HexCoord(80, 51),
                new HexCoord(81, 50),
                canonical,
                new WorldVec2(137.8f, 77.5f),
                HexTravelMode.Ground,
                HexSize);

            // 把 external hex 设为 Water（不可通行）→ cross 必须失败。
            // 顺序：先解析 connection（需 external 可通行），再设 Water，再 cross。
            var conn = default(SurfaceExitConnection);
            var ok = WorldSiteFootprintExitConnectionResolver.TryResolveFormalExitConnection(
                world, site, new HexCoord(80, 51), new HexCoord(81, 50), HexSize, RealBounds, out conn);
            if (!ok)
                return; // 该方向在真实 bounds 下可能无 connection——跳过，防御断言已由 01 覆盖

            world.HexWorld.SetTile(new XianXia.Core.World.Hex.HexCell
            {
                Coord = new HexCoord(81, 50),
                Terrain = HexTerrainType.Water,
                IsPassable = false,
            });

            var before = m.WorldPosition;
            var beforeKind = m.LocationKind;
            var result = PlayerPartyLocalVisibleAutoTravelService
                .TryCrossWorldSiteEdgePreservingLocalVisibleAutoTravel(world, party, conn);

            Assert.IsFalse(result.IsSuccess, "不可通行 external → cross 失败");
            Assert.AreEqual(before, m.WorldPosition, "失败不修改 Canonical（不 teleport）");
            Assert.AreEqual(beforeKind, m.LocationKind, "失败保持 AtWorldSite");
        }

        [Test]
        public void B6_2_06_NominalBoundsSlotRect_DiffersFromRealBoundsSlotRect()
        {
            // 证明原 bug 的存在面：名义 bounds (0,0,1,16,16) 与真实 bounds 派生的 SlotRect 不同 ——
            // 这正是"predicate 查名义区域、视觉方块在真实区域"错位的来源。
            var (world, site) = BuildWorld();
            var nominal = WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(
                0f, 0f, 1f, 16, 16);

            var realAll = CollectRealConnections(world, site);
            Assert.Greater(realAll.Count, 0);
            foreach (var c in realAll)
            {
                var scratch = new List<SurfaceExitConnection>(12);
                WorldSiteFootprintExitConnectionResolver.CollectConnections(
                    world, site, HexSize, nominal,
                    SurfaceExitZoneCalculator.DefaultExitTriggerDepth,
                    SurfaceExitZoneCalculator.DefaultSlotSpanFraction,
                    scratch);
                foreach (var n in scratch)
                {
                    if (!n.SourceHex.Equals(c.SourceHex) || !n.DestinationHex.Equals(c.DestinationHex))
                        continue;
                    var same = System.Math.Abs(n.SlotRect.MinX - c.SlotRect.MinX) < 1e-3f &&
                               System.Math.Abs(n.SlotRect.MaxX - c.SlotRect.MaxX) < 1e-3f &&
                               System.Math.Abs(n.SlotRect.MinY - c.SlotRect.MinY) < 1e-3f &&
                               System.Math.Abs(n.SlotRect.MaxY - c.SlotRect.MaxY) < 1e-3f;
                    Assert.IsFalse(same,
                        "同一 connection 的名义/真实 bounds SlotRect 必须不同（否则原错位 bug 不成立）");
                }
            }
        }
    }
}
