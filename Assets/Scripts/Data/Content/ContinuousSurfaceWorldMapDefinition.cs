using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    /// <summary>Presentation-only raster published by WorldComposer; navigation remains outdoorSurfaceGeography.</summary>
    public sealed class ContinuousSurfaceWorldMapDefinition
    {
        public DefinitionId Id;
        public string CompositionId = string.Empty;
        public string SurfaceId = string.Empty;
        public string SourceHash = string.Empty;
        public float OriginWorldX, OriginWorldY, CellSize;
        public int WidthCells, HeightCells;
        public readonly List<string> BaseTerrainRows = new List<string>(); // P plain, M mountain, W water
        public readonly List<string> ForestRows = new List<string>(); // 0..9 density
    }
}
