using System;
using System.Collections.Generic;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase 5R-B3A（Unified）→ 5R-B3C3（V2 Kernel Radial）：WorldSite LocalMap 空间 ↔ HexWorld
    /// 连续世界表面空间映射。
    ///
    /// 5R-B3C3 起内部<b>正式切到 <see cref="HexFootprintSpatialGeometry"/>（star-shaped radial）</b>：
    ///  - Local 矩形归一化到 square [-1,1]² → 从 footprint kernel 沿方向 ray → polygon boundary，
    ///    不再经过 AABB 空洞 + nearest projection（V1 collapse 根因，见 5R-B3C2.1 验收 FAIL）。
    ///  - 前提：footprint 必须非空 kernel（star-shaped）。kernel-empty → 明确失败，不 fallback V1。
    ///  - <see cref="TryBuildGeometry"/> 一次性构建 geometry（B4 每帧调用复用）；
    ///    既有 site+bounds API 保留，内部走同一 geometry。
    ///  - 独立投影（最近 footprint 点）降级为 <see cref="ProjectWorldPointToFootprint"/>，不混入
    ///    coordinate mapping 主链。
    ///
    /// 职责边界（ADR-0027 §11）：只负责物理位置映射；不负责 Battle / Travel / PlayerParty Context /
    /// Army / Presence sync / Materialization / Ingress-Egress 行为。
    ///
    /// 设计约束（ADR-0027 + 5R-0.1 修正）：
    ///  - 不依赖 UnityEngine / XianXia.Data —— Core 程序集零引用。
    ///  - 不用 MapLayout 的 absolute coordinate 直接当 HexWorld 坐标；world domain 来自真实
    ///    OccupiedHexes + HexMath Pointy-Top / Odd-R 几何。
    ///  - AnchorHex / PresenceHex / DisplayName / SiteType 不参与物理映射。
    ///  - <b>OccupiedHexes 为空 / kernel 为空时所有 Physical Mapping API 明确失败</b>（不伪造
    ///    AnchorHex fake single-hex physical domain，不静默 fallback）。
    /// </summary>
    public static class WorldSiteSpatialMapping
    {
        /// <summary>
        /// WorldSite LocalMap 真实 playable bounds（LocalMap grid 世界单位）。
        /// localPosition 采用 <b>LocalMap 世界单位坐标</b>（与 MinX/MinY 同系，grid cell 坐标需
        /// 先乘 CellSize 再加 OriginX/OriginY）；归一化只使用相对 bounds。
        /// </summary>
        public readonly struct WorldSiteLocalMapBounds
        {
            public WorldSiteLocalMapBounds(float originX, float originY, float cellSize, int width, int height)
            {
                OriginX = originX;
                OriginY = originY;
                CellSize = cellSize;
                Width = width;
                Height = height;
            }

            public static WorldSiteLocalMapBounds FromOriginSize(
                float originX,
                float originY,
                float cellSize,
                int width,
                int height) => new WorldSiteLocalMapBounds(originX, originY, cellSize, width, height);

            public float OriginX { get; }
            public float OriginY { get; }
            public float CellSize { get; }
            public int Width { get; }
            public int Height { get; }

            public bool IsValid => CellSize > 0.0001f && Width > 0 && Height > 0;

            public float MinX => OriginX;
            public float MaxX => OriginX + Width * CellSize;
            public float MinY => OriginY;
            public float MaxY => OriginY + Height * CellSize;

            public float CenterX => (MinX + MaxX) * 0.5f;
            public float CenterY => (MinY + MaxY) * 0.5f;

            /// <summary>playable 世界跨度（防除零）。</summary>
            public float SpanX => Math.Max(0.0001f, MaxX - MinX);
            public float SpanY => Math.Max(0.0001f, MaxY - MinY);
        }

        /// <summary>
        /// Site footprint 解析：<see cref="WorldSite.OccupiedHexes"/>（缓存 ReadOnlyCollection，零分配）。
        /// <b>空 footprint 一律返回 false</b> —— AnchorHex / PresenceHex 不参与 physical mapping
        /// （ADR-0027 Decision #2/#7），不自动修 footprint，不产生 fake single-hex domain。
        /// </summary>
        static bool ResolveFootprint(
            WorldSite site,
            out IReadOnlyList<HexCoord> footprint)
        {
            footprint = null;
            if (site == null)
                return false;
            if (site.OccupiedHexes.Count == 0)
                return false;
            footprint = site.OccupiedHexes;
            return true;
        }

        /// <summary>
        /// 计算 Site footprint 的连续 world-surface domain：全部 OccupiedHexes 的真实 Hex 角点
        /// 的 axis-aligned 外接框。不做 nearest-hex-center 简化；irregular footprint 的包络空洞
        /// 由投影阶段处理（5R-B3A：delegate 到 HexFootprintSpatialMapping）。
        /// </summary>
        public static bool TryComputeFootprintWorldDomain(
            WorldSite site,
            float hexSize,
            out float minX,
            out float maxX,
            out float minY,
            out float maxY)
        {
            minX = maxX = minY = maxY = 0f;
            if (!ResolveFootprint(site, out var footprint))
                return false;
            return HexFootprintSpatialMapping.TryComputeWorldDomain(
                footprint, hexSize, out minX, out maxX, out minY, out maxY);
        }

        /// <summary>
        /// Phase 5R-B3C3：一次性构建 V2 geometry（boundary + kernel）。B4 每帧调用应在此处构建一次并
        /// 复用 <see cref="HexFootprintSpatialGeometry"/>（radial 方法零堆分配）。
        /// footprint 空 / kernel-empty → false（不 fallback V1）。
        /// </summary>
        public static bool TryBuildGeometry(
            WorldSite site,
            float hexSize,
            out HexFootprintSpatialGeometry geometry)
        {
            geometry = null;
            if (site == null || hexSize <= 0.0001f)
                return false;
            if (!ResolveFootprint(site, out var footprint))
                return false;
            return HexFootprintSpatialGeometry.TryBuild(footprint, hexSize, out geometry) && geometry.HasKernel;
        }

        /// <summary>V2 主映射：Local normalized (u,v) → footprint kernel radial → world surface。
        /// 内部构建 geometry（每调用一次）；高频路径应改用 <see cref="TryBuildGeometry"/> 复用。</summary>
        public static bool TryLocalToWorldSurface(
            WorldSite site,
            WorldSiteLocalMapBounds bounds,
            WorldVec2 localPosition,
            float hexSize,
            out WorldVec2 worldPosition)
        {
            worldPosition = default;
            if (site == null || !bounds.IsValid || hexSize <= 0.0001f)
                return false;
            if (!TryBuildGeometry(site, hexSize, out var geometry))
                return false;
            return geometry.TryLocalToWorldSurface(
                bounds.MinX, bounds.MaxX, bounds.MinY, bounds.MaxY, localPosition, out worldPosition);
        }

        /// <summary>V2 主映射（复用 geometry，零堆分配；B4 推荐路径）。</summary>
        public static bool TryLocalToWorldSurface(
            HexFootprintSpatialGeometry geometry,
            WorldSiteLocalMapBounds bounds,
            WorldVec2 localPosition,
            out WorldVec2 worldPosition)
        {
            worldPosition = default;
            if (geometry == null || !bounds.IsValid)
                return false;
            return geometry.TryLocalToWorldSurface(
                bounds.MinX, bounds.MaxX, bounds.MinY, bounds.MaxY, localPosition, out worldPosition);
        }

        /// <summary>便捷重载：hexSize = <see cref="HexWorldScale.DefaultHexOuterRadius"/>（1f）。</summary>
        public static bool TryLocalToWorldSurface(
            WorldSite site,
            WorldSiteLocalMapBounds bounds,
            WorldVec2 localPosition,
            out WorldVec2 worldPosition) =>
            TryLocalToWorldSurface(site, bounds, localPosition, HexWorldScale.DefaultHexOuterRadius, out worldPosition);

        /// <summary>
        /// V2 inverse：worldSurface → 同一 kernel radial authority → Local。
        /// footprint 外点由 radial 语义投影到 polygon（非 V1 AABB collapse；bijective 域内稳定）。
        /// </summary>
        public static bool TryWorldSurfaceToLocal(
            WorldSite site,
            WorldSiteLocalMapBounds bounds,
            WorldVec2 worldPosition,
            float hexSize,
            out WorldVec2 localPosition)
        {
            localPosition = default;
            if (site == null || !bounds.IsValid || hexSize <= 0.0001f)
                return false;
            if (!TryBuildGeometry(site, hexSize, out var geometry))
                return false;
            return geometry.TryWorldSurfaceToLocal(
                bounds.MinX, bounds.MaxX, bounds.MinY, bounds.MaxY, worldPosition, out localPosition);
        }

        /// <summary>V2 inverse（复用 geometry，零堆分配；B4 推荐路径）。</summary>
        public static bool TryWorldSurfaceToLocal(
            HexFootprintSpatialGeometry geometry,
            WorldSiteLocalMapBounds bounds,
            WorldVec2 worldPosition,
            out WorldVec2 localPosition)
        {
            localPosition = default;
            if (geometry == null || !bounds.IsValid)
                return false;
            return geometry.TryWorldSurfaceToLocal(
                bounds.MinX, bounds.MaxX, bounds.MinY, bounds.MaxY, worldPosition, out localPosition);
        }

        /// <summary>
        /// 独立 helper（非主 mapping）：world 点 → footprint 最近合法点。仅供 legacy / DerivedHex 等
        /// 需要"最近点"的场景；不再混入 coordinate mapping（5R-B3C3 §十）。
        /// </summary>
        public static bool ProjectWorldPointToFootprint(
            WorldSite site,
            WorldVec2 worldPosition,
            float hexSize,
            out WorldVec2 projected,
            out HexCoord nearestHex)
        {
            projected = default;
            nearestHex = default;
            if (site == null || hexSize <= 0.0001f)
                return false;
            if (!TryBuildGeometry(site, hexSize, out var geometry))
                return false;
            return geometry.TryProjectWorldPointToFootprint(worldPosition, out projected, out nearestHex);
        }

        /// <summary>
        /// DerivedPresenceHex：worldPosition → WorldToHex；若 ∈ footprint 直接返回；
        /// 数值边界歧义（polygon boundary / 空洞）用 footprint polygon containment 稳定解析
        /// （返回最近 occupied hex）。不使用 AnchorHex / PresenceHex / DisplayName / SiteType。
        /// </summary>
        public static bool TryResolveDerivedFootprintHex(
            WorldSite site,
            WorldVec2 worldPosition,
            float hexSize,
            out HexCoord footprintHex)
        {
            footprintHex = default;
            if (!ResolveFootprint(site, out var footprint))
                return false;
            return HexFootprintSpatialMapping.TryResolveFootprintHex(
                footprint, worldPosition, hexSize, out footprintHex);
        }
    }
}
