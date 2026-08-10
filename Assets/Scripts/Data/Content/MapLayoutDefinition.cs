using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    /// <summary>
    /// Grid map layout authored by MapEditor. Cell (0,0) covers
    /// [OriginX, OriginX+CellSize) × [OriginY, OriginY+CellSize).
    /// </summary>
    public sealed class MapLayoutDefinition
    {
        public DefinitionId Id { get; set; }
        public string Name { get; set; }
        public string WorldRegionId { get; set; }
        public float OriginX { get; set; }
        public float OriginY { get; set; }
        public float CellSize { get; set; } = 1f;
        public int Width { get; set; }
        public int Height { get; set; }
        public List<MapPlacement> Placements { get; set; } = new List<MapPlacement>();
    }

    public sealed class MapPlacement
    {
        public string Id { get; set; }
        public string Kind { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int W { get; set; } = 1;
        public int H { get; set; } = 1;
        public bool BlocksMovement { get; set; }
        public string BoundLocationId { get; set; }
        public string Label { get; set; }
    }
}
