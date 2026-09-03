using System;
using System.Collections.Generic;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Navigation
{
    /// <summary>Local surface exit 的 WalkGrid 可达性真源；不改变战略 connection 几何。</summary>
    public static class SurfaceExitWalkGridReachability
    {
        const float Epsilon = 0.001f;
        static WalkGrid _cachedGrid;
        static int _cachedRevision = -1;
        static int[] _components;
        static readonly Queue<int> QueueScratch = new Queue<int>(256);

        public static bool TryResolveReachablePointInsideExitSlot(
            WalkGrid grid,
            float activeX,
            float activeY,
            SurfaceExitConnection connection,
            out float reachableX,
            out float reachableY)
        {
            reachableX = 0f;
            reachableY = 0f;
            if (grid == null)
                return false;

            if (!EnsureConnectivity(grid) || !grid.TryWorldToCell(activeX, activeY, out var sx, out var sy)) return false;
            var activeComponent = _components[sy * grid.Width + sx];
            if (activeComponent < 0) return false;
            var slot = connection.SlotRect;
            var minCx = Math.Max(0, (int)Math.Floor((slot.MinX - grid.OriginX) / grid.CellSize));
            var maxCx = Math.Min(grid.Width - 1, (int)Math.Floor((slot.MaxX - Epsilon - grid.OriginX) / grid.CellSize));
            var minCy = Math.Max(0, (int)Math.Floor((slot.MinY - grid.OriginY) / grid.CellSize));
            var maxCy = Math.Min(grid.Height - 1, (int)Math.Floor((slot.MaxY - Epsilon - grid.OriginY) / grid.CellSize));
            if (minCx > maxCx || minCy > maxCy)
                return false;

            var bestLength = int.MaxValue;
            var found = false;
            for (var cy = minCy; cy <= maxCy; cy++)
            for (var cx = minCx; cx <= maxCx; cx++)
            {
                if (!grid.IsWalkable(cx, cy))
                    continue;

                var cellMinX = grid.OriginX + cx * grid.CellSize;
                var cellMinY = grid.OriginY + cy * grid.CellSize;
                var cellMaxX = cellMinX + grid.CellSize;
                var cellMaxY = cellMinY + grid.CellSize;
                var minX = Math.Max(cellMinX, slot.MinX) + Epsilon;
                var maxX = Math.Min(cellMaxX, slot.MaxX) - Epsilon;
                var minY = Math.Max(cellMinY, slot.MinY) + Epsilon;
                var maxY = Math.Min(cellMaxY, slot.MaxY) - Epsilon;
                if (minX > maxX || minY > maxY)
                    continue;
                var length = _components[cy * grid.Width + cx];
                if (length != activeComponent) continue;
                var candidateX = (minX + maxX) * 0.5f;
                var candidateY = (minY + maxY) * 0.5f;
                if (!found || length < bestLength - Epsilon ||
                    (Math.Abs(length - bestLength) <= Epsilon &&
                     (cy < (int)Math.Floor((reachableY - grid.OriginY) / grid.CellSize) ||
                      (cy == (int)Math.Floor((reachableY - grid.OriginY) / grid.CellSize) && cx < (int)Math.Floor((reachableX - grid.OriginX) / grid.CellSize)))))
                {
                    bestLength = length;
                    reachableX = candidateX;
                    reachableY = candidateY;
                    found = true;
                }
            }
            return found;
        }

        static bool EnsureConnectivity(WalkGrid grid)
        {
            if (_cachedGrid == grid && _cachedRevision == grid.Revision) return true;
            var count = grid.Width * grid.Height;
            if (_components == null || _components.Length != count) _components = new int[count];
            for (var i = 0; i < count; i++) _components[i] = -1;
            var component = 0;
            for (var start = 0; start < count; start++)
            {
                var sx = start % grid.Width; var sy = start / grid.Width;
                if (!grid.IsWalkable(sx, sy) || _components[start] >= 0) continue;
                QueueScratch.Clear(); _components[start] = component; QueueScratch.Enqueue(start);
                while (QueueScratch.Count > 0)
                {
                var cur = QueueScratch.Dequeue(); var cx = cur % grid.Width; var cy = cur / grid.Width;
                for (var dy = -1; dy <= 1; dy++) for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue; var nx = cx + dx; var ny = cy + dy; var ni = ny * grid.Width + nx;
                    if (!grid.IsWalkable(nx, ny) || _components[ni] >= 0) continue;
                    _components[ni] = component; QueueScratch.Enqueue(ni);
                }
                }
                component++;
            }
            _cachedGrid = grid; _cachedRevision = grid.Revision; return true;
        }
    }
}
