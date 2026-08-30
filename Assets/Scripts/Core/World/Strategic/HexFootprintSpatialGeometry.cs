using System;
using System.Collections.Generic;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase 5R-B3C3：WorldSite footprint polygon union ↔ LocalMap rectangle 的 V2 连续映射几何。
    ///
    /// 拓扑（取代 V1 "Local rectangle → footprint AABB → 空洞 nearest projection"）：
    ///  - boundary：footprint 每 hex 的真实外露边（HexMath Pointy-Top / Odd-R 同一角点约定）；
    ///  - kernel：所有 boundary inward half-plane 的 Sutherland–Hodgman 凸裁剪交集；
    ///  - mapping：star-shaped radial bijection（Kernel 为原点，Local square [-1,1]² ↔ footprint 全域）。
    ///
    /// 职责边界：只做 Local↔World 物理位置映射；不做 Battle / Travel / Context / Army /
    /// Presence / Materialization / Ingress-Egress 行为（ADR-0027 §11）。纯 Core，零 UnityEngine。
    ///
    /// 性能：<see cref="TryBuild"/> 一次性构建（boundary + kernel），之后
    /// <see cref="TryLocalToWorldSurface"/> / <see cref="TryWorldSurfaceToLocal"/> 零堆分配
    /// （B4 每帧调用应在 Site materialize 时构建一次并复用本实例）。
    ///
    /// kernel-empty（footprint 非 star-shaped，如分离多片）：
    ///  <see cref="TryBuild"/> 仍返回 true（几何可构建、boundary 合法），但 <see cref="HasKernel"/>
    ///  = false，所有 radial 映射方法明确 return false（不 fallback V1 collapse）。
    /// </summary>
    public sealed class HexFootprintSpatialGeometry
    {
        /// <summary>boundary segment：世界坐标 A→B + 单位 inward 法线（指向 footprint 内部侧）。</summary>
        public readonly struct BoundarySegment
        {
            public readonly WorldVec2 A;
            public readonly WorldVec2 B;
            public readonly float NormalX;
            public readonly float NormalY;

            public BoundarySegment(WorldVec2 a, WorldVec2 b, float normalX, float normalY)
            {
                A = a;
                B = b;
                NormalX = normalX;
                NormalY = normalY;
            }
        }

        // pointy-top 单位六角角点（radius=1，angle = (π/3)·i + π/6，与
        // HexMath.CollectCornerWorldPositions 公式逐点一致；V1 已验证一致，单一来源）。
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

        readonly HexCoord[] _footprint;
        readonly float _hexSize;
        readonly BoundarySegment[] _boundary;
        readonly WorldVec2 _kernel;
        readonly bool _hasKernel;
        readonly float _minX;
        readonly float _maxX;
        readonly float _minY;
        readonly float _maxY;

        HexFootprintSpatialGeometry(
            HexCoord[] footprint,
            float hexSize,
            BoundarySegment[] boundary,
            WorldVec2 kernel,
            bool hasKernel,
            float minX,
            float maxX,
            float minY,
            float maxY)
        {
            _footprint = footprint;
            _hexSize = hexSize;
            _boundary = boundary;
            _kernel = kernel;
            _hasKernel = hasKernel;
            _minX = minX;
            _maxX = maxX;
            _minY = minY;
            _maxY = maxY;
        }

        public bool HasKernel => _hasKernel;

        public WorldVec2 Kernel => _kernel;

        public int BoundaryCount => _boundary.Length;

        /// <summary>boundary segments 只读视图（构建后不可变；调试 / 测试 / 预检用）。</summary>
        public BoundarySegment[] Boundary => _boundary;

        public int FootprintCount => _footprint.Length;

        public float HexSize => _hexSize;

        public float MinX => _minX;
        public float MaxX => _maxX;
        public float MinY => _minY;
        public float MaxY => _maxY;

        public IReadOnlyList<HexCoord> Footprint => _footprint;

        // ============================ 构建 ============================

        /// <summary>
        /// 构建 V2 几何：footprint 快照 + 真实 boundary edges + kernel。
        /// footprint 为空 / 非法 → false。footprint 非 star-shaped → 返回 true 但 <see cref="HasKernel"/> = false。
        /// </summary>
        public static bool TryBuild(
            IReadOnlyList<HexCoord> footprint,
            float hexSize,
            out HexFootprintSpatialGeometry geometry)
        {
            geometry = null;
            if (footprint == null || footprint.Count == 0 || hexSize <= 0.0001f)
                return false;

            var copy = new HexCoord[footprint.Count];
            var fpSet = new HashSet<HexCoord>();
            for (var f = 0; f < footprint.Count; f++)
            {
                copy[f] = footprint[f];
                fpSet.Add(footprint[f]);
            }

            // boundary edges + world domain（真实 hex 角点外接框，仅 debug）。
            var boundary = new List<BoundarySegment>(footprint.Count * 3 + 4);
            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minY = float.MaxValue;
            var maxY = float.MinValue;

            // 角点缓存移出 hex 循环（footprint 构建期一次性，避免循环内 stackalloc）。
            Span<float> cornerX = stackalloc float[6];
            Span<float> cornerY = stackalloc float[6];
            for (var f = 0; f < footprint.Count; f++)
            {
                var hex = footprint[f];
                HexMath.ToWorldPosition(hex, hexSize, out var cx, out var cy);
                for (var i = 0; i < 6; i++)
                {
                    cornerX[i] = cx + UnitCornerX[i] * hexSize;
                    cornerY[i] = cy + UnitCornerY[i] * hexSize;
                    minX = Math.Min(minX, cornerX[i]);
                    maxX = Math.Max(maxX, cornerX[i]);
                    minY = Math.Min(minY, cornerY[i]);
                    maxY = Math.Max(maxY, cornerY[i]);
                }

                // edge(i, i+1) 的世界外法线角 = (π/3)·(i+1)（东=0°：边(5,0)→E, (0,1)→NE, (1,2)→NW,
                // (2,3)→W, (3,4)→SW, (4,5)→SE）。邻居方向 d 的世界方向与轴向标签在 Odd-R 中不同
                // （偶行 hex 的 axial-SE 邻居在世界方向 60°/NE，与奇行一致）；逐方向匹配得
                // 边(i,i+1) 的外邻居 = HexMath.Neighbor(hex, (5-i)%6)：
                //   i=0(NE)↔d=5, i=1(NW)↔d=4, i=2(W)↔d=3, i=3(SW)↔d=2, i=4(SE)↔d=1, i=5(E)↔d=0。
                // 邻居不在 footprint → 该边为 footprint exterior boundary edge；
                // inward 法线 = normalize(hexCenter - edgeMid)（owning hex 恒在 footprint 内部侧）。
                for (var i = 0; i < 6; i++)
                {
                    var j = (i + 1) % 6;
                    var neighbor = HexMath.Neighbor(hex, (5 - i) % 6);
                    if (fpSet.Contains(neighbor))
                        continue;

                    var ax = cornerX[i];
                    var ay = cornerY[i];
                    var bx = cornerX[j];
                    var by = cornerY[j];
                    var mx = (ax + bx) * 0.5f;
                    var my = (ay + by) * 0.5f;
                    var nx = cx - mx;
                    var ny = cy - my;
                    var len = (float)Math.Sqrt(nx * nx + ny * ny);
                    if (len <= 0.000001f)
                        continue;
                    boundary.Add(new BoundarySegment(
                        new WorldVec2(ax, ay),
                        new WorldVec2(bx, by),
                        nx / len,
                        ny / len));
                }
            }

            if (boundary.Count < 3 || minX > maxX || minY > maxY)
                return false;

            var kernelOk = TryComputeKernel(boundary, minX, maxX, minY, maxY, hexSize, out var kernel);
            geometry = new HexFootprintSpatialGeometry(
                copy, hexSize, boundary.ToArray(), kernel, kernelOk, minX, maxX, minY, maxY);
            return true;
        }

        /// <summary>
        /// Kernel = 所有 boundary inward half-plane 的交集（Sutherland–Hodgman 凸裁剪）。
        /// 从覆盖 footprint world domain 的外扩凸矩形开始；结果为空 → 非 star-shaped（return false）。
        /// </summary>
        static bool TryComputeKernel(
            List<BoundarySegment> boundary,
            float minX,
            float maxX,
            float minY,
            float maxY,
            float hexSize,
            out WorldVec2 kernel)
        {
            kernel = default;
            var eps = 1e-4f * Math.Max(hexSize, 1f);
            var margin = Math.Max(maxX - minX, maxY - minY) * 0.25f + hexSize;

            var poly = new List<WorldVec2>(16)
            {
                new WorldVec2(minX - margin, minY - margin),
                new WorldVec2(maxX + margin, minY - margin),
                new WorldVec2(maxX + margin, maxY + margin),
                new WorldVec2(minX - margin, maxY + margin),
            };

            var next = new List<WorldVec2>(16);
            for (var s = 0; s < boundary.Count; s++)
            {
                var seg = boundary[s];
                var nx = seg.NormalX;
                var ny = seg.NormalY;
                var ax = seg.A.X;
                var ay = seg.A.Y;

                next.Clear();
                for (var k = 0; k < poly.Count; k++)
                {
                    var cur = poly[k];
                    var nxt = poly[(k + 1) % poly.Count];
                    var curIn = (cur.X - ax) * nx + (cur.Y - ay) * ny >= -eps;
                    var nxtIn = (nxt.X - ax) * nx + (nxt.Y - ay) * ny >= -eps;

                    if (curIn)
                        next.Add(cur);

                    if (curIn != nxtIn)
                    {
                        var dx = nxt.X - cur.X;
                        var dy = nxt.Y - cur.Y;
                        var denom = dx * nx + dy * ny;
                        if (Math.Abs(denom) > 1e-12f)
                        {
                            var t = ((ax - cur.X) * nx + (ay - cur.Y) * ny) / denom;
                            next.Add(new WorldVec2(cur.X + t * dx, cur.Y + t * dy));
                        }
                    }
                }

                var tmp = poly;
                poly = next;
                next = tmp;
                if (poly.Count == 0)
                    return false;
            }

            if (poly.Count == 0)
                return false;

            // 最终仲裁：K = 交集内一点。退化交集（site_a/site_b 类：20 半平面收敛到单点/线段）
            // 是合法 kernel（star-shaped 退化），不能被面积质心误拒（area2≈0）。
            //  - &gt;=3 顶点：面积加权质心（凸多边形稳定）；面积≈0（全重合）→ 顶点平均。
            //  - 2 顶点：交集退化为线段 → 取中点。
            //  - 1 顶点：交集退化为单点。
            WorldVec2 candidate;
            if (poly.Count >= 3)
            {
                double area2 = 0.0;
                double cx = 0.0;
                double cy = 0.0;
                for (var k = 0; k < poly.Count; k++)
                {
                    var p = poly[k];
                    var q = poly[(k + 1) % poly.Count];
                    var cross = p.X * (double)q.Y - p.Y * (double)q.X;
                    area2 += cross;
                    cx += (p.X + (double)q.X) * cross;
                    cy += (p.Y + (double)q.Y) * cross;
                }

                if (Math.Abs(area2) >= 1e-9)
                {
                    candidate = new WorldVec2((float)(cx / (3.0 * area2)), (float)(cy / (3.0 * area2)));
                }
                else
                {
                    var ax = 0f;
                    var ay = 0f;
                    for (var k = 0; k < poly.Count; k++)
                    {
                        ax += poly[k].X;
                        ay += poly[k].Y;
                    }

                    candidate = new WorldVec2(ax / poly.Count, ay / poly.Count);
                }
            }
            else if (poly.Count == 2)
            {
                candidate = new WorldVec2(
                    (poly[0].X + poly[1].X) * 0.5f,
                    (poly[0].Y + poly[1].Y) * 0.5f);
            }
            else
            {
                candidate = poly[0];
            }

            // 防御验证：K 必须满足全部 boundary inward 半平面（数值兜底，宽容差）。
            var tol = 1e-3f * Math.Max(hexSize, 1f);
            for (var s = 0; s < boundary.Count; s++)
            {
                var seg = boundary[s];
                if ((candidate.X - seg.A.X) * seg.NormalX +
                    (candidate.Y - seg.A.Y) * seg.NormalY < -tol)
                {
                    return false;
                }
            }

            kernel = candidate;
            return true;
        }

        // ============================ Local → World（radial） ============================

        /// <summary>
        /// Local 矩形点 → footprint 全域（kernel 原点 star-shaped radial）：
        ///  u,v → square Q=(2u-1,2v-1) → r=|Q|∞, S=Q/r（square perimeter）→ ray(K,S) ∩ boundary = B →
        ///  world = K + r·(B-K)。Local 矩形边界 → polygon boundary；内部 → 内部。零分配。
        /// </summary>
        public bool TryLocalToWorldSurface(
            float localMinX,
            float localMaxX,
            float localMinY,
            float localMaxY,
            WorldVec2 localPosition,
            out WorldVec2 worldPosition)
        {
            worldPosition = default;
            if (!_hasKernel)
                return false;

            var spanX = Math.Max(0.0001f, localMaxX - localMinX);
            var spanY = Math.Max(0.0001f, localMaxY - localMinY);
            var u = (localPosition.X - localMinX) / spanX;
            var v = (localPosition.Y - localMinY) / spanY;
            var qx = 2f * u - 1f;
            var qy = 2f * v - 1f;
            var r = Math.Max(Math.Abs(qx), Math.Abs(qy));
            if (r <= 0.000001f)
            {
                worldPosition = _kernel;
                return true;
            }

            var sx = qx / r;
            var sy = qy / r;
            if (!TryRayBoundary(_kernel.X, _kernel.Y, sx, sy, out var bx, out var by))
                return false;

            worldPosition = new WorldVec2(
                _kernel.X + r * (bx - _kernel.X),
                _kernel.Y + r * (by - _kernel.Y));
            return true;
        }

        // ============================ World → Local（radial inverse） ============================

        /// <summary>
        /// footprint 全域点 → Local 矩形（与 <see cref="TryLocalToWorldSurface"/> 同一 ray/boundary authority）：
        ///  V=world-K → 单位方向 D → ray(K,D) ∩ boundary = B → r=|V|/|B-K| → S=V/|V|∞（square perimeter）
        ///  → Q=r·S → u=(Qx+1)/2, v=(Qy+1)/2 → Local。零分配。
        /// </summary>
        public bool TryWorldSurfaceToLocal(
            float localMinX,
            float localMaxX,
            float localMinY,
            float localMaxY,
            WorldVec2 worldPosition,
            out WorldVec2 localPosition)
        {
            localPosition = default;
            if (!_hasKernel)
                return false;

            var vx = worldPosition.X - _kernel.X;
            var vy = worldPosition.Y - _kernel.Y;
            var mag = Math.Sqrt(vx * (double)vx + vy * (double)vy);
            if (mag <= 1e-6)
            {
                localPosition = new WorldVec2((localMinX + localMaxX) * 0.5f, (localMinY + localMaxY) * 0.5f);
                return true;
            }

            var dx = (float)(vx / mag);
            var dy = (float)(vy / mag);
            if (!TryRayBoundary(_kernel.X, _kernel.Y, dx, dy, out var bx, out var by))
                return false;

            var bdx = bx - _kernel.X;
            var bdy = by - _kernel.Y;
            var bLen = Math.Sqrt(bdx * (double)bdx + bdy * (double)bdy);
            if (bLen <= 1e-9)
                return false;

            var r = (float)(mag / bLen);
            var inv = 1f / Math.Max(Math.Abs(dx), Math.Abs(dy));
            var qx = r * dx * inv;
            var qy = r * dy * inv;
            var u = (qx + 1f) * 0.5f;
            var v = (qy + 1f) * 0.5f;
            localPosition = new WorldVec2(
                localMinX + u * (localMaxX - localMinX),
                localMinY + v * (localMaxY - localMinY));
            return true;
        }

        // ============================ 独立投影 helper（降级用，不是主 mapping） ============================

        /// <summary>
        /// 点到 footprint 全部 hex 多边形边的最近投影（独立 helper；仅用于 legacy / DerivedHex 等
        /// 需要"最近合法点"的场景，不混入 coordinate mapping）。零分配。
        /// </summary>
        public bool TryProjectWorldPointToFootprint(
            WorldVec2 point,
            out WorldVec2 projected,
            out HexCoord nearestHex)
        {
            projected = default;
            nearestHex = default;
            if (_footprint.Length == 0)
                return false;

            var bestDistSq = double.MaxValue;
            var found = false;
            for (var f = 0; f < _footprint.Length; f++)
            {
                var hex = _footprint[f];
                HexMath.ToWorldPosition(hex, _hexSize, out var cx, out var cy);
                for (var i = 0; i < 6; i++)
                {
                    var j = (i + 1) % 6;
                    ClosestPointOnSegment(
                        point.X, point.Y,
                        cx + UnitCornerX[i] * _hexSize,
                        cy + UnitCornerY[i] * _hexSize,
                        cx + UnitCornerX[j] * _hexSize,
                        cy + UnitCornerY[j] * _hexSize,
                        out var px, out var py);
                    var dx = point.X - px;
                    var dy = point.Y - py;
                    var distSq = dx * (double)dx + dy * (double)dy;
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

        // ============================ ray ∩ boundary ============================

        /// <summary>
        /// Ray(origin, dir) 与 boundary segments 的最小正 t 交点（双向映射共用同一 authority）。
        /// dir 不必为单位向量（t 为沿 dir 的比例）。vertex / 平行 / epsilon 确定性处理：
        ///  - 平行（|cross| &lt; eps）跳过；
        ///  - t &lt; tiny 视为起点处（kernel 严格内部，正常 t 应 &gt; 0）；
        ///  - segment u 参数带容差。
        /// </summary>
        bool TryRayBoundary(float originX, float originY, float dirX, float dirY, out float hitX, out float hitY)
        {
            hitX = hitY = 0f;
            if (Math.Abs(dirX) < 1e-12f && Math.Abs(dirY) < 1e-12f)
                return false;

            var bestT = double.MaxValue;
            for (var s = 0; s < _boundary.Length; s++)
            {
                var ax = _boundary[s].A.X;
                var ay = _boundary[s].A.Y;
                var ex = _boundary[s].B.X - ax;
                var ey = _boundary[s].B.Y - ay;
                var denom = dirX * (double)ey - dirY * (double)ex;
                if (Math.Abs(denom) < 1e-9)
                    continue;

                // t = cross(A-O, E) / cross(D, E)；u = cross(A-O, D) / cross(D, E)
                // （O=origin, A=segment start, D=ray dir, E=segment dir）
                var vx = ax - originX;
                var vy = ay - originY;
                var t = (vx * ey - vy * ex) / denom;
                if (t < 1e-7)
                    continue;
                var u = (vx * dirY - vy * dirX) / denom;
                if (u < -1e-6 || u > 1.0 + 1e-6)
                    continue;
                if (t < bestT)
                    bestT = t;
            }

            if (bestT >= double.MaxValue)
                return false;

            hitX = (float)(originX + bestT * dirX);
            hitY = (float)(originY + bestT * dirY);
            return true;
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
