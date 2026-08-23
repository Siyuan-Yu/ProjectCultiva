using UnityEngine;
using XianXia.Core.World.Hex;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Hex WorldMap 视口投影真源：屏幕 GUI ↔ 逻辑世界 ↔ HexCoord。
    /// Hover / Select / Path / Inspect / 渲染必须只经此类型。
    /// </summary>
    public readonly struct HexMapViewportProjection
    {
        public readonly Rect Viewport;
        public readonly float ViewCenterX;
        public readonly float ViewCenterY;
        public readonly float ViewHalf;
        public readonly float HexSize;

        public HexMapViewportProjection(
            Rect viewport,
            float viewCenterX,
            float viewCenterY,
            float viewHalf,
            float hexSize)
        {
            Viewport = viewport;
            ViewCenterX = viewCenterX;
            ViewCenterY = viewCenterY;
            ViewHalf = viewHalf;
            HexSize = hexSize;
        }

        public float Scale =>
            Mathf.Min(Viewport.width, Viewport.height) / (2f * Mathf.Max(0.01f, ViewHalf));

        public Vector2 ScreenCenter =>
            new Vector2(
                Viewport.x + Viewport.width * 0.5f,
                Viewport.y + Viewport.height * 0.5f);

        public void HexCoordToWorldCenter(HexCoord coord, out float worldX, out float worldY) =>
            HexMetrics.HexCoordToWorldCenter(coord, HexSize, out worldX, out worldY);

        public Vector2 ProjectHexCenter(HexCoord coord)
        {
            HexCoordToWorldCenter(coord, out var wx, out var wy);
            return ProjectWorld(wx, wy);
        }

        public Vector2 ProjectWorld(float worldX, float worldY)
        {
            var scale = Scale;
            var center = ScreenCenter;
            return new Vector2(
                center.x + (worldX - ViewCenterX) * scale,
                center.y - (worldY - ViewCenterY) * scale);
        }

        public Vector2 ScreenToWorld(Vector2 screenGui)
        {
            var scale = Scale;
            var center = ScreenCenter;
            return new Vector2(
                ViewCenterX + (screenGui.x - center.x) / scale,
                ViewCenterY - (screenGui.y - center.y) / scale);
        }

        public HexCoord WorldToHexCoord(float worldX, float worldY) =>
            HexMetrics.WorldToHexCoord(worldX, worldY, HexSize);

        public HexCoord WorldToHexCoord(Vector2 world) =>
            WorldToHexCoord(world.x, world.y);

        public bool TryPickHex(HexWorld grid, Vector2 screenGui, out HexCoord coord) =>
            HexMapMousePick.TryResolveMouseHex(this, grid, screenGui, out coord);

        /// <summary>H → center(H) → H 自检。</summary>
        public bool ValidateHexRoundTrip(HexCoord hex, out HexCoord roundTripped) =>
            HexMetrics.ValidateCenterRoundTrip(hex, HexSize, out roundTripped);

        /// <summary>H → screen center → world → H 自检（验证投影与 Hex 数学一致）。</summary>
        public bool ValidateProjectionRoundTrip(HexCoord hex, out HexCoord roundTripped)
        {
            roundTripped = default;
            if (HexSize <= 0.0001f)
                return false;
            var screen = ProjectHexCenter(hex);
            var world = ScreenToWorld(screen);
            roundTripped = WorldToHexCoord(world.x, world.y);
            return roundTripped.Equals(hex);
        }
    }
}
