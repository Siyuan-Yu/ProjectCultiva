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
        /// <summary>Acceptance/diagnostic surfaces never participate in normal runtime authority.</summary>
        public bool AcceptanceOnly { get; set; }
        public List<OutdoorSurfaceChunkDefinition> Chunks { get; set; } = new List<OutdoorSurfaceChunkDefinition>();
        public List<WorldSitePhysicalRegionDefinition> SiteRegions { get; set; } = new List<WorldSitePhysicalRegionDefinition>();
        public List<OutdoorSurfacePlacementDefinition> SitePlacements { get; set; } = new List<OutdoorSurfacePlacementDefinition>();
        public List<WorldSitePlaceDefinition> SitePlaces { get; set; } = new List<WorldSitePlaceDefinition>();
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

    public sealed class WorldSitePhysicalRegionDefinition
    {
        public string SiteId { get; set; }
        public string SurfaceId { get; set; }
        public string SourceLocalMapId { get; set; }
        public float ArrivalWorldX { get; set; }
        public float ArrivalWorldY { get; set; }
    }

    public sealed class OutdoorSurfacePlacementDefinition
    {
        public string StableId { get; set; }
        public string SiteId { get; set; }
        public int ChunkX { get; set; }
        public int ChunkY { get; set; }
        public float WorldX { get; set; }
        public float WorldY { get; set; }
        public float WorldWidth { get; set; }
        public float WorldHeight { get; set; }
        /// <summary>Authored semantic subdivision. Physical size must never be used as a cell count.</summary>
        public int SourceGridX { get; set; }
        public int SourceGridY { get; set; }
        public int SourceCellsW { get; set; } = 1;
        public int SourceCellsH { get; set; } = 1;
        public string Kind { get; set; }
        public bool BlocksMovement { get; set; }
        public string BoundLocationId { get; set; }
        public string Label { get; set; }
        public string LootItemId { get; set; }
        public string SpawnTableId { get; set; }
        public int SpawnCount { get; set; }
    }

    public sealed class WorldSitePlaceDefinition
    {
        public string SiteId { get; set; }
        public string LocationId { get; set; }
        public string Name { get; set; }
        public float WorldX { get; set; }
        public float WorldY { get; set; }
        public string LocalMapId { get; set; }
        public string EnterLocalMapId { get; set; }
        public string EnterSpawnLocationId { get; set; }
    }
}
