using System;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase 5R-B1（Shadow）：WorldSite LocalMap 空间 ↔ HexWorld 连续世界表面空间的纯映射 authority。
    /// 只负责物理位置映射；不负责 Battle / Travel / PlayerParty Context / Army / Presence sync /
    /// Materialization / Ingress-Egress 行为（职责分离见 ADR-0027 §11）。
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
    ///  - V1 不接任何运行时行为（shadow）。B1 之后由 5R-B2/B3 接线。
    /// </summary>
    public static class WorldSiteSpatialMapping
    {
        /// <summary>
        /// Phase 5R-B1.1：pointy-top 单位六角角点常量（radius=1），乘以 hexSize 即真实角点。
        /// 与 <see cref="HexMath.CollectCornerWorldPositions"/> 的公式一致（angle = (π/3)·i + π/6），
        /// 消除 per-call float[6] 堆分配（B2 后 LocalVisible 每帧调用 mapping）。static readonly，
        /// 只读、线程安全；不做复杂缓存系统。
        /// </summary>
        static readonly float[] UnitCornerX =
        {
            0.8660254f, // i=0: 30°
            0f,         // i=1: 90°
            -0.8660254f, // i=2: 150°
            -0.8660254f, // i=3: 210°
            0f,         // i=4: 270°
            0.8660254f, // i=5: 330°
        };

        static readonly float[] UnitCornerY =
        {
            0.5f,
            1f,
            0.5f,
            -0.5f,
            -1f,
            -0.5f,
        };

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
        /// 计算 Site footprint 的连续 world-surface domain：全部 OccupiedHexes 的真实 Hex 角点
        /// （HexMath.CollectCornerWorldPositions）的 axis-aligned 外接框。不做 nearest-hex-center
        /// 简化；irregular footprint 的包络空洞由投影阶段处理。
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
            if (site == null || hexSize <= 0.0001f)
                return false;

            var any = false;
            foreach (var hex in site.EnumerateFootprintHexes())
            {
                HexMath.ToWorldPosition(hex, hexSize, out var cx, out var cy);
                for (var i = 0; i < 6; i++)
                {
                    var x = cx + UnitCornerX[i] * hexSize;
                    var y = cy + UnitCornerY[i] * hexSize;
                    minX = any ? Math.Min(minX, x) : x;
                    maxX = any ? Math.Max(maxX, x) : x;
                    minY = any ? Math.Min(minY, y) : y;
                    maxY = any ? Math.Max(maxY, y) : y;
                    any = true;
                }
            }

            return any;
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
            if (!TryComputeFootprintWorldDomain(site, hexSize, out var minX, out var maxX, out var minY, out var maxY))
                return false;

            var domainW = maxX - minX;
            var domainH = maxY - minY;
            if (domainW <= 0.0001f || domainH <= 0.0001f)
                return false;

            var u = Clamp01((localPosition.X - bounds.MinX) / bounds.SpanX);
            var v = Clamp01((localPosition.Y - bounds.MinY) / bounds.SpanY);
            var candidate = new WorldVec2(minX + u * domainW, minY + v * domainH);

            var hex = HexMath.WorldToHex(candidate.X, candidate.Y, hexSize);
            if (site.OccupiesHex(hex))
            {
                worldPosition = candidate;
                return true;
            }

            // irregular/concave footprint：candidate 落在包络空洞 → 投影到最近合法 occupied polygon。
            if (TryProjectToFootprintPolygon(site, candidate, hexSize, out var projected, out _))
            {
                worldPosition = projected;
                return true;
            }

            return false;
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
            if (!TryComputeFootprintWorldDomain(site, hexSize, out var minX, out var maxX, out var minY, out var maxY))
                return false;

            var domainW = maxX - minX;
            var domainH = maxY - minY;
            if (domainW <= 0.0001f || domainH <= 0.0001f)
                return false;

            var wp = worldPosition;
            var hex = HexMath.WorldToHex(wp.X, wp.Y, hexSize);
            if (!site.OccupiesHex(hex))
            {
                if (!TryProjectToFootprintPolygon(site, wp, hexSize, out var projected, out _))
                    return false;
                wp = projected;
            }

            var u = Clamp01((wp.X - minX) / domainW);
            var v = Clamp01((wp.Y - minY) / domainH);
            localPosition = new WorldVec2(bounds.MinX + u * bounds.SpanX, bounds.MinY + v * bounds.SpanY);
            return true;
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
            if (site == null || hexSize <= 0.0001f)
                return false;

            var hex = HexMath.WorldToHex(worldPosition.X, worldPosition.Y, hexSize);
            if (site.OccupiesHex(hex))
            {
                footprintHex = hex;
                return true;
            }

            if (TryProjectToFootprintPolygon(site, worldPosition, hexSize, out _, out var nearest))
            {
                footprintHex = nearest;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 点到所有 occupied hex 多边形边界的最近投影（polygon 为凸正六边形，遍历 6 边即可）。
        /// 返回最近世界点 + 所属 hex。不做 nearest-hex-center（避免明显位置跳变）。
        /// </summary>
        static bool TryProjectToFootprintPolygon(
            WorldSite site,
            WorldVec2 candidate,
            float hexSize,
            out WorldVec2 projected,
            out HexCoord nearestHex)
        {
            projected = default;
            nearestHex = default;
            if (site == null || hexSize <= 0.0001f)
                return false;

            var bestDistSq = float.MaxValue;
            var found = false;

            foreach (var hex in site.EnumerateFootprintHexes())
            {
                HexMath.ToWorldPosition(hex, hexSize, out var cx, out var cy);
                for (var i = 0; i < 6; i++)
                {
                    var j = (i + 1) % 6;
                    var ax = cx + UnitCornerX[i] * hexSize;
                    var ay = cy + UnitCornerY[i] * hexSize;
                    var bx = cx + UnitCornerX[j] * hexSize;
                    var by = cy + UnitCornerY[j] * hexSize;
                    ClosestPointOnSegment(
                        candidate.X, candidate.Y,
                        ax, ay, bx, by,
                        out var px, out var py);
                    var dx = candidate.X - px;
                    var dy = candidate.Y - py;
                    var distSq = dx * dx + dy * dy;
                    if (distSq >= bestDistSq)
                        continue;
                    bestDistSq = distSq;
                    projected = new WorldVec2(px, py);
                    nearestHex = hex;
                    found = true;
                }
            }

            return found;
        }

        static void ClosestPointOnSegment(
            float px, float py,
            float ax, float ay, float bx, float by,
            out float cx, out float cy)
        {
            var dx = bx - ax;
            var dy = by - ay;
            var lenSq = dx * dx + dy * dy;
            var t = lenSq <= 0.0000001f ? 0f : Clamp01(((px - ax) * dx + (py - ay) * dy) / lenSq);
            cx = ax + t * dx;
            cy = ay + t * dy;
        }

        static float Clamp01(float v)
        {
            if (v < 0f)
                return 0f;
            if (v > 1f)
                return 1f;
            return v;
        }
    }
}
