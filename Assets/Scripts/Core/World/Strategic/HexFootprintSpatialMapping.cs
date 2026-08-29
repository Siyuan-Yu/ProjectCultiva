using System;
using System.Collections.Generic;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase 5R-B3A：LocalMap playable bounds + Hex footprint ↔ 连续 WorldSurface 的<b>共享纯 Core 映射</b>。
    ///
    /// 统一两种 footprint 的 world-surface 几何语义（真实 Pointy-Top / Odd-R Hex polygon）：
    ///  - Wilderness：footprint = 单个 Hex（LocalMap 边缘 → 该 Hex 的真实 polygon boundary）；
    ///  - WorldSite：footprint = Site.OccupiedHexes（多 Hex 外接域 + irregular 空洞投影）。
    ///
    /// 职责边界（ADR-0027 §11）：只做 Local ↔ World 物理位置映射；不负责 Battle / Travel /
    /// PlayerParty Context / Army / Presence / Materialization / Ingress-Egress 行为。
    ///
    /// 无 UnityEngine / XianXia.Data 依赖（Core 零引用程序集）。<b>无 per-call 堆分配</b>：
    ///  - 六角角点用 static readonly 常量（不 new float[]）；
    ///  - 多 Hex 直接消费调用方已有的 IReadOnlyList（WorldSite.OccupiedHexes 为缓存 ReadOnlyCollection）；
    ///  - 单 Hex 用 struct 包装（SingleHexFootprint）经泛型单核调用，无装箱、无列表分配；
    ///  - 全部算法只走一个泛型核心，不存在第二套会漂移的复制公式。
    /// </summary>
    public static class HexFootprintSpatialMapping
    {
        /// <summary>pointy-top 单位六角角点（radius=1，angle = (π/3)·i + π/6，与
        /// <see cref="HexMath.CollectCornerWorldPositions"/> 公式逐点一致）。static readonly，只读线程安全。</summary>
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

        /// <summary>满足 IEnumerator 约束的共享空枚举器（单例，零分配；映射核心从不迭代）。</summary>
        sealed class EmptyHexEnumerator : IEnumerator<HexCoord>
        {
            public static readonly EmptyHexEnumerator Instance = new EmptyHexEnumerator();

            public HexCoord Current => default;
            object System.Collections.IEnumerator.Current => default;
            public bool MoveNext() => false;
            public void Reset() { }
            public void Dispose() { }
        }

        /// <summary>单 Hex footprint 的零分配只读包装（仅暴露 Count / 索引；不产生枚举器分配）。</summary>
        readonly struct SingleHexFootprint : IReadOnlyList<HexCoord>
        {
            readonly HexCoord _hex;

            public SingleHexFootprint(HexCoord hex)
            {
                _hex = hex;
            }

            public int Count => 1;

            public HexCoord this[int index] => _hex;

            public IEnumerator<HexCoord> GetEnumerator() => EmptyHexEnumerator.Instance;

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
                EmptyHexEnumerator.Instance;
        }

        // ============================ footprint 世界域 ============================

        /// <summary>单 Hex：真实角点外接框（axis-aligned），即该 Hex polygon 的 world-surface bbox。</summary>
        public static bool TryComputeWorldDomain(
            HexCoord hex,
            float hexSize,
            out float minX,
            out float maxX,
            out float minY,
            out float maxY)
        {
            minX = maxX = minY = maxY = 0f;
            if (hexSize <= 0.0001f)
                return false;
            HexMath.ToWorldPosition(hex, hexSize, out var cx, out var cy);
            return ComputeDomainCorners(cx, cy, hexSize, out minX, out maxX, out minY, out maxY);
        }

        /// <summary>多 Hex footprint：全部 hex 角点外接框。</summary>
        public static bool TryComputeWorldDomain(
            IReadOnlyList<HexCoord> footprint,
            float hexSize,
            out float minX,
            out float maxX,
            out float minY,
            out float maxY)
        {
            minX = maxX = minY = maxY = 0f;
            if (footprint == null || footprint.Count == 0 || hexSize <= 0.0001f)
                return false;

            var any = false;
            for (var f = 0; f < footprint.Count; f++)
            {
                HexMath.ToWorldPosition(footprint[f], hexSize, out var cx, out var cy);
                if (!ComputeDomainCorners(cx, cy, hexSize, out var hxMin, out var hxMax, out var hyMin, out var hyMax))
                    continue;
                minX = any ? Math.Min(minX, hxMin) : hxMin;
                maxX = any ? Math.Max(maxX, hxMax) : hxMax;
                minY = any ? Math.Min(minY, hyMin) : hyMin;
                maxY = any ? Math.Max(maxY, hyMax) : hyMax;
                any = true;
            }

            return any;
        }

        static bool ComputeDomainCorners(
            float cx,
            float cy,
            float hexSize,
            out float minX,
            out float maxX,
            out float minY,
            out float maxY)
        {
            minX = cx + UnitCornerX[0] * hexSize;
            maxX = minX;
            minY = cy + UnitCornerY[0] * hexSize;
            maxY = minY;
            for (var i = 1; i < 6; i++)
            {
                var x = cx + UnitCornerX[i] * hexSize;
                var y = cy + UnitCornerY[i] * hexSize;
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }

            return true;
        }

        // ============================ Local → World ============================

        /// <summary>单 Hex：Local normalized (u,v) → Hex polygon world bbox → 验证/投影。</summary>
        public static bool TryLocalToWorldSurface(
            HexCoord hex,
            float localMinX,
            float localMaxX,
            float localMinY,
            float localMaxY,
            WorldVec2 localPosition,
            float hexSize,
            out WorldVec2 worldPosition) =>
            TryLocalToWorldSurfaceCore(
                new SingleHexFootprint(hex),
                localMinX, localMaxX, localMinY, localMaxY,
                localPosition, hexSize, out worldPosition);

        /// <summary>多 Hex footprint：Local normalized (u,v) → footprint world bbox → 验证/投影。</summary>
        public static bool TryLocalToWorldSurface(
            IReadOnlyList<HexCoord> footprint,
            float localMinX,
            float localMaxX,
            float localMinY,
            float localMaxY,
            WorldVec2 localPosition,
            float hexSize,
            out WorldVec2 worldPosition)
        {
            worldPosition = default;
            if (footprint == null || footprint.Count == 0)
                return false;
            return TryLocalToWorldSurfaceCore(
                footprint,
                localMinX, localMaxX, localMinY, localMaxY,
                localPosition, hexSize, out worldPosition);
        }

        static bool TryLocalToWorldSurfaceCore<T>(
            T footprint,
            float localMinX,
            float localMaxX,
            float localMinY,
            float localMaxY,
            WorldVec2 localPosition,
            float hexSize,
            out WorldVec2 worldPosition)
            where T : IReadOnlyList<HexCoord>
        {
            worldPosition = default;
            if (footprint == null || footprint.Count == 0 || hexSize <= 0.0001f)
                return false;
            if (!TryComputeWorldDomainCore(footprint, hexSize, out var dMinX, out var dMaxX, out var dMinY, out var dMaxY))
                return false;

            var domainW = dMaxX - dMinX;
            var domainH = dMaxY - dMinY;
            if (domainW <= 0.0001f || domainH <= 0.0001f)
                return false;

            var spanX = Math.Max(0.0001f, localMaxX - localMinX);
            var spanY = Math.Max(0.0001f, localMaxY - localMinY);
            var u = Clamp01((localPosition.X - localMinX) / spanX);
            var v = Clamp01((localPosition.Y - localMinY) / spanY);
            var candidate = new WorldVec2(dMinX + u * domainW, dMinY + v * domainH);

            var candidateHex = HexMath.WorldToHex(candidate.X, candidate.Y, hexSize);
            if (FootprintContainsHex(footprint, candidateHex))
            {
                worldPosition = candidate;
                return true;
            }

            // irregular/concave（或单 Hex bbox 角落）：candidate 落在 footprint 外 → 投影到最近 polygon。
            if (TryProjectToFootprintPolygonCore(footprint, candidate, hexSize, out var projected, out _))
            {
                worldPosition = projected;
                return true;
            }

            return false;
        }

        // ============================ World → Local ============================

        /// <summary>单 Hex：worldPosition → 该 Hex world bbox normalized (u,v) → Local。近似可逆。</summary>
        public static bool TryWorldSurfaceToLocal(
            HexCoord hex,
            float localMinX,
            float localMaxX,
            float localMinY,
            float localMaxY,
            WorldVec2 worldPosition,
            float hexSize,
            out WorldVec2 localPosition) =>
            TryWorldSurfaceToLocalCore(
                new SingleHexFootprint(hex),
                localMinX, localMaxX, localMinY, localMaxY,
                worldPosition, hexSize, out localPosition);

        /// <summary>多 Hex footprint：worldPosition → footprint world bbox normalized (u,v) → Local。</summary>
        public static bool TryWorldSurfaceToLocal(
            IReadOnlyList<HexCoord> footprint,
            float localMinX,
            float localMaxX,
            float localMinY,
            float localMaxY,
            WorldVec2 worldPosition,
            float hexSize,
            out WorldVec2 localPosition)
        {
            localPosition = default;
            if (footprint == null || footprint.Count == 0)
                return false;
            return TryWorldSurfaceToLocalCore(
                footprint,
                localMinX, localMaxX, localMinY, localMaxY,
                worldPosition, hexSize, out localPosition);
        }

        static bool TryWorldSurfaceToLocalCore<T>(
            T footprint,
            float localMinX,
            float localMaxX,
            float localMinY,
            float localMaxY,
            WorldVec2 worldPosition,
            float hexSize,
            out WorldVec2 localPosition)
            where T : IReadOnlyList<HexCoord>
        {
            localPosition = default;
            if (footprint == null || footprint.Count == 0 || hexSize <= 0.0001f)
                return false;
            if (!TryComputeWorldDomainCore(footprint, hexSize, out var dMinX, out var dMaxX, out var dMinY, out var dMaxY))
                return false;

            var domainW = dMaxX - dMinX;
            var domainH = dMaxY - dMinY;
            if (domainW <= 0.0001f || domainH <= 0.0001f)
                return false;

            var wp = worldPosition;
            if (!FootprintContainsHex(footprint, HexMath.WorldToHex(wp.X, wp.Y, hexSize)))
            {
                if (!TryProjectToFootprintPolygonCore(footprint, wp, hexSize, out var projected, out _))
                    return false;
                wp = projected;
            }

            var spanX = Math.Max(0.0001f, localMaxX - localMinX);
            var spanY = Math.Max(0.0001f, localMaxY - localMinY);
            var u = Clamp01((wp.X - dMinX) / domainW);
            var v = Clamp01((wp.Y - dMinY) / domainH);
            localPosition = new WorldVec2(localMinX + u * spanX, localMinY + v * spanY);
            return true;
        }

        // ============================ Derived footprint Hex ============================

        /// <summary>单 Hex：worldPosition → 归属 hex（containment，边界歧义用 polygon 投影稳定解析）。</summary>
        public static bool TryResolveFootprintHex(
            HexCoord hex,
            WorldVec2 worldPosition,
            float hexSize,
            out HexCoord footprintHex)
        {
            footprintHex = default;
            if (hexSize <= 0.0001f)
                return false;

            if (HexMath.WorldToHex(worldPosition.X, worldPosition.Y, hexSize) == hex)
            {
                footprintHex = hex;
                return true;
            }

            if (TryProjectToFootprintPolygonCore(
                    new SingleHexFootprint(hex), worldPosition, hexSize, out _, out var nearest))
            {
                footprintHex = nearest;
                return true;
            }

            return false;
        }

        /// <summary>多 Hex footprint：worldPosition → 最近合法 footprint hex（containment，否则 polygon 投影）。</summary>
        public static bool TryResolveFootprintHex(
            IReadOnlyList<HexCoord> footprint,
            WorldVec2 worldPosition,
            float hexSize,
            out HexCoord footprintHex)
        {
            footprintHex = default;
            if (footprint == null || footprint.Count == 0 || hexSize <= 0.0001f)
                return false;

            var candidate = HexMath.WorldToHex(worldPosition.X, worldPosition.Y, hexSize);
            if (FootprintContainsHex(footprint, candidate))
            {
                footprintHex = candidate;
                return true;
            }

            if (TryProjectToFootprintPolygonCore(footprint, worldPosition, hexSize, out _, out var nearest))
            {
                footprintHex = nearest;
                return true;
            }

            return false;
        }

        // ============================ 共享核心 ============================

        static bool TryComputeWorldDomainCore<T>(
            T footprint,
            float hexSize,
            out float minX,
            out float maxX,
            out float minY,
            out float maxY)
            where T : IReadOnlyList<HexCoord>
        {
            minX = maxX = minY = maxY = 0f;
            if (footprint == null || footprint.Count == 0 || hexSize <= 0.0001f)
                return false;

            var any = false;
            for (var f = 0; f < footprint.Count; f++)
            {
                HexMath.ToWorldPosition(footprint[f], hexSize, out var cx, out var cy);
                if (!ComputeDomainCorners(cx, cy, hexSize, out var hxMin, out var hxMax, out var hyMin, out var hyMax))
                    continue;
                minX = any ? Math.Min(minX, hxMin) : hxMin;
                maxX = any ? Math.Max(maxX, hxMax) : hxMax;
                minY = any ? Math.Min(minY, hyMin) : hyMin;
                maxY = any ? Math.Max(maxY, hyMax) : hyMax;
                any = true;
            }

            return any;
        }

        static bool FootprintContainsHex<T>(T footprint, HexCoord hex)
            where T : IReadOnlyList<HexCoord>
        {
            for (var i = 0; i < footprint.Count; i++)
            {
                if (footprint[i] == hex)
                    return true;
            }

            return false;
        }

        /// <summary>点到所有 footprint hex 多边形边的最近投影（正六边形凸多边形，遍历 6 边即可）。</summary>
        static bool TryProjectToFootprintPolygonCore<T>(
            T footprint,
            WorldVec2 candidate,
            float hexSize,
            out WorldVec2 projected,
            out HexCoord nearestHex)
            where T : IReadOnlyList<HexCoord>
        {
            projected = default;
            nearestHex = default;
            if (footprint == null || footprint.Count == 0 || hexSize <= 0.0001f)
                return false;

            var bestDistSq = float.MaxValue;
            var found = false;

            for (var f = 0; f < footprint.Count; f++)
            {
                var hex = footprint[f];
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
            float px,
            float py,
            float ax,
            float ay,
            float bx,
            float by,
            out float cx,
            out float cy)
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
