using System;
using System.Collections.Generic;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Data.Content
{
    /// <summary>
    /// Normal Continuous NewGame 的 opening entity 初始落点权威（§4–§12）。
    ///
    /// <para>
    /// 唯一坐标空间：全部读数来自 checked-in Continuous 真源
    /// （<c>sitePlaces</c> / <c>sitePlacements</c> / <c>openingEntityAnchors</c> / <c>siteRegions</c>）。
    /// 本类<b>不</b>调用 legacy LocalMap → WorldPosition 映射；那条路径只属于
    /// old save compatibility／legacy LocalMap／migration tooling。
    /// </para>
    ///
    /// <para>
    /// 落点优先级（§9）：
    /// <list type="number">
    /// <item>RuntimePreciseAnchor —— NPC 真实移动过／snapshot／dematerialize capture 的既有
    /// canonical 位置（经 §11 spatial validation 后才信任）。</item>
    /// <item>BakedOpeningEntityAnchor —— checked-in 的该 spawn 专属锚点。</item>
    /// <item>BakedSitePlace —— 该 LocationId 的 baked 地点中心。</item>
    /// <item>SiteArrivalFallback —— Site arrival + deterministic fallback（由调用方叠加）。</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// §11：<c>AtSite(site) + HasContinuousWorldPosition</c> 但锚点明显不属于该 Site 的 baked
    /// physical envelope 时，不得盲目信任 —— 返回拒绝原因让调用方记录
    /// <c>[ContinuousResidentAnchorRejected]</c> 并退回 baked 真源。
    /// </para>
    /// </summary>
    public enum OpeningInitialPlacementSource
    {
        RuntimePreciseAnchor = 0,
        BakedOpeningEntityAnchor = 1,
        BakedSitePlace = 2,
        SiteArrivalFallback = 3
    }

    public static class ContinuousOutdoorOpeningAnchorResolver
    {
        public static float EnvelopeMargin(OutdoorWorldSurfaceDefinition surface) =>
            surface == null ? 0.1f : Math.Max(surface.CellSize * 3f, 0.05f);

        /// <summary>
        /// checked-in opening anchor 查询。<b>只按 SpawnKey 匹配</b>：SpawnStableKey 与 DefinitionId 是
        /// 两个不同的 contract（§4），允许 <c>DefinitionId == spawnKey</c> 会让「同一 Definition 多次
        /// spawn」直接吃第一个实例的 anchor。key 由 <c>OpeningSpawnIdentityBoard</c> 在 GameStart
        /// 建立，可从 spawned Entity 反查。
        /// </summary>
        public static bool TryGetBakedEntityAnchor(
            OutdoorWorldSurfaceDefinition surface,
            string siteId,
            string spawnKey,
            out WorldVec2 anchor)
        {
            anchor = default;
            if (!TryGetBakedEntityAnchorDefinition(surface, siteId, spawnKey, out var definition))
                return false;
            anchor = new WorldVec2(definition.WorldX, definition.WorldY);
            return true;
        }

        /// <summary>
        /// 返回 spawn 专属 opening anchor 的完整 authored definition。调用方可同时取得
        /// canonical 坐标与 <see cref="WorldSiteOpeningEntityAnchorDefinition.SourceLocationId"/>，
        /// 避免首帧物理出生点与逻辑 LocationId 分裂。
        /// </summary>
        public static bool TryGetBakedEntityAnchorDefinition(
            OutdoorWorldSurfaceDefinition surface,
            string siteId,
            string spawnKey,
            out WorldSiteOpeningEntityAnchorDefinition definition)
        {
            definition = null;
            if (surface?.OpeningEntityAnchors == null || string.IsNullOrEmpty(siteId) ||
                string.IsNullOrEmpty(spawnKey))
                return false;
            for (var i = 0; i < surface.OpeningEntityAnchors.Count; i++)
            {
                var a = surface.OpeningEntityAnchors[i];
                if (a == null || !string.Equals(a.SiteId, siteId, StringComparison.Ordinal))
                    continue;
                if (!string.Equals(a.SpawnKey, spawnKey, StringComparison.Ordinal))
                    continue;
                definition = a;
                return true;
            }

            return false;
        }

        public static bool TryGetBakedSitePlace(
            OutdoorWorldSurfaceDefinition surface,
            string siteId,
            string locationId,
            out WorldVec2 anchor)
        {
            anchor = default;
            if (surface?.SitePlaces == null || string.IsNullOrEmpty(siteId) ||
                string.IsNullOrEmpty(locationId))
                return false;
            for (var i = 0; i < surface.SitePlaces.Count; i++)
            {
                var p = surface.SitePlaces[i];
                if (p == null || !string.Equals(p.SiteId, siteId, StringComparison.Ordinal))
                    continue;
                if (!string.Equals(p.LocationId, locationId, StringComparison.Ordinal))
                    continue;
                anchor = new WorldVec2(p.WorldX, p.WorldY);
                return true;
            }

            return false;
        }

        /// <summary>
        /// §12：由 checked-in <c>sitePlacements</c>（rect）∪ <c>sitePlaces</c>（点）∪ arrival
        /// 计算该 Site 的 baked physical envelope。比 Strategic Footprint 小得多，
        /// 因此能真正发现「坐标系完全跑偏」的锚点。
        /// </summary>
        public static bool TryGetSiteBakedEnvelope(
            OutdoorWorldSurfaceDefinition surface,
            string siteId,
            out float minX,
            out float minY,
            out float maxX,
            out float maxY)
        {
            minX = float.MaxValue; minY = float.MaxValue;
            maxX = float.MinValue; maxY = float.MinValue;
            if (surface == null || string.IsNullOrEmpty(siteId))
                return false;
            var any = false;
            if (surface.SitePlacements != null)
                for (var i = 0; i < surface.SitePlacements.Count; i++)
                {
                    var p = surface.SitePlacements[i];
                    if (p == null || !string.Equals(p.SiteId, siteId, StringComparison.Ordinal))
                        continue;
                    var x1 = p.WorldX;
                    var y1 = p.WorldY;
                    var x2 = p.WorldX + Math.Max(0f, p.WorldWidth);
                    var y2 = p.WorldY + Math.Max(0f, p.WorldHeight);
                    minX = Math.Min(minX, Math.Min(x1, x2)); maxX = Math.Max(maxX, Math.Max(x1, x2));
                    minY = Math.Min(minY, Math.Min(y1, y2)); maxY = Math.Max(maxY, Math.Max(y1, y2));
                    any = true;
                }
            if (surface.SitePlaces != null)
                for (var i = 0; i < surface.SitePlaces.Count; i++)
                {
                    var p = surface.SitePlaces[i];
                    if (p == null || !string.Equals(p.SiteId, siteId, StringComparison.Ordinal))
                        continue;
                    minX = Math.Min(minX, p.WorldX); maxX = Math.Max(maxX, p.WorldX);
                    minY = Math.Min(minY, p.WorldY); maxY = Math.Max(maxY, p.WorldY);
                    any = true;
                }
            if (surface.SiteRegions != null)
                for (var i = 0; i < surface.SiteRegions.Count; i++)
                {
                    var r = surface.SiteRegions[i];
                    if (r == null || !string.Equals(r.SiteId, siteId, StringComparison.Ordinal))
                        continue;
                    minX = Math.Min(minX, r.ArrivalWorldX); maxX = Math.Max(maxX, r.ArrivalWorldX);
                    minY = Math.Min(minY, r.ArrivalWorldY); maxY = Math.Max(maxY, r.ArrivalWorldY);
                    any = true;
                }
            if (!any)
                return false;
            var margin = EnvelopeMargin(surface);
            minX -= margin; minY -= margin; maxX += margin; maxY += margin;
            return true;
        }

        public static bool IsInsideSiteBakedEnvelope(
            OutdoorWorldSurfaceDefinition surface,
            string siteId,
            float x,
            float y)
        {
            if (!TryGetSiteBakedEnvelope(surface, siteId, out var minX, out var minY, out var maxX, out var maxY))
                return false;
            return x >= minX && x <= maxX && y >= minY && y <= maxY;
        }

        /// <summary>
        /// §9 落点优先级 + §11 防御性校验。调用方负责最后一级（arrival fallback + deterministic
        /// offset）以及 presentation 去重叠（同一 materialize pass 内不得两点重合）。
        /// </summary>
        public static OpeningInitialPlacementSource ResolveInitialPlacement(
            OutdoorWorldSurfaceDefinition surface,
            string siteId,
            string spawnKey,
            string locationId,
            bool hasRuntimeAnchor,
            float runtimeX,
            float runtimeY,
            out WorldVec2 anchor,
            out string rejectedRuntimeAnchorReason,
            out bool rejectedRuntimeAnchor)
        {
            anchor = default;
            rejectedRuntimeAnchor = false;
            rejectedRuntimeAnchorReason = string.Empty;

            if (hasRuntimeAnchor)
            {
                if (IsInsideSiteBakedEnvelope(surface, siteId, runtimeX, runtimeY))
                {
                    anchor = new WorldVec2(runtimeX, runtimeY);
                    return OpeningInitialPlacementSource.RuntimePreciseAnchor;
                }

                // §11：不删除 Domain entity，只拒绝这个锚点。
                rejectedRuntimeAnchor = true;
                rejectedRuntimeAnchorReason =
                    "anchor (" + runtimeX + "," + runtimeY + ") outside baked envelope of " + siteId +
                    EnvelopeText(surface, siteId);
            }

            if (TryGetBakedEntityAnchor(surface, siteId, spawnKey, out anchor))
                return OpeningInitialPlacementSource.BakedOpeningEntityAnchor;
            if (TryGetBakedSitePlace(surface, siteId, locationId, out anchor))
                return OpeningInitialPlacementSource.BakedSitePlace;
            anchor = default;
            return OpeningInitialPlacementSource.SiteArrivalFallback;
        }

        static string EnvelopeText(OutdoorWorldSurfaceDefinition surface, string siteId)
        {
            if (!TryGetSiteBakedEnvelope(surface, siteId, out var minX, out var minY, out var maxX, out var maxY))
                return " [envelope=unresolved]";
            return " [envelope=[" + minX.ToString("F4") + "," + minY.ToString("F4") + "]..[" +
                   maxX.ToString("F4") + "," + maxY.ToString("F4") + "]]";
        }

        /// <summary>
        /// §6/§7：某个 location 的 authored placement 内，按稳定 slot index 生成 canonical anchor。
        ///
        /// <para>
        /// 实现已收敛到 <see cref="WorldSiteOutdoorOpeningAnchorBake"/> + <see cref="WorldSiteOutdoorBakeTransform"/>
        /// 的<b>单一 bake truth</b>（与 SitePlacements／SitePlaces 同一公式）：slot 先在 source LocalMap
        /// 坐标系定位，再经共享 transform 烘到 canonical。因此这里必须给出 Site physical footprint
        /// 与 hexSize。
        /// </para>
        /// </summary>
        public static bool TryComputePlaceSlotAnchor(
            OutdoorWorldSurfaceDefinition surface,
            IReadOnlyList<HexCoord> footprint,
            float hexSize,
            MapLayoutDefinition sourceLayout,
            WorldVec2 placeLocalPosition,
            string locationId,
            int slotIndex,
            out WorldVec2 anchor,
            out bool usedPlacementExtent)
        {
            anchor = default;
            usedPlacementExtent = false;
            if (surface == null || sourceLayout == null || slotIndex < 0)
                return false;

            var sourceBounds = WorldSiteSpatialMapping.WorldSiteLocalMapBounds.FromOriginSize(
                sourceLayout.OriginX, sourceLayout.OriginY, sourceLayout.CellSize,
                sourceLayout.Width, sourceLayout.Height);
            if (!sourceBounds.IsValid)
                return false;

            var hasExtent = WorldSiteOutdoorOpeningAnchorBake.TryGetBoundPlacementLocalExtent(
                sourceLayout, locationId, out var minX, out var minY, out var maxX, out var maxY);
            usedPlacementExtent = hasExtent;
            return WorldSiteOutdoorOpeningAnchorBake.TryBakeSlotAnchor(
                footprint, hexSize, sourceBounds, placeLocalPosition,
                hasExtent, minX, minY, maxX, maxY, slotIndex, out _, out anchor);
        }

        /// <summary>
        /// §16：materialize 时的非法起点搜索。在 baked 位置周围按确定性同心环尝试候选点，
        /// 直到落入 CompositeWalkGrid；找不到就返回 false（调用方记录
        /// <c>ContinuousMaterializationInvalidSpawn</c> 并阻止该 NPC 启动日程寻路）。
        /// </summary>
        public static bool TryFindWalkableCandidate(
            float originX,
            float originY,
            float spacing,
            int maxCandidates,
            Func<float, float, bool> isWalkable,
            out WorldVec2 candidate)
        {
            candidate = default;
            if (isWalkable == null)
                return false;
            if (isWalkable(originX, originY))
            {
                candidate = new WorldVec2(originX, originY);
                return true;
            }

            if (spacing <= 0f)
                spacing = 0.05f;
            var rings = Math.Max(1, maxCandidates / 8);
            for (var ring = 1; ring <= rings; ring++)
            {
                for (var step = 0; step < 8; step++)
                {
                    var angle = step * (Math.PI / 4.0);
                    var radius = spacing * ring;
                    var x = originX + (float)Math.Cos(angle) * radius;
                    var y = originY + (float)Math.Sin(angle) * radius;
                    if (!isWalkable(x, y))
                        continue;
                    candidate = new WorldVec2(x, y);
                    return true;
                }
            }

            return false;
        }
    }
}
