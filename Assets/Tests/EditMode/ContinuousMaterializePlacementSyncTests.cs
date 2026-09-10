using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Surface;
using XianXia.Data.Bootstrap;
using XianXia.Data.Content;
using XianXia.Unity.Host;

namespace XianXia.Tests.EditMode
{
    /// <summary>
    /// Continuous Outdoor 呈现落点收口（materialize 权威落点 → 已存在 view 的对齐）。
    ///
    /// 制作人复验 blocker（诊断已排除 population 数量问题）：
    /// <c>Expected=17 Materialized=17 Views=17 SpatialValid=1</c>，可见的只有 1 人；
    /// 16 个 view 的 <c>ViewWorld</c> 全在 presentation 原点附近（间距恰好是 0.85 的 stack 网格）。
    ///
    /// 根因（结构证据 + 实测数值）：view 在 Continuous activation <b>之前</b> 由
    /// <c>EntityViewSpawner.Rebuild</c> 创建，那时只能用 legacy WorldRegion 地点 presentation +
    /// stack 偏移；materialize 随后写入权威 <c>PresentationOverride</c>，但
    /// <c>SpawnMissingVisibleViews</c> 只补缺失 view、绝不搬动已存在的 view。
    /// </summary>
    public sealed class ContinuousMaterializePlacementSyncTests
    {
        const string ScenarioId = "base:scenario_ch01_reference";
        const string SiteId = "base:site_huangcun";
        const string SurfaceId = "base:surface_main_wilderness_v1";
        const string ProtagonistDefinitionId = "base:character_protagonist";
        const string CompanionADefinitionId = "base:character_companion_a";
        const string CompanionBDefinitionId = "base:character_companion_b";
        const string LaborYardLocationId = "base:loc_ref_labor_yard";

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

        static EntityId FindByDefinition(SimulationWorld world, string definitionId)
        {
            foreach (var entity in world.Entities.All)
                if (string.Equals(entity.DefinitionId.ToString(), definitionId, StringComparison.Ordinal))
                    return entity.Id;
            return EntityId.None;
        }

        /// <summary>
        /// 复刻 <c>EntityViewSpawner.ResolvePresentationPosition</c> 的 legacy 回退分支
        /// （activation 之前的 view 只能走这一条）：location presentation + stack 偏移。
        /// </summary>
        static bool TryLegacyFallbackPresentation(
            SimulationWorld world, EntityId id, int stackIndex, out float px, out float py)
        {
            px = py = 0f;
            if (!world.Entities.TryGet(id, out var entity) ||
                !entity.TryGet<EntityLocationComponent>(out var loc) || loc == null ||
                !loc.HasLocation ||
                !world.WorldRegion.TryGet(loc.LocationId, out var location) || location == null)
                return false;
            px = location.PresentationX + (stackIndex % 3) * 0.85f - 0.85f;
            py = location.PresentationZ + (stackIndex / 3) * 0.85f;
            return true;
        }

        static WorldVec2 AuthoritativeAnchor(SimulationWorld world, OutdoorWorldSurfaceDefinition surface,
            EntityId id, out string locationId)
        {
            Assert.IsTrue(world.Entities.TryGet(id, out var entity));
            locationId = entity.TryGet<EntityLocationComponent>(out var loc) && loc != null
                ? loc.LocationId ?? string.Empty
                : string.Empty;
            var source = ContinuousOutdoorOpeningAnchorResolver.ResolveInitialPlacement(
                surface, SiteId, entity.DefinitionId.ToString(), locationId,
                false, 0f, 0f, out var anchor, out _, out _);
            Assert.AreNotEqual(OpeningInitialPlacementSource.SiteArrivalFallback, source,
                "opening entity 必须能解析到 baked 真源");
            return anchor;
        }

