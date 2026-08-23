namespace ContentAuthoring.Shared.HexWorld;

/// <summary>与 Runtime HexTerrainPresentation 对齐的 WPF/Editor 配色。</summary>
public static class HexTerrainPalette
{
    public static readonly IReadOnlyList<(string Id, string Label, byte R, byte G, byte B)> Legend =
        new (string, string, byte, byte, byte)[]
        {
            (HexTerrainIds.Plain, "平原", 0xD8, 0xD0, 0xB0),
            (HexTerrainIds.Forest, "森林", 0xB8, 0xD6, 0x9E),
            (HexTerrainIds.Water, "水域", 0xAD, 0xD1, 0xF0),
            (HexTerrainIds.Mountain, "岩地", 0xB8, 0xAD, 0x9E),
            (HexTerrainIds.Road, "道路", 0xC8, 0xB8, 0x88),
        };

    public static (byte R, byte G, byte B) ResolveRgb(string terrain, bool isRoad, bool passable)
    {
        if (isRoad || string.Equals(terrain, HexTerrainIds.Road, StringComparison.Ordinal))
            return (0xC8, 0xB8, 0x88);
        return terrain switch
        {
            HexTerrainIds.Forest => (0xB8, 0xD6, 0x9E),
            HexTerrainIds.Water => (0xAD, 0xD1, 0xF0),
            HexTerrainIds.Mountain => (0xB8, 0xAD, 0x9E),
            _ when !passable => (0x98, 0x90, 0x88),
            _ => (0xD8, 0xD0, 0xB0),
        };
    }

    public static bool DefaultPassable(string terrain) =>
        !string.Equals(terrain, HexTerrainIds.Water, StringComparison.Ordinal) &&
        !string.Equals(terrain, HexTerrainIds.Mountain, StringComparison.Ordinal);

    public static string ResolveLabel(string terrain) =>
        terrain switch
        {
            HexTerrainIds.Forest => "森林",
            HexTerrainIds.Water => "水域",
            HexTerrainIds.Mountain => "岩地",
            HexTerrainIds.Road => "道路",
            _ => "平原",
        };
}
