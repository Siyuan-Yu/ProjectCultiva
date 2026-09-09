using System;
using System.Collections.Generic;

namespace XianXia.Core.World.Surface
{
    /// <summary>Pure deterministic desired-set and incremental diff policy for W1C streaming.</summary>
    public static class SurfaceChunkNeighborhood
    {
        public static void CollectSquare(SurfaceChunkCoord center, int radius, ISet<SurfaceChunkCoord> result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
            result.Clear();
            for (var y = center.Y - radius; y <= center.Y + radius; y++)
            for (var x = center.X - radius; x <= center.X + radius; x++) result.Add(new SurfaceChunkCoord(x, y));
        }
        public static void Diff(ISet<SurfaceChunkCoord> previous, ISet<SurfaceChunkCoord> desired, ISet<SurfaceChunkCoord> toAdd, ISet<SurfaceChunkCoord> toRemove)
        {
            if (previous == null || desired == null || toAdd == null || toRemove == null) throw new ArgumentNullException();
            toAdd.Clear(); toRemove.Clear();
            foreach (var item in desired) if (!previous.Contains(item)) toAdd.Add(item);
            foreach (var item in previous) if (!desired.Contains(item)) toRemove.Add(item);
        }
        public static string OwnerKey(string surfaceId, SurfaceChunkCoord coord) => "surface:" + surfaceId + ":chunk:" + coord.X + ":" + coord.Y;

        public static bool PhysicalRectsOverlap(SurfaceChunkCoord a, SurfaceChunkCoord b, float chunkWidth, float chunkHeight)
        {
            if (chunkWidth <= 0f || chunkHeight <= 0f) throw new ArgumentOutOfRangeException(nameof(chunkWidth));
            var ax0 = a.X * chunkWidth; var ay0 = a.Y * chunkHeight;
            var bx0 = b.X * chunkWidth; var by0 = b.Y * chunkHeight;
            return ax0 < bx0 + chunkWidth && ax0 + chunkWidth > bx0 &&
                   ay0 < by0 + chunkHeight && ay0 + chunkHeight > by0;
        }
    }
}
