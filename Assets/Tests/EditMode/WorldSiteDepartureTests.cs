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
    /// Phase 5R-B6：WorldSite Departure / LocalVisible Egress Execution。
    /// 覆盖：DeparturePlan 形成（route first outside hex）、formal SurfaceExitConnection、
    /// WorldMap open 不虚拟推进、close 后 LocalVisible approach（B4 继续）、
    /// TransitionCommit 停 B4、正式 egress（BoundaryContactWorld + route 继续）、
    /// override/Stop 取消、reopen/换目标、同 Site 目标拒绝、失败不 teleport、CurrentHex 不重构。
    /// 真实 fixture：ch01_hex_world.json（base:site_huangcun，4-Hex footprint）。
    /// </summary>
    public sealed class WorldSiteDepartureTests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        const float HexSize = 1f;

        // Phase 5R-B6.2：TryResolveFormalExitConnection 需真实 LocalMap bounds（huangcun_01.json：
        // origin(-40,-25) cellSize=1 80×50）——SlotRect 与 presenter 视觉方块同源。
        static readonly WildernessLocalWorldProjection.WildernessLocalMapBounds TestBounds =
            WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(-40f, -25f, 1f, 80, 50);

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

        static bool B4Allowed(PlayerPartyWorldMotion m)
        {
            var ctx = new WorldSiteLocalVisibleSyncContext(
                inputBlocked: false,
                isWorldMapOpen: false,
                hasActiveView: true,
                isAtWorldSite: m.LocationKind == PlayerPartyLocationKind.AtWorldSite,
                hasSiteId: !string.IsNullOrEmpty(m.SiteId),
                isDepartureTransitionCommit: m.DeparturePhase == PlayerPartyDeparturePhase.TransitionCommit,
                usesTravelPresentation: m.UsesTravelPresentation,
                isMaterializeHeld: false,
                hasGeometry: true);
            return WorldSiteLocalVisibleSyncPolicy.CanSync(ctx);
        }

        // ============================ [1] DeparturePlan 形成 ============================

        [Test]
        public void B6_01_AtWorldSite_RouteFirstLegOutside_CreatesDeparturePlan()
        {
            var (world, site, party) = BuildWorld();
            var fp0 = new HexCoord(80, 51);
            var outsideWest = new HexCoord(79, 51);
            var goalFar = new HexCoord(40, 40);

            var m = BeginDeparture(world, site, party, fp0, outsideWest, goalFar, new WorldVec2(138.2f, 76.5f));

            Assert.IsTrue(m.IsMoving, "AutoTravel active");
            Assert.IsTrue(m.IsSiteDeparturePending, "IsSiteDeparturePending");
            Assert.AreEqual(PlayerPartyDeparturePhase.Planned, m.DeparturePhase, "phase == Planned");
            Assert.AreEqual(outsideWest, m.SiteDepartureExitHex, "exitHex == first outside hex");
            Assert.AreEqual(fp0, m.SiteDepartureFootprintHex, "footprintHex == fp0");
        }

        [Test]
        public void B6_02_DepartureUsesRouteFirstOutsideHex_NotAnchorPresence()
        {
            var (world, site, party) = BuildWorld();
            var fp0 = new HexCoord(80, 51);
            var outsideWest = new HexCoord(79, 51);
            var goalFar = new HexCoord(40, 40);

            var m = BeginDeparture(world, site, party, fp0, outsideWest, goalFar, new WorldVec2(138.2f, 76.5f));

            Assert.AreNotEqual(site.AnchorHex, m.SiteDepartureExitHex, "exit != Anchor");
            Assert.AreNotEqual(site.PresenceHex, m.SiteDepartureExitHex, "exit != Presence");
            Assert.AreEqual(outsideWest, m.SiteDepartureExitHex, "exit == route first outside hex");
        }

        [Test]
        public void B6_03_FormalSurfaceExitConnection_Resolves()
        {
            var (world, site, _) = BuildWorld();
            var fp0 = new HexCoord(80, 51);
            var outsideWest = new HexCoord(79, 51);

            Assert.IsTrue(
                WorldSiteFootprintExitConnectionResolver.TryResolveFormalExitConnection(
                    world, site, fp0, outsideWest, HexSize, TestBounds, out var conn),
                "resolve exit connection");
            Assert.AreEqual(fp0, conn.SourceHex, "conn source == fp0");
            Assert.AreEqual(outsideWest, conn.DestinationHex, "conn dest == outsideWest");
            Assert.IsTrue(conn.BoundaryContactWorldX != 0f || conn.BoundaryContactWorldY != 0f,
                "boundary contact present");
        }

        // ============================ [2] WorldMap open：plan 不 teleport ============================

        [Test]
        public void B6_04_WorldMapOpen_WorldExecutor_AdvancesCanonicalToBoundary()
        {
            var (world, site, party) = BuildWorld();
            var fp0 = new HexCoord(80, 51);
            var outsideWest = new HexCoord(79, 51);
            var goalFar = new HexCoord(40, 40);

            var m = BeginDeparture(world, site, party, fp0, outsideWest, goalFar, new WorldVec2(138.2f, 76.5f));
            var before = m.WorldPosition;
            var exitHex = m.SiteDepartureExitHex;
            Assert.IsTrue(m.IsSiteDeparturePending, "plan exists after Begin");

            // Phase 5R-B6.5-B：WorldMap open（ExecutionMode=World）→ World executor 推进 Canonical
            // 朝正式 BoundaryContactWorld（唯一 physical truth），到达后正式 egress commit
            // （AtWorldPosition + route 对齐 DestinationHex）。不再抑制（旧 B6 语义）。
            var target = m.SiteDepartureBoundaryEntry;
            var dist = WorldVec2.Distance(before, target);
            Assert.IsTrue(dist > 0.001f, "formal boundary distinct from canonical");

            PlayerPartyHexTravelService.AdvanceDistanceBudget(world, dist * 0.3f);
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldSite, m.LocationKind,
                "not committed before boundary");
            Assert.IsTrue(
                WorldVec2.Distance(before, m.WorldPosition) > 0.01f,
                "canonical advanced toward boundary");
            Assert.IsTrue(m.IsMoving, "still moving");

            // 精确预算直达 boundary：恰好 commit（不递归推进后续段），验证 commit 瞬间 route hex。
            PlayerPartyHexTravelService.AdvanceDistanceBudget(world, dist + 0.001f);
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldPosition, m.LocationKind,
                "committed at boundary (egress)");
            Assert.AreEqual(exitHex, m.CurrentHex, "route hex = exit hex (no WorldToHex tie)");
            Assert.IsTrue(m.IsMoving, "route continues after commit");
        }

        // ============================ [3] Close → LocalVisible approach（B4 继续） ============================

        [Test]
        public void B6_05_WorldMapClose_LocalDepartureApproachStarts()
        {
            var (world, site, party) = BuildWorld();
            var fp0 = new HexCoord(80, 51);
            var outsideWest = new HexCoord(79, 51);
            var goalFar = new HexCoord(40, 40);

            var m = BeginDeparture(world, site, party, fp0, outsideWest, goalFar, new WorldVec2(138.2f, 76.5f));
            // CloseWorldMapTakeover: IsMoving → ExecutionMode=LocalVisible（Host 驱动层语义）
            m.SetExecutionMode(PlayerPartyTravelExecutionMode.LocalVisible);

            Assert.AreEqual(PlayerPartyTravelExecutionMode.LocalVisible, m.ExecutionMode, "LocalVisible");
            Assert.IsTrue(B4Allowed(m), "B4 allowed during approach (phase=Planned)");
        }

        [Test]
        public void B6_07_Approach_KeepsAtWorldSite_AndSiteId()
        {
            var (world, site, party) = BuildWorld();
            var fp0 = new HexCoord(80, 51);
            var outsideWest = new HexCoord(79, 51);
            var goalFar = new HexCoord(40, 40);

            var m = BeginDeparture(world, site, party, fp0, outsideWest, goalFar, new WorldVec2(138.2f, 76.5f));
            m.SetExecutionMode(PlayerPartyTravelExecutionMode.LocalVisible);
            m.SetDeparturePhase(PlayerPartyDeparturePhase.Approaching);

            Assert.AreEqual(PlayerPartyLocationKind.AtWorldSite, m.LocationKind, "LocationKind AtWorldSite");
            Assert.AreEqual(site.SiteId, m.SiteId, "SiteId unchanged");
        }

        [Test]
        public void B6_08_Approach_B4SyncAllowed()
        {
            var (world, site, party) = BuildWorld();
            var fp0 = new HexCoord(80, 51);
            var outsideWest = new HexCoord(79, 51);
            var goalFar = new HexCoord(40, 40);

            var m = BeginDeparture(world, site, party, fp0, outsideWest, goalFar, new WorldVec2(138.2f, 76.5f));
            m.SetExecutionMode(PlayerPartyTravelExecutionMode.LocalVisible);
            m.SetDeparturePhase(PlayerPartyDeparturePhase.Approaching);

            Assert.IsTrue(B4Allowed(m), "B4 allowed during Approaching");
        }

        [Test]
        public void B6_09_TransitionCommit_B4SyncBlocked()
        {
            var (world, site, party) = BuildWorld();
            var fp0 = new HexCoord(80, 51);
            var outsideWest = new HexCoord(79, 51);
            var goalFar = new HexCoord(40, 40);

            var m = BeginDeparture(world, site, party, fp0, outsideWest, goalFar, new WorldVec2(138.2f, 76.5f));
            m.SetExecutionMode(PlayerPartyTravelExecutionMode.LocalVisible);
            m.SetDeparturePhase(PlayerPartyDeparturePhase.TransitionCommit);

            Assert.IsFalse(B4Allowed(m), "B4 blocked at TransitionCommit");
        }

        // ============================ [4] egress commit ============================

        [Test]
        public void B6_10_11_12_EgressCommit_AtWorldPosition_BoundaryContact_RouteContinues()
        {
            var (world, site, party) = BuildWorld();
            var fp0 = new HexCoord(80, 51);
            var outsideWest = new HexCoord(79, 51);
            var goalFar = new HexCoord(40, 40);

            var m = BeginDeparture(world, site, party, fp0, outsideWest, goalFar, new WorldVec2(138.2f, 76.5f));
            m.SetExecutionMode(PlayerPartyTravelExecutionMode.LocalVisible);
            m.SetDeparturePhase(PlayerPartyDeparturePhase.Approaching);

            Assert.IsTrue(
                WorldSiteFootprintExitConnectionResolver.TryResolveFormalExitConnection(
                    world, site, fp0, outsideWest, HexSize, TestBounds, out var conn),
                "resolve exit conn");

            m.SetDeparturePhase(PlayerPartyDeparturePhase.TransitionCommit);
            var cross = PlayerPartyLocalVisibleAutoTravelService
                .TryCrossWorldSiteEdgePreservingLocalVisibleAutoTravel(world, party, conn);
            Assert.IsTrue(cross.IsSuccess, "egress success" + (cross.IsSuccess ? string.Empty : " " + cross.Error));

            Assert.AreEqual(PlayerPartyLocationKind.AtWorldPosition, m.LocationKind, "AtWorldPosition after egress");
            Assert.AreEqual(conn.BoundaryContactWorldX, m.WorldPosition.X, 1e-4f, "canonical X == BoundaryContactWorld");
            Assert.AreEqual(conn.BoundaryContactWorldY, m.WorldPosition.Y, 1e-4f, "canonical Y == BoundaryContactWorld");
            Assert.IsFalse(m.IsSiteDeparturePending, "departure cleared");

            Assert.IsTrue(m.HexPathCount >= 3, "route preserved (hexPath count=" + m.HexPathCount + ")");
            Assert.AreEqual(goalFar, m.DestinationHex, "destination preserved");
            Assert.IsTrue(m.IsMoving, "AutoTravel preserved");
            Assert.AreEqual(1, m.SegmentIndex, "segment advanced to exit->goal");
        }

        // ============================ [5] override / Stop ============================

        [Test]
        public void B6_13_WasdOverride_CancelClearsDeparture_KeepsCanonical()
        {
            var (world, site, party) = BuildWorld();
            var fp0 = new HexCoord(80, 51);
            var outsideWest = new HexCoord(79, 51);
            var goalFar = new HexCoord(40, 40);

            var m = BeginDeparture(world, site, party, fp0, outsideWest, goalFar, new WorldVec2(138.2f, 76.5f));
            var canonicalBefore = m.WorldPosition;
            m.SetExecutionMode(PlayerPartyTravelExecutionMode.LocalVisible);
            m.SetDeparturePhase(PlayerPartyDeparturePhase.Approaching);

            var cancel = PlayerPartyHexTravelService.CancelTravel(world);
            Assert.IsTrue(cancel.IsSuccess, "CancelTravel success");
            Assert.IsFalse(m.IsMoving, "not moving after cancel");
            Assert.IsFalse(m.IsSiteDeparturePending, "departure cleared");
            Assert.AreEqual(PlayerPartyDeparturePhase.None, m.DeparturePhase, "phase == None");
            Assert.AreEqual(canonicalBefore, m.WorldPosition, "canonical preserved (no snap)");
        }

        [Test]
        public void B6_15_WorldMapReopen_SwitchesToWorldExecutor()
        {
            var (world, site, party) = BuildWorld();
            var fp0 = new HexCoord(80, 51);
            var outsideWest = new HexCoord(79, 51);
            var goalFar = new HexCoord(40, 40);

            var m = BeginDeparture(world, site, party, fp0, outsideWest, goalFar, new WorldVec2(138.2f, 76.5f));
            m.SetExecutionMode(PlayerPartyTravelExecutionMode.LocalVisible);
            m.SetDeparturePhase(PlayerPartyDeparturePhase.Approaching);
            var canonicalBefore = m.WorldPosition;

            PlayerPartyHexTravelService.ResumeWorldTravelExecutionIfNeeded(world);

            // Phase 5R-B6.5-B：WorldMap reopen → World executor（departure 由 World executor 推进
            // Canonical 朝正式 BoundaryContact；WorldMap open 强制 ManualPaused，Resume 后 tick 即推进）。
            Assert.AreEqual(PlayerPartyTravelExecutionMode.World, m.ExecutionMode,
                "reopen switches to World executor");
            Assert.AreEqual(canonicalBefore, m.WorldPosition,
                "canonical preserved at switch (advance only on tick)");
            Assert.IsTrue(m.IsSiteDeparturePending, "departure plan persists");
        }

        [Test]
        public void B6_16_WorldMapChangeDestination_PlanUpdated()
        {
            var (world, site, party) = BuildWorld();
            var fp0 = new HexCoord(80, 51);
            var outsideWest = new HexCoord(79, 51);
            var goalFar = new HexCoord(40, 40);
            var exit2 = new HexCoord(82, 52);
            var goal2 = new HexCoord(120, 60);

            BeginDeparture(world, site, party, fp0, outsideWest, goalFar, new WorldVec2(138.2f, 76.5f));
            var m = world.PlayerPartyTravel;
            m.BeginSiteDepartureTravel(
                new List<HexCoord> { fp0, exit2, goal2 }, goal2, string.Empty,
                fp0, exit2, new WorldVec2(139.8f, 77.2f), new WorldVec2(142.0f, 78.0f),
                HexTravelMode.Ground, HexSize);

            Assert.AreEqual(exit2, m.SiteDepartureExitHex, "exit updated to new route");
            Assert.AreEqual(goal2, m.DestinationHex, "destination updated");
        }

        // ============================ [6] 同 Site / 失败 ============================

        [Test]
        public void B6_17_SameSiteTarget_NoDeparture()
        {
            var (world, site, party) = BuildWorld();
            var fp0 = new HexCoord(80, 51);
            var fp1 = new HexCoord(81, 52); // 仍在 footprint 内
            SetAtSite(world, site, party, fp0, new WorldVec2(138.2f, 76.5f));

            var result = PlayerPartyHexTravelService.BeginTravel(world, party, fp1);

            Assert.IsTrue(result.IsFailure, "BeginTravel rejected for same-site target");
            Assert.IsFalse(world.PlayerPartyTravel.IsSiteDeparturePending, "no departure created");
            Assert.IsFalse(world.PlayerPartyTravel.IsMoving, "no AutoTravel");
        }

        [Test]
        public void B6_18_NoConnection_NoTeleport_CanonicalUnchanged()
        {
            var (world, site, party) = BuildWorld();
            var fp0 = new HexCoord(80, 51);
            SetAtSite(world, site, party, fp0, new WorldVec2(138.2f, 76.5f));
            var before = world.PlayerPartyTravel.WorldPosition;

            var ok = WorldSiteFootprintExitConnectionResolver.TryResolveFormalExitConnection(
                world, site, fp0, new HexCoord(100, 100), HexSize, TestBounds, out _);

            Assert.IsFalse(ok, "non-connection rejected");
            Assert.AreEqual(before, world.PlayerPartyTravel.WorldPosition, "canonical unchanged");
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldSite, world.PlayerPartyTravel.LocationKind, "still AtWorldSite");
        }

        [Test]
        public void B6_19_CurrentHex_NotRewritten()
        {
            var (world, site, party) = BuildWorld();
            var fp0 = new HexCoord(80, 51);
            var outsideWest = new HexCoord(79, 51);
            var goalFar = new HexCoord(40, 40);

            var m = BeginDeparture(world, site, party, fp0, outsideWest, goalFar, new WorldVec2(138.2f, 76.5f));
            m.SetExecutionMode(PlayerPartyTravelExecutionMode.LocalVisible);

            // B6 不写 CurrentHex：path 起点仍是 footprint hex、段 0 为 footprint 内段
            Assert.AreEqual(fp0, m.HexPath[0], "path origin footprint hex");
            Assert.AreEqual(0, m.SegmentIndex, "segment 0 (footprint 内段)");
        }

        [Test]
        public void B6_20_IngressConnection_StillResolves()
        {
            var (world, site, _) = BuildWorld();
            var fp0 = new HexCoord(80, 51);
            var outsideWest = new HexCoord(79, 51);

            Assert.IsTrue(
                WorldSiteFootprintExitConnectionResolver.TryResolveFormalIngressConnection(
                    world, site, fp0, outsideWest, HexSize, out _),
                "ingress connection still resolves (ingress/SafeLanding chain untouched)");
        }
    }
}
