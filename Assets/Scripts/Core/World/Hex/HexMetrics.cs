using System;

namespace XianXia.Core.World.Hex
{
    /// <summary>
    /// Hex 几何真源：Renderer 与 Mouse Picking 必须只读此配置。
    /// Pointy-top；Compact 网格中 Q=列、R=行（Odd-R offset 矩形布局，见 HexWorldLayout）。
    /// HexWorldEditor 镜像：ExternalTools Shared HexWorldLayoutShared（必须保持公式一致）。
    /// </summary>
    public static class HexMetrics
    {
        public const HexOrientation Orientation = HexOrientation.PointyTop;

        public static float OuterRadius(float hexSize) => hexSize;

        public static float InnerRadius(float hexSize) => hexSize * 0.8660254f;

        public static float HorizontalPitch(float hexSize) => (float)Math.Sqrt(3) * hexSize;

        public static float VerticalPitch(float hexSize) => 1.5f * hexSize;

        public static void HexCoordToWorldCenter(HexCoord coord, float hexSize, out float worldX, out float worldY) =>
            HexMath.ToWorldPosition(coord, hexSize, out worldX, out worldY);

        public static HexCoord WorldToHexCoord(float worldX, float worldY, float hexSize) =>
            HexMath.WorldToHex(worldX, worldY, hexSize);

        public static bool ValidateCenterRoundTrip(HexCoord coord, float hexSize, out HexCoord roundTripped)
        {
            roundTripped = default;
            if (hexSize <= 0.0001f)
                return false;
            HexCoordToWorldCenter(coord, hexSize, out var wx, out var wy);
            roundTripped = WorldToHexCoord(wx, wy, hexSize);
            return roundTripped.Equals(coord);
        }
    }

    public enum HexOrientation
    {
        PointyTop,
    }
}
