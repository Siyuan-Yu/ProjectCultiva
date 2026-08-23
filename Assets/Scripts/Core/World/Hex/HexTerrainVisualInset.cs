using System;

namespace XianXia.Core.World.Hex
{
    /// <summary>
    /// 地形视觉内缩（仅渲染）：逻辑 Hex 尺寸不变，地形多边形向中心缩小以露出背景 gutter。
    /// </summary>
    public static class HexTerrainVisualInset
    {
        public const float DebugStrongInsetScale = 0.80f;
        public const float ProductionInsetScale = 0.96f;

        /// <summary>浅米黄 Cell Fill（Plain）。</summary>
        public static readonly HexRgb PlainCellFill = new HexRgb(0.93f, 0.89f, 0.78f);

        /// <summary>正式 Gutter：比 Cell Fill 略深的暖灰褐。</summary>
        public static readonly HexRgb ProductionGutter = new HexRgb(0.78f, 0.72f, 0.60f);

        /// <summary>Debug Strong Separation：高对比 gutter（开发期）。</summary>
        public static readonly HexRgb DebugStrongGutter = new HexRgb(0.45f, 0.10f, 0.45f);

        public static float ResolveInsetScale(bool debugStrongSeparation) =>
            debugStrongSeparation ? DebugStrongInsetScale : ProductionInsetScale;

        public static HexRgb ResolveGutterColor(bool debugStrongSeparation) =>
            debugStrongSeparation ? DebugStrongGutter : ProductionGutter;

        /// <summary>世界空间：逻辑角点向 Hex 中心内缩（仅视觉）。</summary>
        public static void CollectInsetCornerWorldPositions(
            HexCoord coord,
            float hexSize,
            float insetScale,
            float[] cornerWorldX,
            float[] cornerWorldY)
        {
            if (cornerWorldX == null || cornerWorldY == null || cornerWorldX.Length < 6 || cornerWorldY.Length < 6)
                throw new ArgumentException("corner arrays must have length >= 6");

            HexMath.CollectCornerWorldPositions(coord, hexSize, cornerWorldX, cornerWorldY);
            if (insetScale >= 0.9999f)
                return;

            HexMath.ToWorldPosition(coord, hexSize, out var cx, out var cy);
            for (var i = 0; i < 6; i++)
            {
                cornerWorldX[i] = cx + (cornerWorldX[i] - cx) * insetScale;
                cornerWorldY[i] = cy + (cornerWorldY[i] - cy) * insetScale;
            }
        }
    }

    public readonly struct HexRgb
    {
        public readonly float R;
        public readonly float G;
        public readonly float B;

        public HexRgb(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }
    }
}
