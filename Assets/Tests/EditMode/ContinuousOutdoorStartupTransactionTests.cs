using System.IO;
using NUnit.Framework;
using XianXia.Core.World.Surface;
using XianXia.Data.Content;

namespace XianXia.Tests.EditMode
{
    /// <summary>
    /// Continuous Outdoor **startup 事务** 回归：
    /// Prepare（只读解析 candidate）→ Preflight（neighborhood 可加载）→ Commit（提 canonical +
    /// 清 legacy authority）→ Activate；InitialBootstrap token 只在真正激活后消费。
    ///
    /// 这一层是纯 Data 层函数，所以能无头验证：位置真源优先级（baked SitePlace → SiteRegion
    /// arrival → legacy mapping）、preflight 只读、失败不得提交 authority／不得消费 token
    /// （投出 predicate 就是 Host 实际调用的那个）。
    /// </summary>
    public sealed class ContinuousOutdoorStartupTransactionTests
    {
        const string OpeningSiteId = "base:site_huangcun";
        const string OpeningLocationId = "base:loc_ref_labor_yard";
        const string UnknownLocationId = "base:loc_does_not_exist";
        const string MainSurfaceId = "base:surface_main_wilderness_v1";

        /// <summary>
        /// headless harness 注入的内容根（Unity Test Runner 下保持 null，行为完全不变）。
        /// </summary>
        public static string HeadlessContentRoot;

        /// <summary>
        /// BaseGame 内容根。注意：Unity 调用（Application.dataPath）必须隔离在独立方法里 ——
        /// 一个方法只要**包含** ECall，在 Unity 之外 JIT 就会失败（哪怕分支不执行），
        /// headless harness 就靠这个分离来跑同一份测试代码。
        /// </summary>
        static string BaseGamePath =>
            !string.IsNullOrEmpty(HeadlessContentRoot) ? HeadlessContentRoot : UnityDataPathBaseGame();

        static string UnityDataPathBaseGame() =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        static DefinitionRegistry LoadRegistry()
        {
            var loaded = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : string.Empty);
            return loaded.Value.Registry;
        }

        // ---- A 组：surface / region 解析 ----

        [Test]
        public void A01_OpeningSiteResolvesMainContinuousSurfaceAndPhysicalRegion()
        {
            var registry = LoadRegistry();
            Assert.IsTrue(ContinuousOutdoorStartupPlanner.TryResolveSurfaceForSite(
                registry, OpeningSiteId, out var surface, out var region));

            Assert.AreEqual(MainSurfaceId, surface.SurfaceId);
            Assert.IsFalse(surface.AcceptanceOnly, "Normal startup 绝不能用 AcceptanceOnly surface");
            Assert.IsNotNull(region);
            Assert.AreEqual(OpeningSiteId, region.SiteId);
        }

        [Test]
        public void A02_BakedSitePlaceWinsOverRegionArrival()
        {
            var registry = LoadRegistry();
            Assert.IsTrue(ContinuousOutdoorStartupPlanner.TryResolveSurfaceForSite(
                registry, OpeningSiteId, out var surface, out _));

            Assert.IsTrue(ContinuousOutdoorStartupPlanner.TryResolveBakedAnchor(
                surface, OpeningSiteId, OpeningLocationId,
                out var x, out var y, out var source));

            Assert.AreEqual(ContinuousOutdoorAnchorSource.BakedSitePlace, source);

            // 与 MainWildernessSurface 的 sitePlaces 真源一致（checked-in Continuous truth）。
            float expectedX = 0f, expectedY = 0f;
            var found = false;
            foreach (var place in surface.SitePlaces)
            {
                if (place.SiteId != OpeningSiteId || place.LocationId != OpeningLocationId)
                    continue;
                expectedX = place.WorldX;
                expectedY = place.WorldY;
                found = true;
                break;
            }

            Assert.IsTrue(found, "opening LocationId 必须有 baked SitePlace");
            Assert.AreEqual(expectedX, x, 1e-4f);
            Assert.AreEqual(expectedY, y, 1e-4f);
        }

