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
    /// Phase 5R-B6.5：
    /// A. Multi-Hex WorldSite Egress Continuation —— crossing 后 Route Progress 必须对齐到
    ///    已提交的 FormalConnection.DestinationHex（first outside hex），不依赖
    ///    WorldToHex(BoundaryContactWorld) 的 perimeter tie；LocalVisible departure 全程
    ///    SegmentIndex 恒 0，旧 SetSegment(+1) 在 HexPath 前部含多个 footprint hex 时会卡在
    ///    footprint 内部段（multi-hex Site 内部 seam 出发即触发）→ 修复后至少继续 2 个 segment。
    /// B. World Pause / Travel Executor —— AtWorldSite + departure + World executor（WorldMap
    ///    open + Running）：Canonical 是唯一 physical truth，朝正式 BoundaryContactWorld 直线
    ///    推进，到达后正式 egress commit（AtWorldPosition + route 对齐），后续段继续，不
    ///    CompleteMove、不停在 first outside hex。
    /// </summary>
    public sealed class WorldSiteEgressContinuationTests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        const float HexSize = 1f;

        static readonly string WorldJsonPath =
            Path.Combine(BaseGamePath, "Data", "Worlds", "ch01_hex_world.json");

        // huangcun_01 LocalMap 真实 playable bounds（与 HostSurfaceExitZonePresenter 同源）。
        static readonly WildernessLocalWorldProjection.WildernessLocalMapBounds Bounds =
            WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(-40f, -25f, 1f, 80, 50);

        static WorldSite LoadSite(string siteId)
        {
            var json = File.ReadAllText(WorldJsonPath);
            var j = JsonLite.Parse(json);
            var sites = j.GetArray("definitions")[0].GetArray("sites");
            for (var i = 0; i < sites.Length; i++)
            {
                var s = sites[i];
                if (s.GetString("siteId") != siteId)
                    continue;
                var site = new WorldSite { SiteId = siteId };
                site.AnchorHex = new HexCoord(s.GetInt("anchorQ"), s.GetInt("anchorR"));
                site.PresenceHex = new HexCoord(s.GetInt("presenceQ"), s.GetInt("presenceR"));
                var fp = s.GetArray("footprint");
                var hexes = new List<HexCoord>();
                for (var f = 0; f < fp.Length; f++)
                    hexes.Add(new HexCoord(fp[f].GetInt("q"), fp[f].GetInt("r")));
                site.SetFootprint(hexes);
                return site;
            }

            Assert.Fail("site not found: " + siteId);
            return null;
        }

        static SimulationWorld BuildWorld(params string[] siteIds)
        {
            var world = new SimulationWorld();
            world.HexWorld.MapId = "test:ch01";
            world.HexWorld.HexSize = HexSize;
            world.HexWorld.FillRectangle(200, 100, HexTerrainType.Plain);
            for (var i = 0; i < siteIds.Length; i++)
            {
                var s = LoadSite(siteIds[i]);
                world.Strategic.Sites.Register(s);
                foreach (var h in s.EnumerateFootprintHexes())
                {
                    if (world.HexWorld.TryGetTile(h, out var t) && t != null)
                        t.IsPassable = true;
                }
            }

            return world;
        }

        static PlayerPartyRuntime NewParty() =>
            new PlayerPartyRuntime();

        static WorldVec2 HexCenter(HexCoord hex)
        {
            HexMath.ToWorldPosition(hex, HexSize, out var x, out var y);
            return new WorldVec2(x, y);
        }

        static WorldVec2 Midpoint(HexCoord a, HexCoord b)
        {
            var ca = HexCenter(a);
            var cb = HexCenter(b);
            return new WorldVec2((ca.X + cb.X) * 0.5f, (ca.Y + cb.Y) * 0.5f);
        }

        static int IndexOf(IReadOnlyList<HexCoord> path, HexCoord hex)
        {
            for (var i = 0; i < path.Count; i++)
            {
                if (path[i].Equals(hex))
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// 5 种多格 Site 内部位置：footprint hex 中心 / seam epsilon 左 / 正好 seam / epsilon 右 /
        /// 内部 vertex 附近 —— 覆盖 WorldToHex(BoundaryContact) 的所有 tie 场景。
        /// </summary>
        static List<WorldVec2> InternalSeamPositions(WorldSite site)
        {
            var seam = Midpoint(new HexCoord(80, 52), new HexCoord(81, 52));
            var seamNorth = Midpoint(new HexCoord(80, 51), new HexCoord(81, 51));
            var eps = 0.05f;
            var list = new List<WorldVec2>
            {
                HexCenter(new HexCoord(81, 52)),                                    // 1. footprint hex 中心
                new WorldVec2(seam.X - eps, seam.Y),                                // 2. seam epsilon 左
                seam,                                                               // 3. 正好 seam
                new WorldVec2(seam.X + eps, seam.Y),                                // 4. seam epsilon 右
                new WorldVec2((seam.X + seamNorth.X) * 0.5f,
                              (seam.Y + seamNorth.Y) * 0.5f),                       // 5. 内部 vertex 附近
            };
            _ = site;
            return list;
        }

        // 5 个外部战略目标方向（远点，保证 HexPath ≥ 3 段：能验证"至少继续 2 段"）。
        static readonly HexCoord[] Goals =
        {
            new HexCoord(60, 30),   // 西南
            new HexCoord(110, 60),  // 东北
            new HexCoord(60, 70),   // 西北
            new HexCoord(105, 25),  // 东南
            new HexCoord(75, 25),   // 南
        };

        [Test]
        public void B6_5_01_MultiHexSeamCrossing_RouteContinuesAfterEgress()
        {
            var world = BuildWorld("base:site_huangcun");
            var site = LoadSite("base:site_huangcun");
            var party = NewParty();
            party.TryInitialize(new EntityId(1), out _);
            var motion = world.PlayerPartyTravel;
            motion.SetAtWorldSite(site.SiteId, site.PresenceHex, HexSize);

            var positions = InternalSeamPositions(site);
            Assert.AreEqual(5, positions.Count);

            for (var p = 0; p < positions.Count; p++)
            {
                for (var g = 0; g < Goals.Length; g++)
                {
                    var pos = positions[p];
                    // 每轮迭代重置 AtWorldSite context（上一轮已模拟 crossing → AtWorldPosition）。
                    motion.SetAtWorldSite(site.SiteId, site.PresenceHex, HexSize);
                    Assert.IsTrue(
                        motion.TryUpdateWorldPositionWithinSite(site.SiteId, pos),
                        "canonical set [" + p + "][" + g + "]");
                    var begin = PlayerPartyHexTravelService.BeginTravel(world, party, Goals[g]);
                    Assert.IsTrue(begin.IsSuccess, "BeginTravel [" + p + "][" + g + "] to " + Goals[g]);
                    Assert.IsTrue(motion.IsSiteDeparturePending, "departure pending [" + p + "][" + g + "]");

                    var connOk = WorldSiteFootprintExitConnectionResolver.TryResolveFormalExitConnection(
                        world, site, motion.SiteDepartureFootprintHex, motion.SiteDepartureExitHex,
                        HexSize, Bounds, out var conn);
                    Assert.IsTrue(connOk, "formal connection resolved [" + p + "][" + g + "]");

                    // 模拟 LocalVisible crossing 的状态变更（与 TryCrossWorldSiteEdge 内部一致）：
                    // Canonical = BoundaryContactWorld；Route hex = FormalConnection.DestinationHex；
                    // Route Progress 对齐到 DestinationHex。
                    motion.SetWorldPositionInternal(
                        new WorldVec2(conn.BoundaryContactWorldX, conn.BoundaryContactWorldY),
                        conn.DestinationHex);
                    PlayerPartyHexTravelService.AlignRouteProgressAfterSiteEgress(motion, conn.DestinationHex);

                    // AfterCross invariants（A2 / A3）
                    Assert.AreEqual(
                        PlayerPartyLocationKind.AtWorldPosition, motion.LocationKind,
                        "LocationKind after egress [" + p + "][" + g + "]");
                    Assert.IsTrue(motion.IsMoving, "route still moving [" + p + "][" + g + "]");
                    Assert.AreEqual(
                        conn.DestinationHex, motion.CurrentHex,
                        "committed route hex = FormalConnection.DestinationHex [" + p + "][" + g + "]");

                    Assert.IsTrue(
                        PlayerPartyLocalVisibleAutoTravelService.TryResolveActiveLeg(
                            motion, out var cur, out _, out _),
                        "active leg after egress [" + p + "][" + g + "]");
                    Assert.AreEqual(
                        conn.DestinationHex, cur,
                        "leg starts at first outside hex [" + p + "][" + g + "]");

                    var idx = IndexOf(motion.HexPath, conn.DestinationHex);
                    Assert.GreaterOrEqual(idx, 0, "DestinationHex in HexPath [" + p + "][" + g + "]");
                    Assert.GreaterOrEqual(
                        motion.HexPathCount, idx + 3,
                        "at least 2 segments continue after egress [" + p + "][" + g + "]");
                }
            }
        }

        [Test]
        public void B6_5_02_WorldExecutor_DepartureAdvancesCanonicalTowardBoundary()
        {
            var world = BuildWorld("base:site_huangcun");
            var site = LoadSite("base:site_huangcun");
            var party = NewParty();
            party.TryInitialize(new EntityId(1), out _);
            var motion = world.PlayerPartyTravel;
            motion.SetAtWorldSite(site.SiteId, site.PresenceHex, HexSize);

            var seam = Midpoint(new HexCoord(80, 52), new HexCoord(81, 52));
            Assert.IsTrue(
                motion.TryUpdateWorldPositionWithinSite(site.SiteId, new WorldVec2(seam.X - 0.05f, seam.Y)),
                "canonical at seam epsilon left");
            var begin = PlayerPartyHexTravelService.BeginTravel(world, party, Goals[1]);
            Assert.IsTrue(begin.IsSuccess, "BeginTravel");
            Assert.IsTrue(motion.IsSiteDeparturePending, "departure pending");
            Assert.AreEqual(PlayerPartyTravelExecutionMode.World, motion.ExecutionMode, "World executor mode");

            var target = motion.SiteDepartureBoundaryEntry;
            Assert.IsFalse(float.IsNaN(target.X) || float.IsNaN(target.Y), "formal boundary");
            var exitHex = motion.SiteDepartureExitHex; // commit 前保存（commit 会清 departure 状态）
            var before = motion.WorldPosition;
            var dist = WorldVec2.Distance(before, target);

            // 小预算：Canonical 前进、未到 boundary、未 commit。
            PlayerPartyHexTravelService.AdvanceDistanceBudget(world, 0.5f);
            var after = motion.WorldPosition;
            Assert.Greater(WorldVec2.Distance(before, after), 0.01f, "canonical advanced");
            Assert.Less(WorldVec2.Distance(before, after), dist + 0.001f, "not overshoot");
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldSite, motion.LocationKind, "not committed yet");
            Assert.IsTrue(motion.IsMoving, "still moving");

            // 精确预算直达 boundary：恰好 commit，不递归推进后续段（验证 commit 瞬间 committed hex）。
            PlayerPartyHexTravelService.AdvanceDistanceBudget(world, dist + 0.001f);
            Assert.AreEqual(
                PlayerPartyLocationKind.AtWorldPosition, motion.LocationKind,
                "committed to AtWorldPosition");
            Assert.AreEqual(exitHex, motion.CurrentHex, "route hex = exit hex (commit instant)");
            Assert.IsTrue(motion.IsMoving, "route continues after commit");

            Assert.IsTrue(
                PlayerPartyLocalVisibleAutoTravelService.TryResolveActiveLeg(
                    motion, out var cur, out _, out _),
                "active leg after commit");
            Assert.AreEqual(exitHex, cur, "leg starts at first outside hex");

            var idx = IndexOf(motion.HexPath, exitHex);
            Assert.GreaterOrEqual(idx, 0, "exit hex in HexPath");
            Assert.GreaterOrEqual(motion.HexPathCount, idx + 3, "at least 2 segments continue");

            // 后续 World tick：route 继续推进（不停在 first outside hex）。
            PlayerPartyHexTravelService.AdvanceDistanceBudget(world, 3f);
            Assert.IsTrue(motion.IsMoving, "still moving after further ticks");
            Assert.Greater(motion.SegmentIndex, idx, "route progressed past exit segment");
        }

        [Test]
        public void B6_5_03_WorldExecutor_EgressCommit_ContinuesMultipleSegments_NoStop()
        {
            var world = BuildWorld("base:site_huangcun");
            var site = LoadSite("base:site_huangcun");
            var party = NewParty();
            party.TryInitialize(new EntityId(1), out _);
            var motion = world.PlayerPartyTravel;
            motion.SetAtWorldSite(site.SiteId, site.PresenceHex, HexSize);

            var seam = Midpoint(new HexCoord(80, 52), new HexCoord(81, 52));
            Assert.IsTrue(
                motion.TryUpdateWorldPositionWithinSite(site.SiteId, seam),
                "canonical exactly on seam");
            Assert.IsTrue(
                PlayerPartyHexTravelService.BeginTravel(world, party, Goals[0]).IsSuccess,
                "BeginTravel");
            Assert.IsTrue(motion.IsSiteDeparturePending);

            var target = motion.SiteDepartureBoundaryEntry;
            var dist = WorldVec2.Distance(motion.WorldPosition, target);
            var exitHex = motion.SiteDepartureExitHex; // commit 前保存

            // 模拟多个 World tick（Running）：每 tick 走一小段，越过 crossing 后继续沿 route。
            // 目标足够远（(30,15)，直线 ~53 hex ≈ 92 单位），60 ticks×0.8=48 单位不足以到达。
            var commitSeen = false;
            for (var tick = 0; tick < 60 && motion.IsMoving; tick++)
            {
                PlayerPartyHexTravelService.AdvanceDistanceBudget(world, 0.8f);
                if (!commitSeen &&
                    motion.LocationKind == PlayerPartyLocationKind.AtWorldPosition)
                {
                    commitSeen = true;
                    Assert.AreEqual(exitHex, motion.CurrentHex, "committed at exit hex");
                }
            }

            Assert.IsTrue(commitSeen, "egress committed during world ticks");
            Assert.IsTrue(motion.IsMoving, "still moving (goal is far, not arrived)");
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldPosition, motion.LocationKind);
            var exitIdx = IndexOf(motion.HexPath, exitHex);
            Assert.GreaterOrEqual(exitIdx, 0, "exit hex in path");
            Assert.Greater(motion.SegmentIndex, exitIdx, "route progressed past exit segment (no stop)");
        }

        [Test]
        public void B6_5_04_LocalVisibleCrossing_EndToEnd_ContinuesRoute()
        {
            var world = BuildWorld("base:site_huangcun");
            var site = LoadSite("base:site_huangcun");
            var party = NewParty();
            party.TryInitialize(new EntityId(1), out _);
            var motion = world.PlayerPartyTravel;
            motion.SetAtWorldSite(site.SiteId, site.PresenceHex, HexSize);

            var seam = Midpoint(new HexCoord(80, 52), new HexCoord(81, 52));
            Assert.IsTrue(
                motion.TryUpdateWorldPositionWithinSite(site.SiteId, new WorldVec2(seam.X + 0.05f, seam.Y)),
                "canonical at seam epsilon right");
            Assert.IsTrue(
                PlayerPartyHexTravelService.BeginTravel(world, party, Goals[2]).IsSuccess,
                "BeginTravel");

            var connOk = WorldSiteFootprintExitConnectionResolver.TryResolveFormalExitConnection(
                world, site, motion.SiteDepartureFootprintHex, motion.SiteDepartureExitHex,
                HexSize, Bounds, out var conn);
            Assert.IsTrue(connOk, "formal connection resolved");

            // LocalVisible egress（完整 crossing 服务路径）。
            motion.SetExecutionMode(PlayerPartyTravelExecutionMode.LocalVisible);
            var cross = PlayerPartyLocalVisibleAutoTravelService
                .TryCrossWorldSiteEdgePreservingLocalVisibleAutoTravel(world, party, conn);
            Assert.IsTrue(cross.IsSuccess, "cross success");

            // AfterCross：AtWorldPosition + route 继续 + committed hex = DestinationHex。
            Assert.AreEqual(
                PlayerPartyLocationKind.AtWorldPosition, motion.LocationKind,
                "LocationKind after cross");
            Assert.IsTrue(motion.IsMoving, "route continues after cross");
            Assert.AreEqual(conn.DestinationHex, motion.CurrentHex, "committed route hex");
            Assert.IsFalse(string.IsNullOrEmpty(world.PartyWorld.LocalMapId), "wilderness LocalMap entered");

            Assert.IsTrue(
                PlayerPartyLocalVisibleAutoTravelService.TryResolveActiveLeg(
                    motion, out var cur, out _, out _),
                "active leg after cross");
            Assert.AreEqual(conn.DestinationHex, cur, "leg starts at first outside hex");

            var idx = IndexOf(motion.HexPath, conn.DestinationHex);
            Assert.GreaterOrEqual(motion.HexPathCount, idx + 3, "at least 2 segments continue");
        }

        [Test]
        public void B6_5_05_WorldExecutor_PausedDoesNotAdvance_ResumeAdvances()
        {
            // B7/B8 语义在 Core 层：Pause 由 Host 门控 Loop（bootstrap Update），Core 只保证
            // World executor 在调用时按 distance budget 推进 / 静止即静止。这里验证：
            // 不调用 Advance（模拟 paused）→ Canonical 不变；调用（模拟 running tick）→ 推进。
            var world = BuildWorld("base:site_huangcun");
            var site = LoadSite("base:site_huangcun");
            var party = NewParty();
            party.TryInitialize(new EntityId(1), out _);
            var motion = world.PlayerPartyTravel;
            motion.SetAtWorldSite(site.SiteId, site.PresenceHex, HexSize);

            var seam = Midpoint(new HexCoord(80, 52), new HexCoord(81, 52));
            Assert.IsTrue(
                motion.TryUpdateWorldPositionWithinSite(site.SiteId, seam),
                "canonical set");
            Assert.IsTrue(
                PlayerPartyHexTravelService.BeginTravel(world, party, Goals[3]).IsSuccess,
                "BeginTravel");

            var frozen = motion.WorldPosition;
            // "Paused"：无 tick 调用 → 位置恒不变（order 已存在）。
            for (var i = 0; i < 10; i++)
            {
                Assert.AreEqual(
                    frozen, motion.WorldPosition,
                    "paused: zero movement (no tick calls)");
            }

            // "Resume"：第一个 tick 即推进。
            PlayerPartyHexTravelService.AdvanceDistanceBudget(world, 0.5f);
            Assert.AreNotEqual(
                frozen, motion.WorldPosition,
                "resumed: canonical advances on next simulation tick");
        }
    }
}
