using System;
using System.Collections.Generic;

namespace XianXia.Core.World.Surface
{
    /// <summary>
    /// Complete authored Surface identity, metric and chunk coverage. This is not terrain or
    /// navigation data: it intentionally carries no walkability, river, bridge or blocker cells.
    /// </summary>
    public sealed class OutdoorSurfaceSpatialMetric
    {
        readonly HashSet<SurfaceChunkCoord> _authoredChunks = new HashSet<SurfaceChunkCoord>();

        public OutdoorSurfaceSpatialMetric(
            string surfaceId,
            float originWorldX,
            float originWorldY,
            float cellSize,
            float chunkWidth,
            float chunkHeight,
            IEnumerable<SurfaceChunkCoord> authoredChunks)
        {
            if (string.IsNullOrWhiteSpace(surfaceId))
                throw new ArgumentException("SurfaceId required.", nameof(surfaceId));
            if (!IsFinite(cellSize) || cellSize <= 0f ||
                !IsFinite(chunkWidth) || chunkWidth <= 0f ||
                !IsFinite(chunkHeight) || chunkHeight <= 0f ||
                !IsFinite(originWorldX) || !IsFinite(originWorldY))
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Surface spatial metric must be finite and positive.");
            SurfaceId = surfaceId;
            OriginWorldX = originWorldX;
            OriginWorldY = originWorldY;
            CellSize = cellSize;
            ChunkWidth = chunkWidth;
            ChunkHeight = chunkHeight;
            if (authoredChunks != null)
                foreach (var chunk in authoredChunks) _authoredChunks.Add(chunk);
            if (_authoredChunks.Count == 0)
                throw new ArgumentException("Surface authored chunk coverage required.", nameof(authoredChunks));
        }

        public string SurfaceId { get; }
        public float OriginWorldX { get; }
        public float OriginWorldY { get; }
        public float CellSize { get; }
        public float ChunkWidth { get; }
        public float ChunkHeight { get; }
        public IReadOnlyCollection<SurfaceChunkCoord> AuthoredChunks => _authoredChunks;

        public bool ContainsWorldPosition(float worldX, float worldY)
        {
            if (!IsFinite(worldX) || !IsFinite(worldY)) return false;
            var chunk = new SurfaceChunkCoord(
                (int)Math.Floor((worldX - OriginWorldX) / ChunkWidth),
                (int)Math.Floor((worldY - OriginWorldY) / ChunkHeight));
            return _authoredChunks.Contains(chunk);
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>Session registry for complete, non-acceptance Outdoor Surface spatial metadata.</summary>
    public sealed class OutdoorSurfaceSpatialAuthority
    {
        readonly Dictionary<string, OutdoorSurfaceSpatialMetric> _registered =
            new Dictionary<string, OutdoorSurfaceSpatialMetric>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, OutdoorSurfaceSpatialMetric> Registered => _registered;

        public void Clear() => _registered.Clear();

        public void Register(OutdoorSurfaceSpatialMetric metric)
        {
            if (metric == null) throw new ArgumentNullException(nameof(metric));
            _registered[metric.SurfaceId] = metric;
        }

        public bool TryGet(string surfaceId, out OutdoorSurfaceSpatialMetric metric) =>
            _registered.TryGetValue(surfaceId ?? string.Empty, out metric);

        public bool TryResolveContaining(
            float worldX, float worldY, out OutdoorSurfaceSpatialMetric metric)
        {
            metric = null;
            foreach (var pair in _registered)
            {
                var candidate = pair.Value;
                if (candidate == null || !candidate.ContainsWorldPosition(worldX, worldY)) continue;
                if (metric == null || string.CompareOrdinal(candidate.SurfaceId, metric.SurfaceId) < 0)
                    metric = candidate;
            }
            return metric != null;
        }
    }
}
