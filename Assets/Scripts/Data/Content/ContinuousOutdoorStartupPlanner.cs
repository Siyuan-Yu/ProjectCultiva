using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.World.Surface;

namespace XianXia.Data.Content
{
    /// <summary>
    /// Continuous Outdoor startup 位置来源。Normal NewGame 优先使用 checked-in Continuous
    /// truth（baked SitePlace / SiteRegion arrival）；LegacyMapping 仅作旧 content 兼容。
    /// </summary>
    public enum ContinuousOutdoorAnchorSource
    {
        None = 0,
        /// <summary>sitePlaces[] 的 authored worldX/worldY（checked-in Continuous truth）。</summary>
        BakedSitePlace = 1,
        /// <summary>siteRegions[] 的 arrivalWorldX/arrivalWorldY。</summary>
        RegionArrival = 2,
        /// <summary>Resolve checked-in Continuous Surface startup geometry.</summary>
        LegacyMapping = 3
    }

    /// <summary>
    /// Normal Continuous Outdoor startup 的 prepare / preflight。
    ///
    /// 这一层【绝不修改任何 Runtime authority】：调用方先 Prepare（只读解析 candidate
    /// surface / chunk / canonical anchor），再 Preflight（确认 neighborhood 可加载），
    /// 全部成功后才 Commit 并消费 InitialBootstrap token。这样 activation 失败不会留下
    /// 「Legacy 已销毁 + Continuous 未建立」的不可恢复 half-state。
    ///
    /// 与 activation 共用同一份 source/neighborhood 规则（<see cref="ContinuousOutdoorSurfaceRuntime"/>
    /// 委托到此），避免「preflight 说可以、activation 又不行」的双真源。
    /// </summary>
    public static class ContinuousOutdoorStartupPlanner
    {
        public readonly struct StartupPlan
        {
            public StartupPlan(
                string siteId,
                OutdoorWorldSurfaceDefinition surface,
                SurfaceChunkCoord chunk,
                float canonicalWorldX,
                float canonicalWorldY,
                ContinuousOutdoorAnchorSource anchorSource)
            {
                SiteId = siteId;
                Surface = surface;
                Chunk = chunk;
                CanonicalWorldX = canonicalWorldX;
                CanonicalWorldY = canonicalWorldY;
                AnchorSource = anchorSource;
            }

            public string SiteId { get; }
            public OutdoorWorldSurfaceDefinition Surface { get; }
            public SurfaceChunkCoord Chunk { get; }
            public float CanonicalWorldX { get; }
            public float CanonicalWorldY { get; }
            public ContinuousOutdoorAnchorSource AnchorSource { get; }
            public string SurfaceId => Surface?.SurfaceId ?? string.Empty;
        }

