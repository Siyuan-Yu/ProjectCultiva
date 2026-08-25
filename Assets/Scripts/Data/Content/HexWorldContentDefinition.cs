using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    public sealed class HexWorldContentDefinition
    {
        public DefinitionId Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public float HexSize { get; set; } = 1f;
        public string DefaultTerrain { get; set; } = "Mountain";
        public bool DefaultPassable { get; set; }
        public List<HexWorldCellDefinition> Cells { get; set; } = new List<HexWorldCellDefinition>();
        public List<HexWorldSiteDefinition> Sites { get; set; } = new List<HexWorldSiteDefinition>();
    }

    public sealed class HexWorldCellDefinition
    {
        public int Q { get; set; }
        public int R { get; set; }
        public string Terrain { get; set; } = "Plain";
        public bool? Passable { get; set; }
        public bool IsRoad { get; set; }
    }

    public sealed class HexWorldSiteDefinition
    {
        public string SiteId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string SiteType { get; set; } = string.Empty;
        public int AnchorQ { get; set; }
        public int AnchorR { get; set; }
        /// <summary>缺省时由 Loader 确定性迁移为 Anchor。</summary>
        public int? PresenceQ { get; set; }
        public int? PresenceR { get; set; }
        public List<HexWorldCoordDefinition> Footprint { get; set; } = new List<HexWorldCoordDefinition>();
        public string LocalMapId { get; set; } = string.Empty;
        public string OwnerFactionId { get; set; } = string.Empty;
    }

    public sealed class HexWorldCoordDefinition
    {
        public int Q { get; set; }
        public int R { get; set; }
    }
}
