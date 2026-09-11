using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.World.Surface;

namespace XianXia.Data.Content
{
    /// <summary>One checked-in, region-limited geography bake consumed by chunk presentation,
    /// whole-region routing, and the WorldMap layer.</summary>
    public sealed class OutdoorSurfaceGeographyDefinition
    {
        public DefinitionId Id { get; set; }
        public string SurfaceId { get; set; }
        public int SourceSchemaVersion { get; set; }
        public string SourceRevision { get; set; }
        public string SourceHash { get; set; }
        public List<SurfaceChunkCoord> CoverageChunks { get; } = new List<SurfaceChunkCoord>();
        public SurfaceGroundNavigation Navigation { get; set; }
        public List<OutdoorGeographyPrimitive> MapPrimitives { get; } = new List<OutdoorGeographyPrimitive>();
        public List<OutdoorGeographyLandmark> Landmarks { get; } = new List<OutdoorGeographyLandmark>();
        public List<OutdoorGeographyHexSummary> HexSummary { get; } = new List<OutdoorGeographyHexSummary>();
    }

    public sealed class OutdoorGeographyPrimitive
    {
        public string StableId { get; set; }
        public string Kind { get; set; }
        public float WorldX { get; set; }
        public float WorldY { get; set; }
        public float WorldWidth { get; set; }
        public float WorldHeight { get; set; }
        public float StrokeWidth { get; set; }
        /// <summary>World-space x,y pairs for road/river centerline display.</summary>
        public List<float> Points { get; } = new List<float>();
    }

    public sealed class OutdoorGeographyLandmark
    {
        public string StableId { get; set; }
        public string Label { get; set; }
        public float WorldX { get; set; }
        public float WorldY { get; set; }
    }

    public sealed class OutdoorGeographyHexSummary
    {
        public int Q { get; set; }
        public int R { get; set; }
        public float CoveredFraction { get; set; }
        public float WaterFraction { get; set; }
        public float RoadFraction { get; set; }
        public bool HasRiver { get; set; }
        public bool HasBridge { get; set; }
    }
}
