namespace ContentAuthoring.Shared.HexWorld;

public sealed class HexWorldContentFile
{
    public int SchemaVersion { get; set; } = HexWorldContentSchema.CurrentVersion;
    public List<HexWorldDefinitionDto> Definitions { get; set; } = new();
}

public sealed class HexWorldDefinitionDto
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = HexWorldContentSchema.DefinitionType;
    public string Name { get; set; } = string.Empty;
    public int Width { get; set; } = 100;
    public int Height { get; set; } = 50;
    public float HexSize { get; set; } = 1f;
    public string DefaultTerrain { get; set; } = HexTerrainIds.Mountain;
    public bool DefaultPassable { get; set; }
    public List<HexCellDto> Cells { get; set; } = new();
    public List<HexWorldSiteDto> Sites { get; set; } = new();
}

public sealed class HexCellDto
{
    public int Q { get; set; }
    public int R { get; set; }
    public string Terrain { get; set; } = HexTerrainIds.Plain;
    public bool? Passable { get; set; }
    public bool IsRoad { get; set; }
}

public sealed class HexWorldSiteDto
{
    public string SiteId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string SiteType { get; set; } = "Village";
    public int AnchorQ { get; set; }
    public int AnchorR { get; set; }
    public int? PresenceQ { get; set; }
    public int? PresenceR { get; set; }
    public List<HexCoordDto> Footprint { get; set; } = new();
    public string LocalMapId { get; set; } = string.Empty;
    public string OwnerFactionId { get; set; } = string.Empty;
}

public readonly record struct HexCoordDto(int Q, int R)
{
    public static HexCoordDto From(int q, int r) => new(q, r);
}

public static class HexWorldContentSchema
{
    public const int CurrentVersion = 1;
    public const string DefinitionType = "hexWorld";
    /// <summary>HexCell.Q = column, HexCell.R = row; Odd-R offset, pointy-top (Runtime HexWorldLayout).</summary>
    public const string CoordinateSystem = HexWorldLayoutShared.CoordinateSystem;
}

public static class HexTerrainIds
{
    public const string Plain = "Plain";
    public const string Forest = "Forest";
    public const string Mountain = "Mountain";
    public const string Water = "Water";
    public const string Road = "Road";

    public static readonly string[] All =
    {
        Plain, Forest, Mountain, Water, Road
    };

    public static bool IsKnown(string? terrain) =>
        !string.IsNullOrWhiteSpace(terrain) &&
        Array.Exists(All, t => string.Equals(t, terrain, StringComparison.Ordinal));
}
