using System;
using System.Collections.Generic;

namespace XianXia.Core.World.Hex
{
    /// <summary>
    /// Hex 拓扑 Authority。
    /// 存储坐标为 <b>Odd-R offset</b>（Q=列, R=行），布局为 pointy-top。
    /// 邻居 / 距离一律：Odd-R → axial → 计算 → 必要时再转回 Odd-R。
    /// 禁止把存储坐标直接当 axial 加减方向表。
    /// </summary>
    public static class HexMath
    {
        public const int DirectionCount = 6;

        /// <summary>
        /// Axial 方向表（E, NE, NW, W, SW, SE）。
        /// 仅可作用于 axial 坐标；对存储的 Odd-R <see cref="HexCoord"/> 必须经
        /// <see cref="Neighbor"/> / <see cref="CollectNeighbors"/>。
        /// </summary>
        public static readonly HexCoord[] AxialDirections =
        {
            new HexCoord(1, 0),
            new HexCoord(1, -1),
            new HexCoord(0, -1),
            new HexCoord(-1, 0),
            new HexCoord(-1, 1),
            new HexCoord(0, 1),
        };

        public static HexCoord Add(HexCoord a, HexCoord b) => new HexCoord(a.Q + b.Q, a.R + b.R);

        public static HexCoord Neighbor(HexCoord coord, int directionIndex)
        {
            if (directionIndex < 0 || directionIndex >= DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(directionIndex));

            OffsetOddRToAxial(coord, out var aq, out var ar);
            var d = AxialDirections[directionIndex];
            return AxialToOffsetOddR(aq + d.Q, ar + d.R);
        }

        public static void CollectNeighbors(HexCoord coord, List<HexCoord> neighborsOut)
        {
            neighborsOut.Clear();
            OffsetOddRToAxial(coord, out var aq, out var ar);
            for (var i = 0; i < AxialDirections.Length; i++)
            {
                var d = AxialDirections[i];
                neighborsOut.Add(AxialToOffsetOddR(aq + d.Q, ar + d.R));
            }
        }

        /// <summary>Odd-R offset → cube/axial 距离。</summary>
        public static int Distance(HexCoord a, HexCoord b)
        {
            OffsetOddRToAxial(a, out var aq, out var ar);
            OffsetOddRToAxial(b, out var bq, out var br);
            var asCube = -aq - ar;
            var bsCube = -bq - br;
            return (Math.Abs(aq - bq) + Math.Abs(ar - br) + Math.Abs(asCube - bsCube)) / 2;
        }

        /// <summary>
        /// Odd-R 直线：先转 axial/cube，再 lerp + round，再转回 Odd-R。
        /// 禁止对存储的 (Q,R) 直接做 cube lerp。
        /// </summary>
        public static void CollectHexLine(HexCoord from, HexCoord to, List<HexCoord> pathOut)
        {
            if (pathOut == null)
                throw new ArgumentNullException(nameof(pathOut));

            pathOut.Clear();
            var steps = Distance(from, to);
            if (steps <= 0)
            {
                pathOut.Add(from);
                return;
            }

            OffsetOddRToAxial(from, out var aq, out var ar);
            OffsetOddRToAxial(to, out var bq, out var br);
            var asCube = -aq - ar;
            var bsCube = -bq - br;

            for (var i = 0; i <= steps; i++)
            {
                var t = i / (float)steps;
                var q = aq + (bq - aq) * t;
                var r = ar + (br - ar) * t;
                var s = asCube + (bsCube - asCube) * t;
                CubeRound(q, r, s, out var rq, out var rr);
                pathOut.Add(AxialToOffsetOddR(rq, rr));
            }
        }

        /// <summary>Odd-R（col,row）→ axial（q,r）。</summary>
        public static void OffsetOddRToAxial(HexCoord offset, out int axialQ, out int axialR)
        {
            axialQ = offset.Q - (offset.R - (offset.R & 1)) / 2;
            axialR = offset.R;
        }

        /// <summary>axial（q,r）→ Odd-R（col,row）。</summary>
        public static HexCoord AxialToOffsetOddR(int axialQ, int axialR)
        {
            var col = axialQ + (axialR - (axialR & 1)) / 2;
            return new HexCoord(col, axialR);
        }

        static void CubeRound(float q, float r, float s, out int roundQ, out int roundR)
        {
            var rq = Math.Round(q);
            var rr = Math.Round(r);
            var rs = Math.Round(s);

            var dq = Math.Abs(rq - q);
            var dr = Math.Abs(rr - r);
            var ds = Math.Abs(rs - s);

            if (dq > dr && dq > ds)
                rq = -rr - rs;
            else if (dr > ds)
                rr = -rq - rs;

            roundQ = (int)rq;
            roundR = (int)rr;
        }

        /// <summary>两格中心世界距离是否等于 pointy-top 共边邻居间距（√3 · hexSize）。</summary>
        public static bool AreWorldEdgeAdjacent(HexCoord a, HexCoord b, float hexSize, float relativeTolerance = 0.02f)
        {
            if (hexSize <= 0.0001f)
                return false;
            ToWorldPosition(a, hexSize, out var ax, out var ay);
            ToWorldPosition(b, hexSize, out var bx, out var by);
            var dx = ax - bx;
            var dy = ay - by;
            var dist = Math.Sqrt(dx * dx + dy * dy);
            var expected = Math.Sqrt(3.0) * hexSize;
            return Math.Abs(dist - expected) <= expected * relativeTolerance;
        }

        /// <summary>Odd-R 矩形布局：Q=列，R=行 → 世界平面（Presentation + Picking 共用）。</summary>
        public static void ToWorldPosition(HexCoord coord, float hexSize, out float worldX, out float worldY) =>
            HexWorldLayout.CoordToWorldCenter(coord, hexSize, out worldX, out worldY);

        public static void ToWorldPosition(
            HexCoord from,
            HexCoord to,
            float stepProgress,
            float hexSize,
            out float worldX,
            out float worldY)
        {
            ToWorldPosition(from, hexSize, out var fx, out var fy);
            ToWorldPosition(to, hexSize, out var tx, out var ty);
            var t = Math.Max(0f, Math.Min(1f, stepProgress));
            worldX = fx + (tx - fx) * t;
            worldY = fy + (ty - fy) * t;
        }

        /// <summary>世界坐标 → 最近 Hex（Odd-R offset，与矩形布局一致）。</summary>
        public static HexCoord WorldToHex(float worldX, float worldY, float hexSize) =>
            HexWorldLayout.WorldToCoord(worldX, worldY, hexSize);

        public static void CollectCornerWorldPositions(
            HexCoord coord,
            float hexSize,
            float[] cornerWorldX,
            float[] cornerWorldY)
        {
            if (cornerWorldX == null || cornerWorldY == null || cornerWorldX.Length < 6 || cornerWorldY.Length < 6)
                throw new ArgumentException("corner arrays must have length >= 6");

            ToWorldPosition(coord, hexSize, out var cx, out var cy);
            for (var i = 0; i < 6; i++)
            {
                var angle = (Math.PI / 3.0) * i + Math.PI / 6.0;
                cornerWorldX[i] = cx + hexSize * (float)Math.Cos(angle);
                cornerWorldY[i] = cy + hexSize * (float)Math.Sin(angle);
            }
        }
    }
}
