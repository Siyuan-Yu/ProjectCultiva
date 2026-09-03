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
        public List<TerritoryRegionContentDefinition> TerritoryRegions { get; set; } = new List<TerritoryRegionContentDefinition>();

        /// <summary>
        /// 不属于任何 WorldSite TerritoryRegion 的荒野 Hex 明确政治控制权（Editor Territory Brush 单格涂）。
        /// 加载顺序：清空 ControlFactionId → apply standalone → apply TerritoryRegions；
        /// standalone 与 Region 同含同一 Hex = Content ERROR。
        /// </summary>
        public List<HexWorldStandaloneHexControlDefinition> StandaloneTerritoryHexes { get; set; }
            = new List<HexWorldStandaloneHexControlDefinition>();
    }

    /// <summary>荒野单格控制权（不在任何 WorldSite Region 内）。</summary>
    public sealed class HexWorldStandaloneHexControlDefinition
    {
        public int Q { get; set; }
        public int R { get; set; }
        public string ControlFactionId { get; set; } = string.Empty;
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
        /// <summary>绑定 TerritoryRegion（内容加载后由 Region 建立 Site↔Region 链接）。</summary>
        public string TerritoryRegionId { get; set; } = string.Empty;
    }

    /// <summary>政治辖区（2J §6.3）：Hexes 固化在 Content，Runtime 不重算。</summary>
    public sealed class TerritoryRegionContentDefinition
    {
        public string RegionId { get; set; } = string.Empty;
        public string PrimaryWorldSiteId { get; set; } = string.Empty;
        public string ControlFactionId { get; set; } = string.Empty;
        public List<HexWorldCoordDefinition> Hexes { get; set; } = new List<HexWorldCoordDefinition>();
    }

    public sealed class HexWorldCoordDefinition
    {
        public int Q { get; set; }
        public int R { get; set; }
    }
}
