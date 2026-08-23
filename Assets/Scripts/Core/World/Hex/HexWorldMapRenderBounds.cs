using System;

namespace XianXia.Core.World.Hex
{
    /// <summary>Hex WorldMap 可见格范围（紧凑存储 q,r 迭代）。</summary>
    public static class HexWorldMapRenderBounds
    {
        public static void ComputeVisibleCompactRange(
            HexWorld grid,
            float minWx,
            float maxWx,
            float minWy,
            float maxWy,
            float pad,
            out int qMin,
            out int qMax,
            out int rMin,
            out int rMax)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            qMin = Math.Max(0, Math.Min(
                WorldToRoughQ(minWx - pad, minWy - pad, grid),
                WorldToRoughQ(minWx - pad, maxWy + pad, grid)) - 1);
            qMax = Math.Min(grid.Width - 1, Math.Max(
                WorldToRoughQ(maxWx + pad, minWy - pad, grid),
                WorldToRoughQ(maxWx + pad, maxWy + pad, grid)) + 1);
            rMin = Math.Max(0, WorldToRoughR(minWy - pad, grid) - 1);
            rMax = Math.Min(grid.Height - 1, WorldToRoughR(maxWy + pad, grid) + 1);
        }

        public static int CountVisibleCells(
            HexWorld grid,
            float minWx,
            float maxWx,
            float minWy,
            float maxWy,
            float pad)
        {
            if (grid == null || !grid.HasGrid)
                return 0;

            ComputeVisibleCompactRange(grid, minWx, maxWx, minWy, maxWy, pad, out var qMin, out var qMax, out var rMin, out var rMax);
            if (qMin > qMax || rMin > rMax)
                return 0;

            var count = 0;
            for (var r = rMin; r <= rMax; r++)
            {
                for (var q = qMin; q <= qMax; q++)
                {
                    if (grid.TryGetCell(new HexCoord(q, r), out var cell) && cell != null)
                    {
                        HexMath.ToWorldPosition(cell.Coord, grid.HexSize, out var cx, out var cy);
                        if (cx >= minWx - pad && cx <= maxWx + pad && cy >= minWy - pad && cy <= maxWy + pad)
                            count++;
                    }
                }
            }

            return count;
        }

        static int WorldToRoughQ(float wx, float wy, HexWorld grid)
        {
            var row = WorldToRoughR(wy, grid);
            return (int)Math.Round(wx / (grid.HexSize * Math.Sqrt(3)) - 0.5 * (row & 1));
        }

        static int WorldToRoughR(float wy, HexWorld grid) =>
            (int)Math.Round(wy / (grid.HexSize * 1.5));
    }
}