        // ---------------------------------------------------------------- H1
        /// <summary>
        /// 本轮核心：opening resident 的「activation 前 legacy 回退 view」与权威 baked 落点
        /// 在 presentation 空间相差极大（数十~数百单位，即镜头外），因此必须被重新对齐；
        /// 对齐目标恰好等于 baked anchor 的 presentation。
        /// </summary>
        [Test]
        public void H1_LegacyFallbackViewMustBeRealignedToBakedAnchorPresentation()
        {
            var boot = Boot();
            var world = boot.World;
            var surface = Surface(boot.Registry);
            var mapper = Mapper(surface);
            var id = FindByDefinition(world, CompanionADefinitionId);
            Assert.AreNotEqual(EntityId.None, id);

            var anchor = AuthoritativeAnchor(world, surface, id, out var locationId);
            mapper.WorldToPresentation(anchor.X, anchor.Y, out var targetX, out var targetY);

            Assert.IsTrue(TryLegacyFallbackPresentation(world, id, 0, out var viewX, out var viewY),
                "该 opening resident 必须能走 legacy 回退分支（本 bug 的前提）");

            var distance = Math.Sqrt(
                (targetX - viewX) * (targetX - viewX) + (targetY - viewY) * (targetY - viewY));
            Assert.Greater(distance, 10f,
                "legacy 回退 view 与权威落点必须明显不同（实测差距远大于 10 个 presentation 单位），" +
                "否则本 bug 不成立：location=" + locationId + " legacy=(" + viewX + "," + viewY +
                ") target=(" + targetX + "," + targetY + ")");

            Assert.IsTrue(ContinuousMaterializePlacementSync.ShouldRealign(
                    hasView: true,
                    isPartyMember: false,
                    isMoving: false,
                    hasAuthoritativePlacement: true,
                    viewPresentationX: viewX,
                    viewPresentationY: viewY,
                    targetPresentationX: targetX,
                    targetPresentationY: targetY),
                "已存在的 stale view 必须被重新对齐到权威落点");

            // 对齐后：view 的 presentation 就是 baked anchor 的 presentation（往返一致）。
            mapper.PresentationToWorld(targetX, targetY, out var roundTripX, out var roundTripY);
            Assert.AreEqual(anchor.X, roundTripX, 1e-3);
            Assert.AreEqual(anchor.Y, roundTripY, 1e-3);
            Assert.IsTrue(ContinuousOutdoorOpeningAnchorResolver.IsInsideSiteBakedEnvelope(
                surface, SiteId, anchor.X, anchor.Y));
            Assert.IsFalse(ContinuousMaterializePlacementSync.ShouldRealign(
                    true, false, false, true, targetX, targetY, targetX, targetY),
                "对齐后不得再产生重复写入");
        }

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

        // ---------------------------------------------------------------- H7
        /// <summary>
        /// 真实内容：12 名共享 labor_yard 的 opening entity 在 legacy 回退下会压进同一地点网格，
        /// 而权威 baked anchor 互不重合 —— 即「对齐」这一步是必需而不是可选。
        /// </summary>
        [Test]
        public void H7_SharedLocationEntitiesDifferOnlyByBakedAnchor()
        {
            var boot = Boot();
            var world = boot.World;
            var surface = Surface(boot.Registry);
            var mapper = Mapper(surface);

            var shared = new List<EntityId>();
            foreach (var entity in world.Entities.All)
            {
                if (!entity.TryGet<EntityLocationComponent>(out var loc) || loc == null ||
                    !string.Equals(loc.LocationId, LaborYardLocationId, StringComparison.Ordinal))
                    continue;
                if (world.WorldPresence.TryGet(entity.Id, out var presence) &&
                    presence.Mode == PartyWorldPresenceMode.AtSite &&
                    string.Equals(presence.SiteId, SiteId, StringComparison.Ordinal))
                    shared.Add(entity.Id);
            }

            Assert.Greater(shared.Count, 5,
                "ch01_reference 开局有大量共享 labor_yard 的 opening entity（本 bug 的规模前提）");

            var authoritative = new HashSet<string>();
            foreach (var id in shared)
            {
                var anchor = AuthoritativeAnchor(world, surface, id, out _);
                mapper.WorldToPresentation(anchor.X, anchor.Y, out var px, out var py);
                authoritative.Add(px.ToString("F5") + "|" + py.ToString("F5"));
            }

            Assert.AreEqual(shared.Count, authoritative.Count,
                "共享同一个 LocationId 的实体必须靠 baked anchor 拉开，而不是靠 presentation stack 偏移");
        }
    }
}