        [Test]
        public void A03_FallsBackToRegionArrivalWhenLocationHasNoBakedPlace()
        {
            var registry = LoadRegistry();
            Assert.IsTrue(ContinuousOutdoorStartupPlanner.TryResolveSurfaceForSite(
                registry, OpeningSiteId, out var surface, out var region));

            Assert.IsTrue(ContinuousOutdoorStartupPlanner.TryResolveBakedAnchor(
                surface, OpeningSiteId, UnknownLocationId,
                out var x, out var y, out var source));

            Assert.AreEqual(ContinuousOutdoorAnchorSource.RegionArrival, source);
            Assert.AreEqual(region.ArrivalWorldX, x, 1e-4f);
            Assert.AreEqual(region.ArrivalWorldY, y, 1e-4f);
        }

        // ---- B 组：preflight ----

        [Test]
        public void B01_PreflightAcceptsOpeningChunkAndItsNeighbors()
        {
            var registry = LoadRegistry();
            ContinuousOutdoorStartupPlanner.TryResolveSurfaceForSite(
                registry, OpeningSiteId, out var surface, out _);
            ContinuousOutdoorStartupPlanner.TryResolveBakedAnchor(
                surface, OpeningSiteId, OpeningLocationId, out var x, out var y, out _);
            var chunk = ContinuousOutdoorStartupPlanner.WorldToChunk(surface, x, y);

            Assert.IsTrue(
                ContinuousOutdoorStartupPlanner.TryPreflightNeighborhood(
                    registry, surface, chunk, out var failure),
                failure);
            Assert.IsEmpty(failure);
        }

        [Test]
        public void B02_PreflightRejectsChunkOutsideCoverage()
        {
            var registry = LoadRegistry();
            ContinuousOutdoorStartupPlanner.TryResolveSurfaceForSite(
                registry, OpeningSiteId, out var surface, out _);

            Assert.IsFalse(
                ContinuousOutdoorStartupPlanner.TryPreflightNeighborhood(
                    registry, surface, new SurfaceChunkCoord(9999, 9999), out var failure));
            StringAssert.Contains("ChunkExists=false", failure);
        }

        [Test]
        public void B03_PreflightIsReadOnly()
        {
            var registry = LoadRegistry();
            ContinuousOutdoorStartupPlanner.TryResolveSurfaceForSite(
                registry, OpeningSiteId, out var surface, out _);
            ContinuousOutdoorStartupPlanner.TryResolveBakedAnchor(
                surface, OpeningSiteId, OpeningLocationId, out var x, out var y, out _);
            var chunk = ContinuousOutdoorStartupPlanner.WorldToChunk(surface, x, y);

            var chunks = surface.Chunks.Count;
            var places = surface.SitePlaces.Count;
            var regions = surface.SiteRegions.Count;
            var placements = surface.SitePlacements.Count;

            Assert.IsTrue(ContinuousOutdoorStartupPlanner.TryPreflightNeighborhood(registry, surface, chunk, out _));
            Assert.IsTrue(ContinuousOutdoorStartupPlanner.TryPreflightNeighborhood(registry, surface, chunk, out _));
            ContinuousOutdoorStartupPlanner.TryPreflightNeighborhood(
                registry, surface, new SurfaceChunkCoord(9999, 9999), out _);

            Assert.AreEqual(chunks, surface.Chunks.Count);
            Assert.AreEqual(places, surface.SitePlaces.Count);
            Assert.AreEqual(regions, surface.SiteRegions.Count);
            Assert.AreEqual(placements, surface.SitePlacements.Count);
        }

        // ---- C 组：prepare 结果的目标状态（§6 正常启动） ----

