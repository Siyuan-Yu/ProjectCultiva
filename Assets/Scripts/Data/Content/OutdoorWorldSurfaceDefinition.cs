using System.Collections.Generic;
using XianXia.Core.World.Surface;

namespace XianXia.Data.Content
{
    /// <summary>W1C runtime/authoring surface definition; it has no WorldSpaceId or save identity.</summary>
    public sealed class OutdoorWorldSurfaceDefinition
    {
        public string SurfaceId { get; set; } = "base:main_continent_surface";
        public float OriginWorldX { get; set; }
        public float OriginWorldY { get; set; }
        /// <summary>PROVISIONAL/TUNABLE W1C prototype metric; every chunk on this surface shares it.</summary>
        public float CellSize { get; set; } = 1f;
        public float ChunkWidth { get; set; } = 50f;
        public float ChunkHeight { get; set; } = 50f;
        public List<OutdoorSurfaceChunkDefinition> Chunks { get; set; } = new List<OutdoorSurfaceChunkDefinition>();
    }
    public sealed class OutdoorSurfaceChunkDefinition
    {
        public string StableChunkId { get; set; }
        public SurfaceChunkCoord Coord { get; set; }
        /// <summary>Legacy authored-source bridge only; it does not mean Chunk equals LocalMap or Hex.</summary>
        public string SourceMapLayoutId { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
    }
}
