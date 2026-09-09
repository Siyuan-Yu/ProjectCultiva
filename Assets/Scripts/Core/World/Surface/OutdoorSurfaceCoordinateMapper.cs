using System;

namespace XianXia.Core.World.Surface
{
    /// <summary>
    /// W1C uniform mapper. WorldPosition remains the physical authority; this service only
    /// converts between that space, presentation space and the independent rectangular chunks.
    /// All metric values are provisional prototype parameters owned by the surface definition.
    /// </summary>
    public sealed class OutdoorSurfaceCoordinateMapper
    {
        public OutdoorSurfaceCoordinateMapper(
            float chunkWidth,
            float chunkHeight,
            float cellSize,
            float presentationOffsetX = 0f,
            float presentationOffsetY = 0f,
            float presentationUnitsPerWorldUnit = 1f,
            float originWorldX = 0f,
            float originWorldY = 0f)
        {
            if (chunkWidth <= 0f || chunkHeight <= 0f || cellSize <= 0f || presentationUnitsPerWorldUnit <= 0f)
                throw new ArgumentOutOfRangeException(nameof(chunkWidth), "Surface metric must be positive.");
            ChunkWidth = chunkWidth; ChunkHeight = chunkHeight; CellSize = cellSize;
            PresentationOffsetX = presentationOffsetX; PresentationOffsetY = presentationOffsetY;
            PresentationUnitsPerWorldUnit = presentationUnitsPerWorldUnit;
            OriginWorldX = originWorldX; OriginWorldY = originWorldY;
        }
        public float ChunkWidth { get; }
        public float ChunkHeight { get; }
        public float CellSize { get; }
        public float PresentationOffsetX { get; }
        public float PresentationOffsetY { get; }
        /// <summary>Uniform bridge from WorldPosition units to Unity presentation units.</summary>
        public float PresentationUnitsPerWorldUnit { get; }
        public float OriginWorldX { get; }
        public float OriginWorldY { get; }
        public SurfaceChunkCoord WorldToChunk(float worldX, float worldY) => new SurfaceChunkCoord(Floor((worldX - OriginWorldX) / ChunkWidth), Floor((worldY - OriginWorldY) / ChunkHeight));
        public void WorldToChunkLocal(float worldX, float worldY, out SurfaceChunkCoord chunk, out float localX, out float localY)
        {
            chunk = WorldToChunk(worldX, worldY); localX = worldX - (OriginWorldX + chunk.X * ChunkWidth); localY = worldY - (OriginWorldY + chunk.Y * ChunkHeight);
        }
        public void ChunkLocalToWorld(SurfaceChunkCoord chunk, float localX, float localY, out float worldX, out float worldY)
        { worldX = OriginWorldX + chunk.X * ChunkWidth + localX; worldY = OriginWorldY + chunk.Y * ChunkHeight + localY; }
        public void WorldToPresentation(float worldX, float worldY, out float presentationX, out float presentationY)
        {
            presentationX = worldX * PresentationUnitsPerWorldUnit + PresentationOffsetX;
            presentationY = worldY * PresentationUnitsPerWorldUnit + PresentationOffsetY;
        }
        public void PresentationToWorld(float presentationX, float presentationY, out float worldX, out float worldY)
        {
            worldX = (presentationX - PresentationOffsetX) / PresentationUnitsPerWorldUnit;
            worldY = (presentationY - PresentationOffsetY) / PresentationUnitsPerWorldUnit;
        }
        public SurfaceChunkCoord PresentationToChunk(float presentationX, float presentationY)
        { PresentationToWorld(presentationX, presentationY, out var x, out var y); return WorldToChunk(x, y); }
        static int Floor(float value) => (int)Math.Floor(value);
    }
}
