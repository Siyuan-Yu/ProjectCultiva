using UnityEngine;
using XianXia.Core.World.Hex;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Hex WorldMap 鼠标拾取唯一入口：Hover / Click / Path / Inspect 全部经此类型。
    /// </summary>
    public static class HexMapMousePick
    {
        public static bool TryResolveMouseHex(
            HexMapViewportProjection projection,
            HexWorld grid,
            Vector2 screenMouse,
            out HexCoord coord)
        {
            coord = default;
            if (grid == null || !grid.HasGrid || projection.Viewport.width <= 1f)
                return false;

            if (!projection.Viewport.Contains(screenMouse))
                return false;

            var world = projection.ScreenToWorld(screenMouse);
            coord = HexMetrics.WorldToHexCoord(world.x, world.y, projection.HexSize);
            return grid.Contains(coord);
        }

        public static bool TryResolveMouseHex(
            HexMapViewportProjection projection,
            HexWorld grid,
            Vector2 screenMouse,
            out HexCoord coord,
            out Vector2 worldPosition,
            out Vector2 hexCenterScreen)
        {
            worldPosition = default;
            hexCenterScreen = default;
            if (!TryResolveMouseHex(projection, grid, screenMouse, out coord))
                return false;

            worldPosition = projection.ScreenToWorld(screenMouse);
            hexCenterScreen = projection.ProjectHexCenter(coord);
            return true;
        }
    }
}
