using UnityEngine;
using XianXia.Core.World.Surface;

namespace XianXia.Unity.Host
{
    /// <summary>Cartographic coordinate guides only; no navigation or picking authority.</summary>
    public static class HostSurfaceWorldMapGridRenderer
    {
        public static void Draw(Rect mapRect, SurfaceWorldMapViewportProjection projection,
            SurfaceGroundNavigation nav, Texture2D pixel)
        {
            if (Event.current.type != EventType.Repaint || nav == null || pixel == null) return;
            var worldA = projection.ScreenToWorld(new Vector2(mapRect.xMin, mapRect.yMax));
            var worldB = projection.ScreenToWorld(new Vector2(mapRect.xMax, mapRect.yMin));
            var visibleCells = (worldB.x - worldA.x) / nav.CellSize;
            var desiredCells = visibleCells * 110f / mapRect.width;
            var magnitude = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(Mathf.Max(1f, desiredCells))));
            var scaled = desiredCells / magnitude;
            var interval = (scaled <= 1.5f ? 1f : scaled <= 3.5f ? 2f : scaled <= 7.5f ? 5f : 10f) * magnitude;
            var minX = Mathf.Max(0, Mathf.CeilToInt((worldA.x - nav.OriginX) / (interval * nav.CellSize)));
            var maxX = Mathf.Min(Mathf.FloorToInt(nav.Width / interval),
                Mathf.FloorToInt((worldB.x - nav.OriginX) / (interval * nav.CellSize)));
            var minY = Mathf.Max(0, Mathf.CeilToInt((worldA.y - nav.OriginY) / (interval * nav.CellSize)));
            var maxY = Mathf.Min(Mathf.FloorToInt(nav.Height / interval),
                Mathf.FloorToInt((worldB.y - nav.OriginY) / (interval * nav.CellSize)));
            var old = GUI.color;
            GUI.color = new Color(.9f, .9f, .85f, .16f);
            for (var i = minX; i <= maxX; i++)
            {
                var x = projection.ProjectWorld(nav.OriginX + i * interval * nav.CellSize, nav.OriginY).x;
                GUI.DrawTexture(new Rect(x, mapRect.yMin, 1f, mapRect.height), pixel);
            }
            for (var i = minY; i <= maxY; i++)
            {
                var y = projection.ProjectWorld(nav.OriginX, nav.OriginY + i * interval * nav.CellSize).y;
                GUI.DrawTexture(new Rect(mapRect.xMin, y, mapRect.width, 1f), pixel);
            }
            GUI.color = old;
        }
    }
}
