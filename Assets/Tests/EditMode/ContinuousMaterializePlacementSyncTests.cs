using System;
using System.IO;
using NUnit.Framework;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Surface;
using XianXia.Data.Bootstrap;
using XianXia.Data.Content;
using XianXia.Unity.Host;

namespace XianXia.Tests.EditMode
{
    /// <summary>
    /// Continuous Outdoor 呈现落点收口（materialize 权威落点 → 已存在 view 的对齐）。
    /// </summary>
    public sealed class ContinuousMaterializePlacementSyncTests
    {
        const string ScenarioId = "base:scenario_ch01_reference";
        const string SiteId = "base:site_huangcun";
        const string SurfaceId = "base:surface_main_wilderness_v1";

        static string BaseGamePath()
        {
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));
#else
            var fromEnvironment = Environment.GetEnvironmentVariable("XIANXIA_BASEGAME");
            return string.IsNullOrEmpty(fromEnvironment)
                ? Path.GetFullPath(Path.Combine("Content", "BaseGame"))
                : fromEnvironment;
#endif
        }

        static PlayableDayBootstrapResult Boot()
        {
            var loaded = new ContentPackageLoader().Load(new[] { BaseGamePath() });
            Assert.IsTrue(loaded.IsSuccess,
                loaded.IsFailure ? loaded.Error.ToString() : string.Empty);
            var started = new PlayableDayBootstrap().Start(loaded.Value, new PlayableDayOptions
            {
                DailyRequiredAmount = 10,
                OpeningScenarioId = ScenarioId
            });
            Assert.IsTrue(started.IsSuccess,
                started.IsFailure ? started.Error.ToString() : string.Empty);
            return started.Value;
        }

        static OutdoorWorldSurfaceDefinition Surface(DefinitionRegistry registry)
        {
            foreach (var entry in registry.OutdoorSurfaces)
                if (entry.Value != null &&
                    string.Equals(entry.Value.SurfaceId, SurfaceId, StringComparison.Ordinal))
                    return entry.Value;
            Assert.Fail("main continuous surface missing");
            return null;
        }

        static OutdoorSurfaceCoordinateMapper Mapper(OutdoorWorldSurfaceDefinition surface) =>
            new OutdoorSurfaceCoordinateMapper(
                surface.ChunkWidth, surface.ChunkHeight, surface.CellSize,
                presentationUnitsPerWorldUnit: 1f / surface.CellSize,
                originWorldX: surface.OriginWorldX, originWorldY: surface.OriginWorldY);

        // ---------------------------------------------------------------- H2
        /// <summary>§15：正在被 movement 驱动的实体不得被 authored anchor 重置。</summary>
        [Test]
        public void H2_MovingEntityIsNeverRealigned()
        {
            Assert.IsFalse(ContinuousMaterializePlacementSync.ShouldRealign(
                true, false, isMoving: true, true, 0f, 0f, 500f, 500f));
        }

        // ---------------------------------------------------------------- H3
        /// <summary>PlayerParty 成员由 AlignPartyPresentationToWorld 负责，不在此处搬动。</summary>
        [Test]
        public void H3_PartyMemberIsNeverRealigned()
        {
            Assert.IsFalse(ContinuousMaterializePlacementSync.ShouldRealign(
                true, isPartyMember: true, false, true, 0f, 0f, 500f, 500f));
        }

        // ---------------------------------------------------------------- H4
        /// <summary>已对齐（含容差内）不产生写入；超出容差才对齐。</summary>
        [Test]
        public void H4_AlreadyAlignedViewIsNotTouched()
        {
            const float epsilon = ContinuousMaterializePlacementSync.PositionEpsilon;
            Assert.IsFalse(ContinuousMaterializePlacementSync.ShouldRealign(
                true, false, false, true, 100f, 100f, 100f, 100f));
            Assert.IsFalse(ContinuousMaterializePlacementSync.ShouldRealign(
                true, false, false, true, 100f, 100f, 100f + epsilon * 0.5f, 100f));
            Assert.IsTrue(ContinuousMaterializePlacementSync.ShouldRealign(
                true, false, false, true, 100f, 100f, 100f + epsilon * 10f, 100f));
        }

        // ---------------------------------------------------------------- H5
        /// <summary>没有权威落点时不猜位置（保持既有 fallback 语义）。</summary>
        [Test]
        public void H5_EntityWithoutAuthoritativePlacementIsNotMoved()
        {
            Assert.IsFalse(ContinuousMaterializePlacementSync.ShouldRealign(
                true, false, false, hasAuthoritativePlacement: false, 0f, 0f, 500f, 500f));
            Assert.IsFalse(ContinuousMaterializePlacementSync.ShouldRealign(
                hasView: false, false, false, true, 0f, 0f, 500f, 500f));
        }

        // ---------------------------------------------------------------- H6
        /// <summary>
        /// 真实内容：全部 opening anchor 都落在「玩家当前 chunk ±1」的已加载邻域内。
        /// 这保证修复后 <c>ViewInsideLoadedChunks</c> 成立（§17-G 的 InLoaded 前提）。
        /// </summary>
        [Test]
        public void H6_EveryOpeningAnchorLandsInsidePlayersLoadedNeighborhood()
        {
            var boot = Boot();
            var world = boot.World;
            var surface = Surface(boot.Registry);
            var mapper = Mapper(surface);

            var motion = world.PlayerPartyTravel;
            Assert.IsTrue(motion.HasPosition);
            var partyChunk = mapper.WorldToChunk(motion.WorldPosition.X, motion.WorldPosition.Y);

            Assert.Greater(surface.OpeningEntityAnchors.Count, 0);
            foreach (var anchor in surface.OpeningEntityAnchors)
            {
                if (anchor == null || !string.Equals(anchor.SiteId, SiteId, StringComparison.Ordinal))
                    continue;
                var chunk = mapper.WorldToChunk(anchor.WorldX, anchor.WorldY);
                Assert.LessOrEqual(Math.Abs(chunk.X - partyChunk.X), 1,
                    "anchor " + anchor.SpawnKey + " 超出玩家 chunk 邻域 X");
                Assert.LessOrEqual(Math.Abs(chunk.Y - partyChunk.Y), 1,
                    "anchor " + anchor.SpawnKey + " 超出玩家 chunk 邻域 Y");
            }
        }

    }
}
