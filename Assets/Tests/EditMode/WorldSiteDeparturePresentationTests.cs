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
    /// Phase 5R-B6.1：Departure Presentation Authority + Egress Camera。
    /// 覆盖：AtWorldSite 阶段（Idle / Planned / Approaching / IsMoving）Query 一律 Canonical-first
    /// （不再被 IsMoving→TravelPresentation 抢走，修复 DepartureApproach 时 WorldMap 头像卡住）；
    /// Approaching 时 Canonical 连续变化 → Query 连续；AtWorldPosition + World travel 仍保持
    /// TravelPresentation；Query 全程只读；Presence/Anchor 改变不影响 DepartureApproach marker。
    /// Camera（用户 §九 8-10）：结构审计 —— egress 成功置 _pendingEgressRecenter，
    /// OnLocalMapMaterialized（materialize + 实体重建完成后）消费并 SnapCameraToActiveOnce（置 Free，
    /// 一次性对准）；普通 WorldMap open/close 不设标志 → 不强制 recenter。
    /// 真实 fixture：ch01_hex_world.json（base:site_huangcun，4-Hex footprint）。
    /// </summary>
    public sealed class WorldSiteDeparturePresentationTests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        const float HexSize = 1f;

        static string WorldJsonPath => Path.Combine(BaseGamePath, "Data", "Worlds", "ch01_hex_world.json");

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

        static (SimulationWorld World, WorldSite Site, PlayerPartyRuntime Party) BuildWorld()
        {
            var site = LoadHuangcun();
            var world = new SimulationWorld();
            world.HexWorld.MapId = "test:ch01";
            world.HexWorld.FillRectangle(200, 140, HexTerrainType.Plain);
            world.Strategic.Sites.Register(site);

            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryInitialize(new EntityId(1001ul), out _), "party init");
            return (world, site, party);
        }

        static void SetAtSite(
            SimulationWorld world,
            WorldSite site,
            PlayerPartyRuntime party,
            HexCoord footprintHex,
            WorldVec2 canonical)
        {
            var m = world.PlayerPartyTravel;
            m.SetAtWorldSite(site.SiteId, footprintHex, HexSize);
            m.CaptureTravelingMembers(party.Members);
            Assert.IsTrue(m.TryUpdateWorldPositionWithinSite(site.SiteId, canonical), "canonical set");
        }

        static PlayerPartyWorldMotion BeginDeparture(
            SimulationWorld world,
            WorldSite site,
            PlayerPartyRuntime party,
            HexCoord footprintHex,
            HexCoord exitHex,
            HexCoord goalHex,
            WorldVec2 canonical)
        {
            SetAtSite(world, site, party, footprintHex, canonical);
            var m = world.PlayerPartyTravel;
            m.BeginSiteDepartureTravel(
                new List<HexCoord> { footprintHex, exitHex, goalHex },
                goalHex,
                string.Empty,
                footprintHex,
                exitHex,
                new WorldVec2(139.8f, 77.2f),
                new WorldVec2(137.8f, 77.5f),
                HexTravelMode.Ground,
                HexSize);
            return m;
        }

        // ============================ [1] Idle + valid Canonical ============================

        [Test]
        public void B6_1_01_AtWorldSite_Idle_ValidCanonical_QueryIsCanonical()
        {
            var (world, site, party) = BuildWorld();
            var canonical = new WorldVec2(138.2f, 76.5f);
            SetAtSite(world, site, party, new HexCoord(80, 51), canonical);

            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, party, out var r), "resolve");
            Assert.AreEqual(canonical, r.WorldPosition, "Query == Canonical (Idle)");
            Assert.IsFalse(r.IsLegacyFallback, "not legacy fallback");
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldSite, r.LocationKind, "Context 保持 AtWorldSite");
            Assert.AreEqual(site.SiteId, r.SiteId, "SiteId 保持");
        }

        // ============================ [2] Planned + IsMoving ============================

        [Test]
        public void B6_1_02_AtWorldSite_Planned_IsMoving_QueryIsCanonical()
        {
            var (world, site, party) = BuildWorld();
            var canonical = new WorldVec2(138.2f, 76.5f);
            var m = BeginDeparture(
                world, site, party, new HexCoord(80, 51), new HexCoord(79, 51), new HexCoord(40, 40), canonical);

            Assert.IsTrue(m.IsMoving, "fixture: IsMoving");
            Assert.AreEqual(PlayerPartyDeparturePhase.Planned, m.DeparturePhase, "fixture: Planned");
            var virtualPos = new WorldVec2(139.8f, 77.2f);
            Assert.AreEqual(virtualPos, m.SiteDepartureVirtualPosition,
                "fixture: departure virtual 与 canonical 不同");

            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, party, out var r), "resolve");
            Assert.AreEqual(canonical, r.WorldPosition,
                "Planned + IsMoving → 仍 Canonical（不被 IsMoving→TravelPresentation 抢走）");
            Assert.AreNotEqual(virtualPos, r.WorldPosition, "不得返回 SiteDepartureVirtualPosition");
            Assert.IsFalse(r.IsLegacyFallback, "not legacy fallback");
        }

        // ============================ [3] Approaching + IsMoving ============================

        [Test]
        public void B6_1_03_AtWorldSite_Approaching_IsMoving_QueryIsCanonical()
        {
            var (world, site, party) = BuildWorld();
            var canonical = new WorldVec2(138.2f, 76.5f);
            var m = BeginDeparture(
                world, site, party, new HexCoord(80, 51), new HexCoord(79, 51), new HexCoord(40, 40), canonical);
            m.SetDeparturePhase(PlayerPartyDeparturePhase.Approaching);

            Assert.IsTrue(m.IsMoving, "fixture: IsMoving during approach");
            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, party, out var r), "resolve");
            Assert.AreEqual(canonical, r.WorldPosition, "Approaching + IsMoving → 仍 Canonical");
            Assert.IsFalse(r.IsLegacyFallback, "not legacy fallback");
        }

        // ============================ [4] Approaching 中 Canonical A→B 连续 ============================

        [Test]
        public void B6_1_04_Approaching_CanonicalAtoB_QueryFollowsContinuously()
        {
            var (world, site, party) = BuildWorld();
            var a = new WorldVec2(138.2f, 76.5f);
            var b = new WorldVec2(141.6f, 76.5f);
            var m = BeginDeparture(
                world, site, party, new HexCoord(80, 51), new HexCoord(79, 51), new HexCoord(40, 40), a);
            m.SetDeparturePhase(PlayerPartyDeparturePhase.Approaching);

            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, party, out var ra), "resolve A");
            Assert.AreEqual(a, ra.WorldPosition, "位置 A");

            Assert.IsTrue(m.TryUpdateWorldPositionWithinSite(site.SiteId, b), "canonical B");
            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, party, out var rb), "resolve B");
            Assert.AreEqual(b, rb.WorldPosition, "位置 B 连续（不量化 hex center）");
            Assert.AreNotEqual(a, rb.WorldPosition, "移动后 Query 位置随之变化");
        }

        // ============================ [5] AtWorldPosition + World travel 保持 TravelPresentation ============================

        [Test]
        public void B6_1_05_AtWorldPosition_WorldTravel_KeepsTravelPresentation()
        {
            var (world, site, party) = BuildWorld();
            var m = world.PlayerPartyTravel;
            m.SetWorldPositionInternal(new WorldVec2(100f, 60f), new HexCoord(50, 30));
            m.BeginAutoTravel(
                new List<HexCoord> { new HexCoord(50, 30), new HexCoord(49, 30) },
                new HexCoord(49, 30),
                string.Empty,
                HexTravelMode.Ground,
                HexSize);
            Assert.IsTrue(m.IsMoving, "fixture: world travel");

            var tp = new WorldVec2(95.5f, 62.5f);
            m.SetTravelPresentation(tp, new HexCoord(49, 30));

            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, party, out var r), "resolve");
            Assert.AreEqual(tp, r.WorldPosition,
                "AtWorldPosition + World travel → TravelPresentation 保持（egress 后 presentation）");
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldPosition, r.LocationKind, "Context 已切换");
        }

        // ============================ [6] Query 完全只读 ============================

        [Test]
        public void B6_1_06_QueryIsReadOnly_AcrossDeparturePhases()
        {
            var (world, site, party) = BuildWorld();
            var canonical = new WorldVec2(138.2f, 76.5f);
            var m = BeginDeparture(
                world, site, party, new HexCoord(80, 51), new HexCoord(79, 51), new HexCoord(40, 40), canonical);

            foreach (var phase in new[]
                     {
                         PlayerPartyDeparturePhase.Planned,
                         PlayerPartyDeparturePhase.Approaching,
                     })
            {
                m.SetDeparturePhase(phase);
                var beforeKind = m.LocationKind;
                var beforeSite = m.SiteId;
                var beforePos = m.WorldPosition;
                var beforeHex = m.CurrentHex;
                var beforeMoving = m.IsMoving;
                var beforeDep = m.DeparturePhase;

                PlayerPartyWorldLocationQuery.TryResolve(world, party, out _);

                Assert.AreEqual(beforeKind, m.LocationKind, "LocationKind 未变 (" + phase + ")");
                Assert.AreEqual(beforeSite, m.SiteId, "SiteId 未变 (" + phase + ")");
                Assert.AreEqual(beforePos, m.WorldPosition, "WorldPosition 未变 (" + phase + ")");
                Assert.AreEqual(beforeHex, m.CurrentHex, "CurrentHex 未变 (" + phase + ")");
                Assert.AreEqual(beforeMoving, m.IsMoving, "IsMoving 未变 (" + phase + ")");
                Assert.AreEqual(beforeDep, m.DeparturePhase, "DeparturePhase 未变 (" + phase + ")");
            }
        }

        // ============================ [7] Presence/Anchor 改变不影响 DepartureApproach marker ============================

        [Test]
        public void B6_1_07_PresenceAnchorChanges_DoNotAffectDepartureApproachMarker()
        {
            var (world, site, party) = BuildWorld();
            var canonical = new WorldVec2(141.6f, 76.5f);
            var m = BeginDeparture(
                world, site, party, new HexCoord(80, 51), new HexCoord(79, 51), new HexCoord(40, 40), canonical);
            m.SetDeparturePhase(PlayerPartyDeparturePhase.Approaching);

            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, party, out var before), "baseline");
            Assert.AreEqual(canonical, before.WorldPosition, "baseline: canonical");

            var oldPresence = site.PresenceHex;
            var oldAnchor = site.AnchorHex;
            site.PresenceHex = new HexCoord(5, 5);
            site.AnchorHex = new HexCoord(6, 6);

            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, party, out var after), "after");
            Assert.AreEqual(before.WorldPosition, after.WorldPosition,
                "Presence/Anchor 改变 → DepartureApproach marker 不变");
            Assert.AreEqual(canonical, after.WorldPosition, "仍是 Canonical");
            Assert.IsFalse(after.IsLegacyFallback, "not legacy fallback");

            site.PresenceHex = oldPresence;
            site.AnchorHex = oldAnchor;
        }
    }
}
