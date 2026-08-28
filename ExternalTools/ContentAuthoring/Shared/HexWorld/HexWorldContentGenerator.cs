namespace ContentAuthoring.Shared.HexWorld;

/// <summary>Hex 世界内容生成与编辑辅助（纯 Hex，无 Legacy Graph）。</summary>
public static class HexWorldContentGenerator
{
    public static HexWorldDefinitionDto CreateBlank(
        string id,
        string name,
        int width,
        int height,
        string defaultTerrain,
        bool passable)
    {
        var world = new HexWorldDefinitionDto
        {
            Id = id,
            Type = HexWorldContentSchema.DefinitionType,
            Name = name,
            Width = width,
            Height = height,
            HexSize = HexWorldLayoutShared.DefaultHexSize,
            DefaultTerrain = defaultTerrain,
            DefaultPassable = passable,
        };

        for (var r = 0; r < height; r++)
        {
            for (var q = 0; q < width; q++)
            {
                world.Cells.Add(new HexCellDto
                {
                    Q = q,
                    R = r,
                    Terrain = defaultTerrain,
                    Passable = passable,
                    IsRoad = string.Equals(defaultTerrain, HexTerrainIds.Road, StringComparison.Ordinal),
                });
            }
        }

        return world;
    }

    public static void PaintRoadLine(HexWorldDefinitionDto world, HexCoordDto from, HexCoordDto to)
    {
        var line = new List<HexCoordDto>();
        CollectHexLine(from, to, line);
        foreach (var hex in line)
            PaintRoadTile(world, hex.Q, hex.R);
    }

    public static void PaintRoadTile(HexWorldDefinitionDto world, int q, int r)
    {
        var cell = GetCell(world, q, r);
        if (cell == null)
            return;
        cell.Terrain = HexTerrainIds.Road;
        cell.IsRoad = true;
        cell.Passable = true;
    }

    public static void SetTerrain(HexWorldDefinitionDto world, int q, int r, string terrain, bool? passable = null)
    {
        var cell = GetCell(world, q, r);
        if (cell == null)
            return;
        cell.Terrain = terrain;
        cell.IsRoad = string.Equals(terrain, HexTerrainIds.Road, StringComparison.Ordinal);
        cell.Passable = passable ?? HexTerrainPalette.DefaultPassable(terrain);
    }

    public static HexCellDto? GetCell(HexWorldDefinitionDto world, int q, int r)
    {
        var index = q + r * world.Width;
        if (index < 0 || index >= world.Cells.Count)
            return null;
        var cell = world.Cells[index];
        return cell.Q == q && cell.R == r ? cell : world.Cells.FirstOrDefault(c => c.Q == q && c.R == r);
    }

    public static void CollectHexLine(HexCoordDto from, HexCoordDto to, List<HexCoordDto> pathOut) =>
        HexWorldLayoutShared.CollectHexLine(from, to, pathOut);
}
