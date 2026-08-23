using System;
using System.Collections.Generic;

namespace XianXia.Core.World.Hex
{
    public static class HexMath
    {
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
            if (directionIndex < 0 || directionIndex >= AxialDirections.Length)
                throw new ArgumentOutOfRangeException(nameof(directionIndex));
            return Add(coord, AxialDirections[directionIndex]);
        }

        public static void CollectNeighbors(HexCoord coord, List<HexCoord> neighborsOut)
        {
            neighborsOut.Clear();
            for (var i = 0; i < AxialDirections.Length; i++)
                neighborsOut.Add(Add(coord, AxialDirections[i]));
        }

        public static int Distance(HexCoord a, HexCoord b)
        {
            var dq = Math.Abs(a.Q - b.Q);
            var dr = Math.Abs(a.R - b.R);
            var ds = Math.Abs(a.S - b.S);
            return (dq + dr + ds) / 2;
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

        /// <summary>世界坐标 → 最近 Hex（Odd-R offset，与矩形 100×50 布局一致）。</summary>
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