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
    /// Phase 5R-B6.3A：Departure Route / Exit Authority Consistency。
    /// 目标：一次 departure 只有一条正式 connection identity；
    /// route 数据起点（DerivedHex）与 route 绘制起点（Query helper）必须同源，
    /// 不得再用 AtWorldSite 冻结的 CurrentHex（= presence）作为 WorldMap 路线前缀起点。
    /// 真实数据：ch01_hex_world.json（base:site_huangcun，4-Hex footprint）。
    /// </summary>
    public sealed class WorldSiteDepartureRouteConsistencyTests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        const float HexSize = 1f;

        static readonly string WorldJsonPath =
            Path.Combine(BaseGamePath, "Data", "Worlds", "ch01_hex_world.json");

        static readonly WildernessLocalWorldProjection.WildernessLocalMapBounds RealBounds =
            WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(-40f, -25f, 1f, 80, 50);

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

        static (SimulationWorld World, WorldSite Site, PlayerPartyRuntime Party) Build()
        {
            var site = LoadHuangcun();
            var world = new SimulationWorld();
            world.HexWorld.MapId = "test:ch01";
            world.HexWorld.HexSize = HexSize;
            world.HexWorld.FillRectangle(200, 100, HexTerrainType.Plain);
            world.Strategic.Sites.Register(site);
            var party = new PlayerPartyRuntime();
            party.TryInitialize(new EntityId(1), out _);
            return (world, site, party);
        }

        static void BeginDeparture(
            SimulationWorld world,
            WorldSite site,
            PlayerPartyRuntime party,
            WorldVec2 canonical,
            HexCoord goal,
            out PlayerPartyWorldMotion motion)
        {
            motion = world.PlayerPartyTravel;
            motion.SetAtWorldSite(site.SiteId, site.PresenceHex, HexSize); // CurrentHex = presence
            Assert.IsTrue(
                motion.TryUpdateWorldPositionWithinSite(site.SiteId, canonical),
                "canonical set: " + canonical);
            var result = PlayerPartyHexTravelService.BeginTravel(world, party, goal);
            Assert.IsTrue(result.IsSuccess, "BeginTravel failed");
            Assert.IsTrue(motion.IsSiteDeparturePending, "departure pending");
        }

        [Test]
        public void B6_3A_01_RouteStart_UsesCanonicalDerivedHex_NotFrozenCurrentHex()
        {
            var (world, site, party) = Build();
            // presence=(80,51)；角色 Canonical 在 (81,52)（另一 footprint hex）→ CurrentHex 冻结为 (80,51)
            var canonical = new WorldVec2(140.296f, 78f); // (81,52) 中心
            BeginDeparture(world, site, party, canonical, new HexCoord(92, 56), out var motion);

            var derived = HexMath.WorldToHex(canonical.X, canonical.Y, HexSize);
            Assert.AreEqual(new HexCoord(81, 52), derived, "fixture canonical hex");
            Assert.AreEqual(site.PresenceHex, motion.CurrentHex, "fixture CurrentHex 冻结为 presence");

            Assert.IsTrue(
                PlayerPartyWorldLocationQuery.TryResolveRouteStartHex(world, motion, out var start, out var pathIndex),
                "resolve route start");
            Assert.AreEqual(derived, start, "route 起点 = Canonical 派生 hex（不是冻结 presence）");
            Assert.AreNotEqual(site.PresenceHex, start, "不再从 presence 画路线前缀");

            var path = motion.HexPath;
            Assert.Greater(path.Count, 0, "path non-empty");
            Assert.AreEqual(path[0], derived, "path[0] == canonical derived hex");
            Assert.AreEqual(1, pathIndex, "pathIndex 跳过重复 path[0]");
        }

        [Test]
        public void B6_3A_02_RouteStart_WhenCanonicalAtPresenceHex_NoSplit()
        {
            var (world, site, party) = Build();
            // Canonical 恰在 presence hex (80,51) 中心 → 无分裂
            HexMath.ToWorldPosition(new HexCoord(80, 51), HexSize, out var cx, out var cy);
            BeginDeparture(world, site, party, new WorldVec2(cx, cy), new HexCoord(92, 56), out var motion);

            var derived = HexMath.WorldToHex(cx, cy, HexSize);
            Assert.IsTrue(
                PlayerPartyWorldLocationQuery.TryResolveRouteStartHex(world, motion, out var start, out var pathIndex),
                "resolve route start");
            Assert.AreEqual(derived, start, "route 起点 = presence hex（角色就在那）");
            Assert.AreEqual(motion.CurrentHex, start, "与 CurrentHex 一致（角色恰在 presence）");
            Assert.AreEqual(1, pathIndex, "path[0]==start → 跳过");
        }

        [Test]
        public void B6_3A_03_AtWorldPosition_KeepsCurrentHexBehavior()
        {
            var (world, site, party) = Build();
            var motion = world.PlayerPartyTravel;
            HexMath.ToWorldPosition(new HexCoord(70, 60), HexSize, out var wx, out var wy);
            motion.SetAtWorldPosition(new WorldVec2(wx, wy), new HexCoord(70, 60));
            var path = new List<HexCoord> { new HexCoord(70, 60), new HexCoord(71, 60), new HexCoord(72, 60) };
            motion.BeginAutoTravel(path, new HexCoord(72, 60), string.Empty, HexTravelMode.Ground, HexSize);

            Assert.IsTrue(
                PlayerPartyWorldLocationQuery.TryResolveRouteStartHex(world, motion, out var start, out var pathIndex),
                "resolve route start");
            Assert.AreEqual(new HexCoord(70, 60), start, "AtWorldPosition → CurrentHex（既有行为）");
            Assert.AreEqual(1, pathIndex, "path[0]==CurrentHex → 跳过");
        }

        [Test]
        public void B6_3A_04_NoDeparture_KeepsCurrentHex()
        {
            var (world, site, party) = Build();
            var motion = world.PlayerPartyTravel;
            motion.SetAtWorldSite(site.SiteId, site.PresenceHex, HexSize);
            // 无 departure：普通 Site 内移动（B4 状态）→ helper 返回 CurrentHex（保守）
            Assert.IsTrue(
                PlayerPartyWorldLocationQuery.TryResolveRouteStartHex(world, motion, out var start, out var pathIndex),
                "resolve route start");
            Assert.AreEqual(motion.CurrentHex, start, "无 departure → CurrentHex");
            Assert.AreEqual(0, pathIndex, "无 path → 0");
        }

        [Test]
        public void B6_3A_05_AuthorityInvariant_Exit_Connection_RouteStart()
        {
            // 一次 departure 只有一条 connection identity：
            // SiteDepartureExitHex == FormalConnection.DestinationHex
            // SiteDepartureFootprintHex == FormalConnection.SourceHex
            // route 起点 == DerivedHex == path[0]
            var (world, site, party) = Build();
            var canonical = new WorldVec2(138.564f, 78f); // (80,52) 中心
            BeginDeparture(world, site, party, canonical, new HexCoord(92, 56), out var motion);

            var derived = HexMath.WorldToHex(canonical.X, canonical.Y, HexSize);
            Assert.IsTrue(
                WorldSiteFootprintExitConnectionResolver.TryResolveFormalExitConnection(
                    world, site, motion.SiteDepartureFootprintHex, motion.SiteDepartureExitHex, HexSize,
                    RealBounds, out var conn),
                "formal connection resolve");

            Assert.AreEqual(motion.SiteDepartureExitHex, conn.DestinationHex,
                "Route exit hex == FormalConnection.DestinationHex");
            Assert.AreEqual(motion.SiteDepartureFootprintHex, conn.SourceHex,
                "Route footprint hex == FormalConnection.SourceHex");
            Assert.AreEqual(derived, motion.HexPath[0], "path[0] == Canonical derived hex");
            Assert.IsTrue(site.OccupiesHex(derived), "canonical 派生 hex 在 footprint 内");
        }
    }
}
