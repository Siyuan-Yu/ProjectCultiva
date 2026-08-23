using System.Text.Json.Nodes;

namespace ContentAuthoring.Shared.HexWorld;

/// <summary>从 legacy worldGraph 迁移生成 Hex 世界（与 Runtime Ch01HexPrototypeMapBuilder 对齐）。</summary>
public static class HexWorldContentGenerator
{
    public static HexWorldDefinitionDto GenerateFromWorldGraph(
        JsonObject graph,
        string id = "base:hex_world_ch01",
        string name = "Ch01 Hex Strategic")
    {
        var width = HexWorldLayoutShared.DefaultWidth;
        var height = HexWorldLayoutShared.DefaultHeight;
        var world = CreateBlank(id, name, width, height, HexTerrainIds.Mountain, passable: false);

        var nodeHex = MapGraphNodesToHex(graph, width, height);
        var sites = BuildSitesFromGraph(graph, nodeHex);
        world.Sites.AddRange(sites);

        foreach (var site in sites)
            PaintSitePads(world, site);

        if (graph["routes"] is JsonArray routes)
        {
            foreach (var routeToken in routes)
            {
                if (routeToken is not JsonObject route)
                    continue;
                var fromId = route["fromNodeId"]?.GetValue<string>() ?? string.Empty;
                var toId = route["toNodeId"]?.GetValue<string>() ?? string.Empty;
                if (!nodeHex.TryGetValue(fromId, out var from) || !nodeHex.TryGetValue(toId, out var to))
                    continue;
                PaintRoadLine(world, from, to);
            }
        }

        HexWorldContentJson.NormalizeForSave(world);
        return world;
    }

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

    public static Dictionary<string, HexCoordDto> MapGraphNodesToHex(JsonObject graph, int width, int height)
    {
        var result = new Dictionary<string, HexCoordDto>(StringComparer.Ordinal);
        if (graph["nodes"] is not JsonArray nodes || nodes.Count == 0)
            return result;

        var raw = new Dictionary<string, (float X, float Y)>(StringComparer.Ordinal);
        var minX = float.MaxValue;
        var maxX = float.MinValue;
        var minY = float.MaxValue;
        var maxY = float.MinValue;
        foreach (var nodeToken in nodes)
        {
            if (nodeToken is not JsonObject node)
                continue;
            var id = node["id"]?.GetValue<string>();
            if (string.IsNullOrEmpty(id))
                continue;
            var x = (float)(node["worldX"]?.GetValue<double>() ?? 0);
            var y = (float)(node["worldY"]?.GetValue<double>() ?? 0);
            raw[id] = (x, y);
            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);
        }

        var spanX = Math.Max(1f, maxX - minX);
        var spanY = Math.Max(1f, maxY - minY);
        var placeW = width - HexWorldLayoutShared.PlayableOriginQ * 2;
        var placeH = height - HexWorldLayoutShared.PlayableOriginR * 2;

        foreach (var kv in raw)
        {
            var tq = (int)Math.Round((kv.Value.X - minX) / spanX * (placeW - 1));
            var tr = (int)Math.Round((kv.Value.Y - minY) / spanY * (placeH - 1));
            result[kv.Key] = new HexCoordDto(
                HexWorldLayoutShared.PlayableOriginQ + tq,
                HexWorldLayoutShared.PlayableOriginR + tr);
        }

        return result;
    }

    static List<HexWorldSiteDto> BuildSitesFromGraph(
        JsonObject graph,
        Dictionary<string, HexCoordDto> nodeHex)
    {
        var sites = new List<HexWorldSiteDto>();
        if (graph["nodes"] is not JsonArray nodes)
            return sites;

        foreach (var nodeToken in nodes)
        {
            if (nodeToken is not JsonObject node)
                continue;
            var nodeId = node["id"]?.GetValue<string>() ?? string.Empty;
            if (string.IsNullOrEmpty(nodeId) || !nodeHex.TryGetValue(nodeId, out var anchor))
                continue;

            var suffix = nodeId.Contains('_')
                ? nodeId.Substring(nodeId.LastIndexOf('_') + 1)
                : nodeId;
            var site = new HexWorldSiteDto
            {
                SiteId = "base:site_" + suffix,
                DisplayName = node["name"]?.GetValue<string>() ?? nodeId,
                SiteType = node["kind"]?.GetValue<string>() ?? "Site",
                AnchorQ = anchor.Q,
                AnchorR = anchor.R,
                LocalMapId = node["localMapId"]?.GetValue<string>() ?? string.Empty,
                LegacyNodeId = nodeId,
                Footprint = BuildFootprint(anchor, node["kind"]?.GetValue<string>()),
            };
            sites.Add(site);
        }

        return sites;
    }

    static List<HexCoordDto> BuildFootprint(HexCoordDto anchor, string? kind)
    {
        var footprint = new List<HexCoordDto> { anchor };
        var extra = FootprintExtraHexCount(kind);
        for (var d = 0; d < 6 && footprint.Count < 1 + extra; d++)
        {
            var neighbor = HexWorldLayoutShared.Neighbor(anchor, d);
            if (!footprint.Contains(neighbor))
                footprint.Add(neighbor);
        }

        return footprint;
    }

    static int FootprintExtraHexCount(string? kind) =>
        kind switch
        {
            "City" or "Town" => 2,
            "Sect" or "Fortress" or "Mine" => 1,
            _ => 0,
        };

    static void PaintSitePads(HexWorldDefinitionDto world, HexWorldSiteDto site)
    {
        var footprint = site.Footprint.Count > 0
            ? site.Footprint
            : new List<HexCoordDto> { new(site.AnchorQ, site.AnchorR) };
        foreach (var hex in footprint)
            PaintRoadTile(world, hex.Q, hex.R);
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

    public static void CollectHexLine(HexCoordDto from, HexCoordDto to, List<HexCoordDto> pathOut)
    {
        pathOut.Clear();
        var steps = HexWorldLayoutShared.Distance(from, to);
        if (steps <= 0)
        {
            pathOut.Add(from);
            return;
        }

        for (var i = 0; i <= steps; i++)
        {
            var t = i / (float)steps;
            var q = from.Q + (to.Q - from.Q) * t;
            var r = from.R + (to.R - from.R) * t;
            var s = (-from.Q - from.R) + ((-to.Q - to.R) - (-from.Q - from.R)) * t;
            pathOut.Add(CubeRound(q, r, s));
        }
    }

    static HexCoordDto CubeRound(float q, float r, float s)
    {
        var rq = Math.Round(q);
        var rr = Math.Round(r);
        var rs = Math.Round(s);
        var dq = Math.Abs(rq - q);
        var dr = Math.Abs(rr - r);
        var ds = Math.Abs(rs - s);
        if (dq > dr && dq > ds)
            rq = -rr - rs;
        else if (dr > ds)
            rr = -rq - rs;
        else
            rs = -rq - rr;
        return new HexCoordDto((int)rq, (int)rr);
    }
}
