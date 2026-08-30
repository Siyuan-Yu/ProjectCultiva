using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Navigation;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    /// <summary>
    /// Phase 5R-B6.4：Multi-Hex WorldSite Route Endpoint Authority。
    /// 目标：去 WorldSite 的 goal = 目标 Site footprint 中 A* 实际路径代价最低的可达格
    /// （不再用 hex 直线距离 —— ch01 真实地形下 site_daoguan/site_b 各 2 次次优）；
    /// clicked footprint hex / Anchor / Presence 不得决定 route goal；
    /// departure 段 footprint 格必须与正式 connection.SourceHex 一致。
    /// </summary>
    public sealed class WorldSiteMultiHexGoalAuthorityTests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        const float HexSize = 1f;

        static readonly string WorldJsonPath =
            Path.Combine(BaseGamePath, "Data", "Worlds", "ch01_hex_world.json");

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

        [Test]
        public void B6_4_01_GoalToMultiHexSite_UsesAStarCost_NotHexDistance()
        {
            var world = BuildWorld("base:site_huangcun", "base:site_b");
            var huangcun = LoadSite("base:site_huangcun");
            var siteB = LoadSite("base:site_b");
            var party = NewParty();
            party.TryInitialize(new EntityId(1), out _);
            var motion = world.PlayerPartyTravel;
            motion.SetAtWorldSite(huangcun.SiteId, huangcun.PresenceHex, HexSize);
            HexMath.ToWorldPosition(new HexCoord(81, 52), HexSize, out var cx, out var cy);
            Assert.IsTrue(motion.TryUpdateWorldPositionWithinSite(huangcun.SiteId, new WorldVec2(cx, cy)), "canonical set");

            PlayerPartyHexTravelService.TryResolvePartyWorldHex(world, party, out var startHex);
            Assert.AreEqual(new HexCoord(81, 52), startHex, "startHex = canonical derived");

            var blocked = WorldSiteTransitPolicy.BuildBlockedFootprintHexes(world, siteB.SiteId);
            foreach (var h in huangcun.EnumerateFootprintHexes())
                blocked.Remove(h);

            // 目标 site_b：hex 距离最近格 (105,26) 实际 A* cost 69-70；A* 最优 (107,26) cost 67-68。
            var result = PlayerPartyHexTravelService.BeginTravel(world, party, siteB.PresenceHex, siteB.SiteId);
            Assert.IsTrue(result.IsSuccess, "BeginTravel to site_b");

            var goal = motion.DestinationHex;
            var path = new List<HexCoord>(64);
            Assert.IsTrue(
                HexPathfinder.TryFindPath(world.HexWorld, startHex, goal, path, HexTravelMode.Ground, blocked),
                "goal reachable from start");
            var goalCost = path.Count;

            // 验证 goal 的 A* cost == 全局最小（footprint 中所有可达格的最小 cost）
            var minCost = int.MaxValue;
            foreach (var h in siteB.EnumerateFootprintHexes())
            {
                if (!world.HexWorld.TryGetTile(h, out var t) || t == null || !t.IsPassable)
                    continue;
                var p = new List<HexCoord>(64);
                if (!HexPathfinder.TryFindPath(world.HexWorld, startHex, h, p, HexTravelMode.Ground, blocked))
                    continue;
                if (p.Count < minCost)
                    minCost = p.Count;
            }

            Assert.AreEqual(minCost, goalCost, "goal 的 A* 实际代价 = 全局最小（不再 hex 距离次优）");
            Assert.IsTrue(
                goal.Q > 105 || goal.R < 27,
                "goal 落在 site_b 可达侧（放弃 (105,26) 次优格）");
        }

        [Test]
        public void B6_4_02_ClickedFootprintHex_DoesNotForceDestination()
        {
            var world = BuildWorld("base:site_huangcun", "base:site_a");
            var huangcun = LoadSite("base:site_huangcun");
            var siteA = LoadSite("base:site_a");
            var party = NewParty();
            party.TryInitialize(new EntityId(1), out _);
            var motion = world.PlayerPartyTravel;
            motion.SetAtWorldSite(huangcun.SiteId, huangcun.PresenceHex, HexSize);

            // 点击 site_a 不同 footprint 格 → TargetSiteId 相同 + DestinationHex 相同（goal 由 planner 决定）
            var clicked1 = new HexCoord(68, 39);
            var clicked2 = new HexCoord(69, 40);
            Assert.IsTrue(WorldMapPartyTravelCommand.TryResolve(world, clicked1, out var cmd1), "cmd1");
            Assert.IsTrue(WorldMapPartyTravelCommand.TryResolve(world, clicked2, out var cmd2), "cmd2");
            Assert.AreEqual(siteA.SiteId, cmd1.TargetSiteId, "click1 → site_a");
            Assert.AreEqual(siteA.SiteId, cmd2.TargetSiteId, "click2 → site_a");
            Assert.AreEqual(cmd1.DestinationHex, cmd2.DestinationHex,
                "clicked hex 不强制 destination：同一 Site 语义相同");

            foreach (var h in siteA.EnumerateFootprintHexes())
                Assert.IsTrue(siteA.OccupiesHex(h), "cmd destination ∈ site_a footprint: " + h);
        }

        [Test]
        public void B6_4_03_AnchorPresenceChanges_DoNotAffectRouteGoal()
        {
            var world = BuildWorld("base:site_huangcun", "base:site_b");
            var huangcun = LoadSite("base:site_huangcun");
            var siteB = LoadSite("base:site_b");
            var party = NewParty();
            party.TryInitialize(new EntityId(1), out _);

            var before = ResolveGoalToSiteB(world, huangcun, siteB, party);

            // 改 Anchor / Presence → goal 不变（goal 只由 footprint + 起点 + blocked 决定）
            siteB.AnchorHex = new HexCoord(106, 25);
            siteB.PresenceHex = new HexCoord(106, 27);
            var after = ResolveGoalToSiteB(world, huangcun, siteB, party);

            Assert.AreEqual(before, after, "Anchor/Presence 改变不影响 route goal（B6.4 §八 C/D）");
        }

        static HexCoord ResolveGoalToSiteB(
            SimulationWorld world,
            WorldSite huangcun,
            WorldSite siteB,
            PlayerPartyRuntime party)
        {
            var motion = world.PlayerPartyTravel;
            motion.SetAtWorldSite(huangcun.SiteId, huangcun.PresenceHex, HexSize);
            HexMath.ToWorldPosition(new HexCoord(81, 52), HexSize, out var cx, out var cy);
            motion.TryUpdateWorldPositionWithinSite(huangcun.SiteId, new WorldVec2(cx, cy));
            var result = PlayerPartyHexTravelService.BeginTravel(world, party, siteB.PresenceHex, siteB.SiteId);
            Assert.IsTrue(result.IsSuccess, "BeginTravel");
            return motion.DestinationHex;
        }

        [Test]
        public void B6_4_04_DepartureFootprintHex_MatchesFormalConnectionSource()
        {
            var world = BuildWorld("base:site_huangcun");
            var huangcun = LoadSite("base:site_huangcun");
            var bounds = WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(-40f, -25f, 1f, 80, 50);
            var conns = new List<SurfaceExitConnection>(16);
            WorldSiteFootprintExitConnectionResolver.CollectConnections(
                world, huangcun, HexSize, bounds,
                SurfaceExitZoneCalculator.DefaultExitTriggerDepth,
                SurfaceExitZoneCalculator.DefaultSlotSpanFraction,
                conns);
            Assert.Greater(conns.Count, 0, "connections exist");
            foreach (var c in conns)
            {
                Assert.IsTrue(
                    BackgroundCharacterSiteDepartureResolver.TryResolveDepartureFootprintHex(
                        huangcun, c.DestinationHex, out var footprint),
                    "footprint for exit " + c.DestinationHex);
                Assert.AreEqual(c.SourceHex, footprint,
                    "departure 段 footprint 格 == formal connection.SourceHex（exit " + c.DestinationHex + "）");
            }
        }

        [Test]
        public void B6_4_05_CmdFrom_UsesCanonicalDerivedHex_NotFrozenPresence()
        {
            var world = BuildWorld("base:site_huangcun", "base:site_a");
            var huangcun = LoadSite("base:site_huangcun");
            var siteA = LoadSite("base:site_a");
            var party = NewParty();
            party.TryInitialize(new EntityId(1), out _);
            var motion = world.PlayerPartyTravel;
            motion.SetAtWorldSite(huangcun.SiteId, huangcun.PresenceHex, HexSize); // CurrentHex=presence
            HexMath.ToWorldPosition(new HexCoord(81, 52), HexSize, out var cx, out var cy);
            Assert.IsTrue(motion.TryUpdateWorldPositionWithinSite(huangcun.SiteId, new WorldVec2(cx, cy)), "canonical set");

            var derived = HexMath.WorldToHex(cx, cy, HexSize);
            Assert.AreEqual(new HexCoord(81, 52), derived, "fixture derived hex");
            Assert.AreEqual(huangcun.PresenceHex, motion.CurrentHex, "fixture CurrentHex 冻结为 presence");

            Assert.IsTrue(WorldMapPartyTravelCommand.TryResolve(world, new HexCoord(69, 39), out var cmd), "cmd");

            // 修复后 cmd 的 from = Canonical 派生 hex（TryResolve→DerivedHex=(81,52)），不再用
            // presence 冻结 CurrentHex。site_a footprint 中从 (81,52) 出发 hex 距离最近（tie 取 Q 小）
            // = (68,41)：距离 max(|-13|,|-11|,|-24|)=24，与 (69,40) tie，Q 68 < 69 胜出。
            Assert.AreEqual(new HexCoord(68, 41), cmd.DestinationHex,
                "cmd.DestinationHex 基于 Canonical 派生 from（(81,52) 出发 hex 距离最近 + tie Q 最小）");
            Assert.IsTrue(siteA.OccupiesHex(cmd.DestinationHex), "cmd.DestinationHex ∈ site_a footprint");
            Assert.AreEqual(derived, new HexCoord(81, 52), "derived fixture");
        }
    }
}