        /// <summary>该 Site 所属的 Continuous Surface 与它的 PhysicalRegion（非 AcceptanceOnly）。</summary>
        public static bool TryResolveSurfaceForSite(
            DefinitionRegistry registry,
            string siteId,
            out OutdoorWorldSurfaceDefinition surface,
            out WorldSitePhysicalRegionDefinition region)
        {
            surface = null;
            region = null;
            if (registry == null || string.IsNullOrEmpty(siteId))
                return false;
            foreach (var entry in registry.OutdoorSurfaces)
            {
                var candidate = entry.Value;
                if (candidate == null || candidate.AcceptanceOnly || candidate.SiteRegions == null)
                    continue;
                for (var i = 0; i < candidate.SiteRegions.Count; i++)
                {
                    var r = candidate.SiteRegions[i];
                    if (r == null || !string.Equals(r.SiteId, siteId, StringComparison.Ordinal))
                        continue;
                    surface = candidate;
                    region = r;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Continuous authored anchor 优先级：
        /// A. opening LocationId 有 baked SitePlace → 用 sitePlaces[].worldX/worldY；
        /// B. 否则用 siteRegions[].arrivalWorldX/arrivalWorldY；
        /// C. 都没有 → false（调用方走 Legacy LocalMap geometry 兼容路径）。
        /// </summary>
        public static bool TryResolveBakedAnchor(
            OutdoorWorldSurfaceDefinition surface,
            string siteId,
            string locationId,
            out float worldX,
            out float worldY,
            out ContinuousOutdoorAnchorSource source)
        {
            worldX = 0f;
            worldY = 0f;
            source = ContinuousOutdoorAnchorSource.None;
            if (surface == null || string.IsNullOrEmpty(siteId))
                return false;

            if (!string.IsNullOrEmpty(locationId) && surface.SitePlaces != null)
            {
                for (var i = 0; i < surface.SitePlaces.Count; i++)
                {
                    var place = surface.SitePlaces[i];
                    if (place == null ||
                        !string.Equals(place.SiteId, siteId, StringComparison.Ordinal) ||
                        !string.Equals(place.LocationId, locationId, StringComparison.Ordinal))
                        continue;
                    worldX = place.WorldX;
                    worldY = place.WorldY;
                    source = ContinuousOutdoorAnchorSource.BakedSitePlace;
                    return true;
                }
            }

            if (surface.SiteRegions != null)
            {
                for (var i = 0; i < surface.SiteRegions.Count; i++)
                {
                    var r = surface.SiteRegions[i];
                    if (r == null || !string.Equals(r.SiteId, siteId, StringComparison.Ordinal))
                        continue;
                    worldX = r.ArrivalWorldX;
                    worldY = r.ArrivalWorldY;
                    source = ContinuousOutdoorAnchorSource.RegionArrival;
                    return true;
                }
            }

            return false;
        }

        public static bool IsChunkPresent(OutdoorWorldSurfaceDefinition surface, SurfaceChunkCoord coord)
        {
            if (surface?.Chunks == null)
                return false;
            for (var i = 0; i < surface.Chunks.Count; i++)
                if (surface.Chunks[i].Coord == coord)
                    return true;
            return false;
        }

        public static OutdoorSurfaceChunkDefinition FindChunk(
            OutdoorWorldSurfaceDefinition surface, SurfaceChunkCoord coord)
        {
            if (surface?.Chunks == null)
                return null;
            for (var i = 0; i < surface.Chunks.Count; i++)
                if (surface.Chunks[i].Coord == coord)
                    return surface.Chunks[i];
            return null;
        }

        /// <summary>
        /// A streaming chunk must be covered by the published presentation and navigation grids.
        /// </summary>
        public static bool TryValidateChunkData(
            DefinitionRegistry registry,
            OutdoorWorldSurfaceDefinition surface,
            SurfaceChunkCoord coord,
            out string failure)
        {
            failure = string.Empty;
            if (surface == null)
            {
                failure = "SurfaceUnresolved";
                return false;
            }

            var chunk = FindChunk(surface, coord);
            if (chunk == null)
            {
                failure = "ResolvedChunk=" + coord + " ChunkExists=false";
                return false;
            }
            if (registry == null || !registry.TryGetContinuousSurfaceWorldMap(surface.SurfaceId, out var map) || map == null)
            {
                failure = "ResolvedChunk=" + coord + " PresentationMissing";
                return false;
            }
            if (map.CellSize <= 0f)
            {
                failure = "ResolvedChunk=" + coord + " PresentationMetricInvalid";
                return false;
            }
            if (!registry.TryGetOutdoorSurfaceGeography(surface.SurfaceId, out var geography) || geography?.Navigation == null ||
                !geography.CoverageChunks.Contains(coord))
            {
                failure = "ResolvedChunk=" + coord + " GeographyMissing";
                return false;
            }
            var left = surface.OriginWorldX + coord.X * surface.ChunkWidth;
            var bottom = surface.OriginWorldY + coord.Y * surface.ChunkHeight;
            var x = (int)Math.Round((left - map.OriginWorldX) / map.CellSize);
            var y = (int)Math.Round((bottom - map.OriginWorldY) / map.CellSize);
            var width = (int)Math.Round(surface.ChunkWidth / map.CellSize);
            var height = (int)Math.Round(surface.ChunkHeight / map.CellSize);
            if (width <= 0 || height <= 0 || x < 0 || y < 0 ||
                x + width > map.WidthCells || y + height > map.HeightCells ||
                map.BaseTerrainRows.Count != map.HeightCells || map.ForestRows.Count != map.HeightCells)
            {
                failure = "ResolvedChunk=" + coord + " PresentationCoverageMismatch";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 只读 preflight：metric 合法 + center chunk 存在 + shared streaming radius 内全部已覆盖的 neighbor
        /// source 可解析。不写任何 Runtime 字段（与 activation 使用同一规则）。
        /// </summary>
        public static bool TryPreflightNeighborhood(
            DefinitionRegistry registry,
            OutdoorWorldSurfaceDefinition surface,
            SurfaceChunkCoord center,
            out string failure)
        {
            failure = string.Empty;
            if (surface == null)
            {
                failure = "SurfaceUnresolved";
                return false;
            }
            if (surface.CellSize <= 0f || surface.ChunkWidth <= 0f || surface.ChunkHeight <= 0f)
            {
                failure = "MetricValid=false";
                return false;
            }
            if (!IsChunkPresent(surface, center))
            {
                failure = "ResolvedChunk=" + center + " ChunkExists=false";
                return false;
            }

            var present = 0;
            var radius = ContinuousSurfaceStreamingPolicy.ActiveRadiusChunks;
            for (var dy = -radius; dy <= radius; dy++)
            for (var dx = -radius; dx <= radius; dx++)
            {
                var coord = new SurfaceChunkCoord(center.X + dx, center.Y + dy);
                if (!IsChunkPresent(surface, coord))
                    continue;
                if (!TryValidateChunkData(registry, surface, coord, out failure))
                    return false;
                present++;
            }

            if (present <= 0)
            {
                failure = "ResolvedChunk=" + center + " NeighborhoodEmpty";
                return false;
            }
            return true;
        }

        /// <summary>world position → chunk（与 activation 同一 mapper 规则）。</summary>
        public static SurfaceChunkCoord WorldToChunk(OutdoorWorldSurfaceDefinition surface, float worldX, float worldY)
        {
            var mapper = new OutdoorSurfaceCoordinateMapper(
                surface.ChunkWidth, surface.ChunkHeight, surface.CellSize,
                originWorldX: surface.OriginWorldX, originWorldY: surface.OriginWorldY);
            return mapper.WorldToChunk(worldX, worldY);
        }

        /// <summary>
        /// Commit 决策：只有 prepare 成功 **且** preflight 通过，才允许提交 canonical position
        /// 并清掉 legacy Site／LocalMap authority。失败时 legacy/startup authority 必须保持完整
        /// （否则就是 producer 看到的绿色空地：Legacy 已销毁 + Continuous 未建立）。
        /// </summary>
        public static bool ShouldCommitStartupAuthority(bool prepareSucceeded, bool preflightPassed) =>
            prepareSucceeded && preflightPassed;

        /// <summary>
        /// InitialBootstrap token 消费决策：只有 Continuous Surface 真正激活成功才消费。
        /// activation 失败时 token 保留，下一次 activation 仍可 retry（禁止不可恢复 half-state）。
        /// </summary>
        public static bool ShouldConsumeInitialBootstrap(bool startupCommitted, bool surfaceActivated) =>
            startupCommitted && surfaceActivated;

        /// <summary>
        /// Startup opening population 判定真源（§13）：「至少有一个」绝不算通过；expected 中每一个
        /// 都必须 materialized **且** 有 EntityView。与 Host 实际调用的是同一份逻辑（可无头测试）。
        /// </summary>
        public static bool TryCheckPopulationComplete(
            IReadOnlyList<EntityId> expected,
            Func<EntityId, bool> isMaterialized,
            Func<EntityId, bool> hasView,
            List<EntityId> missingInto)
        {
            missingInto?.Clear();
            if (expected == null || expected.Count == 0)
                return true;
            var complete = true;
            for (var i = 0; i < expected.Count; i++)
            {
                var id = expected[i];
                if (id.IsNone)
                    continue;
                var materialized = isMaterialized != null && isMaterialized(id);
                var visible = hasView != null && hasView(id);
                if (materialized && visible)
                    continue;
                complete = false;
                missingInto?.Add(id);
            }

            return complete;
        }

        /// <summary>
        /// §3：spatial validity 判定真源。EntityView 存在不足以通过 —— 每个 expected entity 的
        /// presentation 位置还必须落在 loaded chunks + CompositeWalkGrid + 本 Site
        /// （或本 Site baked envelope 内）。与 Host 实际调用的是同一份逻辑（可无头测试）。
        /// </summary>
        public static bool TryCheckPopulationSpatiallyValid(
            IReadOnlyList<EntityId> expected,
            Func<EntityId, bool> isSpatiallyValid,
            List<EntityId> spatiallyInvalidInto)
        {
            spatiallyInvalidInto?.Clear();
            if (expected == null || expected.Count == 0)
                return true;
            var valid = true;
            for (var i = 0; i < expected.Count; i++)
            {
                var id = expected[i];
                if (id.IsNone)
                    continue;
                if (isSpatiallyValid != null && isSpatiallyValid(id))
                    continue;
                valid = false;
                spatiallyInvalidInto?.Add(id);
            }

            return valid;
        }
    }
}
