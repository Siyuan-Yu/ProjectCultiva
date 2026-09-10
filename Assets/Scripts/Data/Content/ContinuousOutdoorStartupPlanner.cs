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
        /// <summary>旧 LocalMap geometry → WorldSiteSpatialMapping（compatibility only）。</summary>
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
        /// chunk → 其 authored source MapLayout；同时校验 source 必须精确填满 chunk metric
        /// （否则 seam 会静默变成 gameplay 边界）。
        /// </summary>
        public static bool TryResolveChunkSource(
            DefinitionRegistry registry,
            OutdoorWorldSurfaceDefinition surface,
            SurfaceChunkCoord coord,
            out MapLayoutDefinition layout,
            out string failure)
        {
            layout = null;
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
            if (string.IsNullOrWhiteSpace(chunk.SourceMapLayoutId))
            {
                failure = "ResolvedChunk=" + coord + " SourceMapLayoutId=empty";
                return false;
            }

            var parsed = DefinitionId.Parse(chunk.SourceMapLayoutId);
            if (!parsed.IsSuccess || registry == null ||
                !registry.TryGetMapLayout(parsed.Value, out layout) || layout == null)
            {
                failure = "ResolvedChunk=" + coord + " SourceResolved=false SourceMapLayoutId=" +
                          chunk.SourceMapLayoutId;
                layout = null;
                return false;
            }

            var sourceWidth = layout.Width * surface.CellSize;
            var sourceHeight = layout.Height * surface.CellSize;
            if (Math.Abs(sourceWidth - surface.ChunkWidth) > 0.0001f ||
                Math.Abs(sourceHeight - surface.ChunkHeight) > 0.0001f)
            {
                failure = "ResolvedChunk=" + coord + " SourceMetricMismatch SourceMapLayoutId=" +
                          chunk.SourceMapLayoutId;
                layout = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Site 的 Outdoor source MapLayout（<c>siteRegions[].sourceLocalMapId</c>）。
        /// Opening placement bake 与 SitePlacements 必须共用这一张 source map（§2 同一 truth）。
        /// </summary>
        public static bool TryResolveSiteSourceLayout(
            DefinitionRegistry registry,
            OutdoorWorldSurfaceDefinition surface,
            string siteId,
            out MapLayoutDefinition layout,
            out string failure)
        {
            layout = null;
            failure = string.Empty;
            if (surface?.SiteRegions == null || string.IsNullOrEmpty(siteId))
            {
                failure = "SiteSourceLayoutUnresolved site=" + (siteId ?? string.Empty);
                return false;
            }

            string sourceLocalMapId = null;
            for (var i = 0; i < surface.SiteRegions.Count; i++)
            {
                var region = surface.SiteRegions[i];
                if (region == null || !string.Equals(region.SiteId, siteId, StringComparison.Ordinal))
                    continue;
                sourceLocalMapId = region.SourceLocalMapId;
                break;
            }

            if (string.IsNullOrWhiteSpace(sourceLocalMapId))
            {
                failure = "SiteSourceLayoutUnresolved site=" + siteId + " sourceLocalMapId=empty";
                return false;
            }

            var parsed = DefinitionId.Parse(sourceLocalMapId);
            if (!parsed.IsSuccess || registry == null ||
                !registry.TryGetMapLayout(parsed.Value, out layout) || layout == null)
            {
                failure = "SiteSourceLayoutUnresolved site=" + siteId +
                          " sourceLocalMapId=" + sourceLocalMapId;
                layout = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Site 的 source LocalMap 对应的 checked-in LocalPlaceSet（<c>mapLayoutId</c> 唯一匹配）。
        ///
        /// <para>
        /// closing the loop：opening placement 的 authored 真源必须在 **Content** 里，
        /// 而不是运行时 <c>WorldRegion</c> 板 —— Continuous Outdoor 正常运行时**不会**加载
        /// legacy WorldRegion place set（这正是迁移的目的），启动 invariant 若依赖它就会误报
        /// 「authored place missing from WorldRegion」。
        /// </para>
        /// </summary>
        public static bool TryResolveSiteSourcePlaceSet(
            DefinitionRegistry registry,
            OutdoorWorldSurfaceDefinition surface,
            string siteId,
            out LocalPlaceSetDefinition placeSet,
            out string failure)
        {
            placeSet = null;
            failure = string.Empty;
            if (!TryResolveSiteSourceLayout(registry, surface, siteId, out var layout, out failure))
                return false;

            var sourceMapId = layout.Id.ToString();
            LocalPlaceSetDefinition matched = null;
            var matches = 0;
            foreach (var entry in registry.LocalPlaceSets)
            {
                var candidate = entry.Value;
                if (candidate == null ||
                    !string.Equals(candidate.MapLayoutId, sourceMapId, StringComparison.Ordinal))
                    continue;
                matched = candidate;
                matches++;
            }

            if (matches == 1 && matched != null)
            {
                placeSet = matched;
                return true;
            }

            failure = matches == 0
                ? "SiteSourcePlaceSetUnresolved site=" + siteId + " mapLayoutId=" + sourceMapId
                : "SiteSourcePlaceSetAmbiguous site=" + siteId + " mapLayoutId=" + sourceMapId +
                  " matches=" + matches;
            return false;
        }

        /// <summary>
        /// Content-side authored presentation of a location（LocalMap 世界单位）。
        /// <c>WorldRegion</c> 只是运行时镜像，因此这里以 checked-in LocalPlaceSet 为准。
        /// </summary>
        public static bool TryResolvePlaceLocalPosition(
            LocalPlaceSetDefinition placeSet,
            string locationId,
            out float localX,
            out float localZ)
        {
            localX = 0f;
            localZ = 0f;
            if (placeSet?.Locations == null || string.IsNullOrWhiteSpace(locationId))
                return false;
            for (var i = 0; i < placeSet.Locations.Count; i++)
            {
                var location = placeSet.Locations[i];
                if (location == null ||
                    !string.Equals(location.Id, locationId, StringComparison.Ordinal))
                    continue;
                localX = location.PresentationX;
                localZ = location.PresentationZ;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 只读 preflight：metric 合法 + center chunk 存在 + 全部已覆盖的 radius-1 neighbor
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
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                var coord = new SurfaceChunkCoord(center.X + dx, center.Y + dy);
                if (!IsChunkPresent(surface, coord))
                    continue;
                if (!TryResolveChunkSource(registry, surface, coord, out _, out failure))
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
