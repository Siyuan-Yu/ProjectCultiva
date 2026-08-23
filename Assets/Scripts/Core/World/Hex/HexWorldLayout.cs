using System;

namespace XianXia.Core.World.Hex
{
    /// <summary>
    /// 矩形 Hex 世界布局（Odd-R offset）：HexCoord.Q = 列，HexCoord.R = 行。
    /// 仅影响世界平面坐标与视口 fit；Domain 位置真源仍为 <see cref="HexCoord"/>。
    /// </summary>
    public static class HexWorldLayout
    {
        public const float DefaultViewportPadding = 0.04f;
        public const float DefaultCameraMargin = 0.03f;

        public static void CoordToWorldCenter(HexCoord coord, float hexSize, out float worldX, out float worldY)
        {
            var col = coord.Q;
            var row = coord.R;
            worldX = hexSize * (float)Math.Sqrt(3) * (col + 0.5f * (row & 1));
            worldY = hexSize * 1.5f * row;
        }

        public static HexCoord WorldToCoord(float worldX, float worldY, float hexSize)
        {
            if (hexSize <= 0.0001f)
                return default;

            var row = (int)Math.Round(worldY / (1.5 * hexSize));
            var col = (int)Math.Round(worldX / ((float)Math.Sqrt(3) * hexSize) - 0.5 * (row & 1));
            return new HexCoord(col, row);
        }

        public static void ComputeWorldBounds(HexWorld grid, out float minX, out float maxX, out float minY, out float maxY)
        {
            minX = float.MaxValue;
            maxX = float.MinValue;
            minY = float.MaxValue;
            maxY = float.MinValue;
            if (grid == null || !grid.HasGrid)
            {
                minX = maxX = minY = maxY = 0f;
                return;
            }

            if (grid.UsesCompactStorage)
            {
                SampleCellBounds(grid, new HexCoord(0, 0), ref minX, ref maxX, ref minY, ref maxY);
                SampleCellBounds(grid, new HexCoord(grid.Width - 1, 0), ref minX, ref maxX, ref minY, ref maxY);
                SampleCellBounds(grid, new HexCoord(0, grid.Height - 1), ref minX, ref maxX, ref minY, ref maxY);
                SampleCellBounds(grid, new HexCoord(grid.Width - 1, grid.Height - 1), ref minX, ref maxX, ref minY, ref maxY);
            }
            else
            {
                foreach (var kv in grid.Tiles)
                {
                    if (kv.Value == null)
                        continue;
                    SampleCellBounds(grid, kv.Key, ref minX, ref maxX, ref minY, ref maxY);
                }
            }

            if (maxX < minX)
                minX = maxX = minY = maxY = 0f;
        }

        public static void ComputeWorldCenter(HexWorld grid, out float centerX, out float centerY)
        {
            ComputeWorldBounds(grid, out var minX, out var maxX, out var minY, out var maxY);
            centerX = (minX + maxX) * 0.5f;
            centerY = (minY + maxY) * 0.5f;
        }

        /// <summary>使矩形世界 bounds 适配 map viewport（保持 Hex 比例，不拉伸）。</summary>
        public static float ComputeFitViewHalf(
            float viewportWidth,
            float viewportHeight,
            HexWorld grid,
            float paddingFraction = DefaultViewportPadding)
        {
            if (grid == null || !grid.HasGrid || viewportWidth <= 1f || viewportHeight <= 1f)
                return HexWorldScale.ViewHalfForHexesAcross(HexWorldScale.DefaultHexesAcross, grid?.HexSize ?? 1f);

            ComputeWorldBounds(grid, out var minX, out var maxX, out var minY, out var maxY);
            var worldW = Math.Max(0.01f, maxX - minX);
            var worldH = Math.Max(0.01f, maxY - minY);
            var pad = 1f + Math.Max(0f, paddingFraction);
            var minDim = Math.Min(viewportWidth, viewportHeight);
            var halfFromWidth = worldW * pad * minDim / (2f * viewportWidth);
            var halfFromHeight = worldH * pad * minDim / (2f * viewportHeight);
            return Math.Max(halfFromWidth, halfFromHeight);
        }

        public static void ClampViewCenter(
            HexWorld grid,
            float viewportWidth,
            float viewportHeight,
            float viewHalf,
            float marginFraction,
            ref float viewCenterX,
            ref float viewCenterY)
        {
            if (grid == null || !grid.HasGrid || viewHalf <= 0.0001f)
                return;

            ComputeWorldBounds(grid, out var minX, out var maxX, out var minY, out var maxY);
            var scale = Math.Min(viewportWidth, viewportHeight) / (2.0 * viewHalf);
            if (scale <= 0.0001f)
                return;

            var halfVisibleX = viewportWidth / (2.0 * scale);
            var halfVisibleY = viewportHeight / (2.0 * scale);
            var marginX = (maxX - minX) * marginFraction;
            var marginY = (maxY - minY) * marginFraction;

            var minCx = minX + halfVisibleX - marginX;
            var maxCx = maxX - halfVisibleX + marginX;
            var minCy = minY + halfVisibleY - marginY;
            var maxCy = maxY - halfVisibleY + marginY;

            if (minCx > maxCx)
                viewCenterX = (minX + maxX) * 0.5f;
            else
                viewCenterX = (float)Math.Max(minCx, Math.Min(maxCx, viewCenterX));

            if (minCy > maxCy)
                viewCenterY = (minY + maxY) * 0.5f;
            else
                viewCenterY = (float)Math.Max(minCy, Math.Min(maxCy, viewCenterY));
        }

        static void SampleCellBounds(
            HexWorld grid,
            HexCoord coord,
            ref float minX,
            ref float maxX,
            ref float minY,
            ref float maxY)
        {
            CoordToWorldCenter(coord, grid.HexSize, out var cx, out var cy);
            for (var i = 0; i < 6; i++)
            {
                var angle = (Math.PI / 3.0) * i + Math.PI / 6.0;
                var x = cx + grid.HexSize * (float)Math.Cos(angle);
                var y = cy + grid.HexSize * (float)Math.Sin(angle);
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }
        }
    }
}
