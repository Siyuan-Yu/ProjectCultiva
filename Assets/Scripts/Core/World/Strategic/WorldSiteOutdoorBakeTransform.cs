using System;
using System.Collections.Generic;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Outdoor WorldSite 的<b>唯一</b> Site source LocalMap → Continuous WorldPosition bake transform。
    ///
    /// <para>
    /// 输入：source MapLayout bounds（LocalMap grid 世界单位）＋ Site physical footprint（OccupiedHexes）
    /// ＋ hexSize ＋ 一个 source local point。
    /// 输出：canonical Continuous WorldPosition。
    /// </para>
    ///
    /// <para>
    /// 公式（V1 authored bake，纯 AABB 线性归一化，<b>不含</b> footprint 内含性投影）：
    /// <code>
    /// domainX = footprint 全部 hex 角点的 world AABB（HexFootprintSpatialMapping.TryComputeWorldDomain）
    /// u = Clamp01((localX - sourceMinX) / sourceSpanX)
    /// v = Clamp01((localY - sourceMinY) / sourceSpanY)
    /// worldX = domainMinX + u * domainSpanX
    /// worldY = domainMinY + v * domainSpanY
    /// </code>
    /// </para>
    ///
    /// <para>
    /// 为什么必须只有一个：checked-in Content 的 <c>sitePlacements</c>（含 68 个 huangcun 渲染对象）
    /// 与 <c>sitePlaces</c> 全部由这一公式烘焙。任何「建筑用 transform A、NPC 用 transform B」的
    /// 双轨实现都会让 NPC 落在与建筑不同的坐标空间 —— 这正是本轮 opening placement regression 的根因。
    /// </para>
    ///
    /// <para>
    /// 与相邻两个映射的边界（不要混用）：
    /// <list type="bullet">
    /// <item><see cref="HexFootprintSpatialMapping.TryLocalToWorldSurface"/>：同一线性候选点 +
    /// <b>footprint 内含性投影</b>（candidate 不在 footprint hex 内时投到最近多边形边）。
    /// 实测与 authored bake 不一致（68 个 placement 有 39 个不同），因此<b>不是</b> bake truth。</item>
    /// <item><see cref="WorldSiteSpatialMapping"/>：V2 radial kernel 映射（LocalMap 与 Wilderness
    /// 的 Legacy LocalVisible 同步语义），与 authored bake 同样不一致。</item>
    /// </list>
    /// 本类是 SitePlacements／SitePlaces／OpeningEntityAnchors 共用的 authored bake truth。
    /// </para>
    /// </summary>
    public static class WorldSiteOutdoorBakeTransform
    {
        /// <summary>Site physical footprint 的 world AABB（全部 hex 角点外接框）。</summary>
        public static bool TryComputeFootprintDomain(
            IReadOnlyList<HexCoord> footprint,
            float hexSize,
            out float minX,
            out float maxX,
            out float minY,
            out float maxY) =>
            HexFootprintSpatialMapping.TryComputeWorldDomain(footprint, hexSize, out minX, out maxX, out minY, out maxY);

        public static bool TryComputeFootprintDomain(
            WorldSite site,
            float hexSize,
            out float minX,
            out float maxX,
            out float minY,
            out float maxY) =>
            TryComputeFootprintDomain(site?.OccupiedHexes, hexSize, out minX, out maxX, out minY, out maxY);

        /// <summary>
        /// source LocalMap point → canonical Continuous WorldPosition。
        /// footprint 空／规模非法／bounds 非法 → false（不伪造坐标，不 fallback 到别的公式）。
        /// </summary>
        public static bool TryBake(
            IReadOnlyList<HexCoord> footprint,
            float hexSize,
            WorldSiteSpatialMapping.WorldSiteLocalMapBounds sourceBounds,
            WorldVec2 sourceLocalPosition,
            out WorldVec2 canonicalWorldPosition)
        {
            canonicalWorldPosition = default;
            if (footprint == null || footprint.Count == 0 || hexSize <= 0.0001f || !sourceBounds.IsValid)
                return false;
            if (!TryComputeFootprintDomain(footprint, hexSize, out var dMinX, out var dMaxX, out var dMinY, out var dMaxY))
                return false;

            var domainSpanX = dMaxX - dMinX;
            var domainSpanY = dMaxY - dMinY;
            if (domainSpanX <= 0.0001f || domainSpanY <= 0.0001f)
                return false;

            canonicalWorldPosition = Bake(
                sourceBounds, sourceLocalPosition, dMinX, dMinY, domainSpanX, domainSpanY);
            return true;
        }

        public static bool TryBake(
            WorldSite site,
            float hexSize,
            WorldSiteSpatialMapping.WorldSiteLocalMapBounds sourceBounds,
            WorldVec2 sourceLocalPosition,
            out WorldVec2 canonicalWorldPosition) =>
            TryBake(site?.OccupiedHexes, hexSize, sourceBounds, sourceLocalPosition, out canonicalWorldPosition);

        /// <summary>
        /// authored bake 的纯公式部分（domain 已解析）。SitePlacements／SitePlaces／OpeningEntityAnchors
        /// 与内容 bake 工具必须全部走这里，避免第二套公式漂移。
        /// </summary>
        public static WorldVec2 Bake(
            WorldSiteSpatialMapping.WorldSiteLocalMapBounds sourceBounds,
            WorldVec2 sourceLocalPosition,
            float domainMinX,
            float domainMinY,
            float domainSpanX,
            float domainSpanY)
        {
            var u = Clamp01((sourceLocalPosition.X - sourceBounds.MinX) / sourceBounds.SpanX);
            var v = Clamp01((sourceLocalPosition.Y - sourceBounds.MinY) / sourceBounds.SpanY);
            return new WorldVec2(domainMinX + u * domainSpanX, domainMinY + v * domainSpanY);
        }

        /// <summary>bake 的逆运算（domain 线性反归一化）。用于「世界点 → source LocalMap 点」诊断，
        /// 不用于任何 position authority。</summary>
        public static bool TryUnbake(
            IReadOnlyList<HexCoord> footprint,
            float hexSize,
            WorldSiteSpatialMapping.WorldSiteLocalMapBounds sourceBounds,
            WorldVec2 canonicalWorldPosition,
            out WorldVec2 sourceLocalPosition)
        {
            sourceLocalPosition = default;
            if (!TryComputeFootprintDomain(footprint, hexSize, out var dMinX, out var dMaxX, out var dMinY, out var dMaxY))
                return false;
            var domainSpanX = dMaxX - dMinX;
            var domainSpanY = dMaxY - dMinY;
            if (domainSpanX <= 0.0001f || domainSpanY <= 0.0001f || !sourceBounds.IsValid)
                return false;
            var u = Clamp01((canonicalWorldPosition.X - dMinX) / domainSpanX);
            var v = Clamp01((canonicalWorldPosition.Y - dMinY) / domainSpanY);
            sourceLocalPosition = new WorldVec2(
                sourceBounds.MinX + u * sourceBounds.SpanX,
                sourceBounds.MinY + v * sourceBounds.SpanY);
            return true;
        }

        static float Clamp01(float value)
        {
            if (value < 0f)
                return 0f;
            if (value > 1f)
                return 1f;
            return value;
        }
    }
}