        [Test]
        public void C01_PreparedAnchorIsInsideCoverageAndOpeningSiteIsMaterializable()
        {
            var registry = LoadRegistry();
            ContinuousOutdoorStartupPlanner.TryResolveSurfaceForSite(
                registry, OpeningSiteId, out var surface, out _);
            ContinuousOutdoorStartupPlanner.TryResolveBakedAnchor(
                surface, OpeningSiteId, OpeningLocationId, out var x, out var y, out var source);
            var plan = new ContinuousOutdoorStartupPlanner.StartupPlan(
                OpeningSiteId, surface,
                ContinuousOutdoorStartupPlanner.WorldToChunk(surface, x, y),
                x, y, source);

            Assert.AreEqual(ContinuousOutdoorAnchorSource.BakedSitePlace, plan.AnchorSource);
            Assert.IsTrue(
                ContinuousOutdoorStartupPlanner.TryPreflightNeighborhood(
                    registry, surface, plan.Chunk, out var failure),
                failure);
            Assert.IsTrue(OutdoorSurfaceCoverageResolver.ContainsWorldPosition(surface, plan.CanonicalWorldX, plan.CanonicalWorldY),
                "startup canonical anchor 必须落在 Continuous surface coverage 内");

            // opening Site 的 baked placement 必须能在该 chunk 解析出来（否则会 materialize 出空 Site）。
            var placementReady = false;
            for (var i = 0; i < surface.SitePlacements.Count; i++)
            {
                var placement = surface.SitePlacements[i];
                if (placement == null || placement.SiteId != OpeningSiteId)
                    continue;
                if (placement.ChunkX != plan.Chunk.X || placement.ChunkY != plan.Chunk.Y)
                    continue;
                placementReady = true;
                break;
            }

            Assert.IsTrue(placementReady, "opening Site 在该 chunk 必须有 baked placement");
        }

        // ---- D 组：失败不得提交 authority／不得消费 token ----

        [Test]
        public void D01_PreflightFailureMustNotCommitAuthority()
        {
            // Host 的 commit 分支只认这个 predicate：prepare 成功但 preflight 失败 → 不 commit。
            Assert.IsFalse(ContinuousOutdoorStartupPlanner.ShouldCommitStartupAuthority(
                prepareSucceeded: true, preflightPassed: false));
            Assert.IsTrue(ContinuousOutdoorStartupPlanner.ShouldCommitStartupAuthority(
                prepareSucceeded: true, preflightPassed: true));
            Assert.IsFalse(ContinuousOutdoorStartupPlanner.ShouldCommitStartupAuthority(
                prepareSucceeded: false, preflightPassed: true));
        }

        [Test]
        public void D02_InitialBootstrapTokenIsConsumedOnlyAfterSurfaceActivation()
        {
            // 未 commit（preflight 失败）→ 不消费（token 保留，可 retry）。
            Assert.IsFalse(ContinuousOutdoorStartupPlanner.ShouldConsumeInitialBootstrap(
                startupCommitted: false, surfaceActivated: false));
            // 已 commit 但 activation 失败 → 仍不消费（本次修复的 green-field 场景）。
            Assert.IsFalse(ContinuousOutdoorStartupPlanner.ShouldConsumeInitialBootstrap(
                startupCommitted: true, surfaceActivated: false));
            // 只有真正激活成功才消费。
            Assert.IsTrue(ContinuousOutdoorStartupPlanner.ShouldConsumeInitialBootstrap(
                startupCommitted: true, surfaceActivated: true));
        }

        [Test]
        public void D03_LegacyFallbackIsExplicitlyLastResort()
        {
            // surface/region 解析成功 + location 无 baked place → 仍走 RegionArrival（不是 legacy）。
            var registry = LoadRegistry();
            ContinuousOutdoorStartupPlanner.TryResolveSurfaceForSite(
                registry, OpeningSiteId, out var surface, out _);
            ContinuousOutdoorStartupPlanner.TryResolveBakedAnchor(
                surface, OpeningSiteId, UnknownLocationId, out _, out _, out var source);
            Assert.AreNotEqual(ContinuousOutdoorAnchorSource.LegacyMapping, source);
            Assert.AreNotEqual(ContinuousOutdoorAnchorSource.None, source);
        }
    }
}
