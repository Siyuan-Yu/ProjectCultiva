using System;
using System.Collections.Generic;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase 5R-B1（Shadow）→ 5R-B3A（Unified）：WorldSite LocalMap 空间 ↔ HexWorld 连续世界表面空间映射。
    /// 5R-B3A 起<b>内部全部 delegate 到共享 <see cref="HexFootprintSpatialMapping"/></b>（Wilderness 单 Hex 与
    /// WorldSite 多 Hex 同一种真实 Pointy-Top / Odd-R polygon 几何语义）；本类只保留 WorldSite 适配层
    /// （footprint 解析 + 防御性 guards），不再持有任何映射算法，避免与共享 helper 双实现漂移。
    ///
    /// 职责边界（ADR-0027 §11）：只负责物理位置映射；不负责 Battle / Travel / PlayerParty Context /
    /// Army / Presence sync / Materialization / Ingress-Egress 行为。
    ///
    /// 设计约束（ADR-0027 + 5R-0.1 修正）：
    ///  - 不依赖 UnityEngine / XianXia.Data —— Core 程序集零引用（XianXia.Core.asmdef references=[]）。
    ///    调用方从 <see cref="XianXia.Data.Content.MapLayoutDefinition"/> 取值构造
    ///    <see cref="WorldSiteLocalMapBounds"/>（OriginX/OriginY/CellSize/Width/Height）。
    ///  - 不用 MapLayout 的 absolute coordinate 直接当 HexWorld 坐标；world domain 必须来自真实
    ///    OccupiedHexes + HexMath Pointy-Top / Odd-R 几何（footprint polygon 外接框）。
    ///  - 保持 Local 左/右/上/下 ↔ footprint world 域左/右/上/下；不同 LocalMap 尺寸合法
    ///    （normalized (u,v) 语义，如 50×50 与 100×80 的中心都映射到 footprint 域中心）。
    ///  - AnchorHex / PresenceHex / DisplayName / SiteType 不参与物理映射。
    ///  - irregular / concave footprint：candidate 落在包络空洞时投影到最近 occupied hex polygon
    ///    上的最近合法点（不做 nearest-hex-center 跳变）。
    ///  - 无 per-call 堆分配（OccupiedHexes 为缓存 ReadOnlyCollection）。
    ///  - <b>OccupiedHexes 为空时所有 Physical Mapping API 明确失败</b>：AnchorHex / PresenceHex
    ///    不是 Physical Position / Spatial Mapping authority（ADR-0027），绝不回退
    ///    AnchorHex fake single-hex physical domain。
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

        /// <summary>V1 映射：Local normalized (u,v) → footprint world domain → 验证/投影。</summary>
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
            if (!ResolveFootprint(site, out var footprint))
                return false;
            return HexFootprintSpatialMapping.TryLocalToWorldSurface(
                footprint, bounds.MinX, bounds.MaxX, bounds.MinY, bounds.MaxY,
                localPosition, hexSize, out worldPosition);
        }

        /// <summary>便捷重载：hexSize = <see cref="HexWorldScale.DefaultHexOuterRadius"/>（1f）。</summary>
        public static bool TryLocalToWorldSurface(
            WorldSite site,
            WorldSiteLocalMapBounds bounds,
            WorldVec2 localPosition,
            out WorldVec2 worldPosition) =>
            TryLocalToWorldSurface(site, bounds, localPosition, HexWorldScale.DefaultHexOuterRadius, out worldPosition);

        /// <summary>
        /// 近似可逆 inverse：worldPosition → footprint world-domain normalized (u,v) → Local playable bounds。
        /// 输入在 footprint 外/空洞时先投影到最近合法 occupied polygon（deterministic，非严格双射）。
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
            if (!ResolveFootprint(site, out var footprint))
                return false;
            return HexFootprintSpatialMapping.TryWorldSurfaceToLocal(
                footprint, bounds.MinX, bounds.MaxX, bounds.MinY, bounds.MaxY,
                worldPosition, hexSize, out localPosition);
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
