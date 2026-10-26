using System.Text.Json.Nodes;

namespace SurfaceAuthoring.Core;

/// <summary>Biome color only. Walkability is always baked from CompositionEngine.</summary>
public static class WorldMapPresentationPublication
{
    public static void Apply(WorldCompositionDocument source, JsonObject map, JsonObject geography, int step)
    {
        if (source.WorldMapRegions.Count == 0) return;
        var width = map["widthCells"]!.GetValue<int>();
        var height = map["heightCells"]!.GetValue<int>();
        var colors = new List<int> { 0xc1cfa0 };
        foreach (var region in source.WorldMapRegions)
        {
            if (region.Points.Count < 3 || region.ColorRgb < 0 || region.ColorRgb > 0xffffff)
                throw new InvalidDataException("Invalid display region: " + region.Id);
            if (!colors.Contains(region.ColorRgb)) colors.Add(region.ColorRgb);
        }
        if (colors.Count > 26) throw new InvalidDataException("WorldMap display palette exceeds 26 colors.");
        var rows = new JsonArray();
        var intersections = new List<double>();
        for (var y = 0; y < height; y++)
        {
            var cells = new char[width]; Array.Fill(cells, 'A');
            var sampleY = Math.Min(source.SurfaceHeightCells - 1, y * step + step / 2);
            foreach (var region in source.WorldMapRegions)
            {
                intersections.Clear();
                for (var i = 0; i < region.Points.Count; i++)
                {
                    var a = region.Points[i]; var b = region.Points[(i + 1) % region.Points.Count];
                    if ((a.Y > sampleY) == (b.Y > sampleY)) continue;
                    intersections.Add(a.X + (sampleY - a.Y) * (b.X - a.X) / (b.Y - a.Y));
                }
                intersections.Sort();
                var glyph = (char)('A' + colors.IndexOf(region.ColorRgb));
                for (var i = 0; i + 1 < intersections.Count; i += 2)
                {
                    var left = Math.Clamp((int)Math.Ceiling((intersections[i] - step / 2) / step), 0, width);
                    var right = Math.Clamp((int)Math.Ceiling((intersections[i + 1] - step / 2) / step), 0, width);
                    Array.Fill(cells, glyph, left, right - left);
                }
            }
            rows.Add(new string(cells));
        }
        var palette = new JsonArray(); foreach (var rgb in colors) palette.Add(rgb);
        map["presentationPalette"] = palette; map["presentationRows"] = rows;
        var labels = new JsonArray(); var binding = source.RuntimeSurface!;
        foreach (var region in source.WorldMapRegions.Where(r => !string.IsNullOrWhiteSpace(r.Label)))
            labels.Add(new JsonObject {
                ["stableId"] = "base:final_region:" + region.Id, ["label"] = region.Label,
                ["worldX"] = binding.OriginWorldX + region.LabelPoint.X * binding.SurfaceCellWorldSize,
                ["worldY"] = binding.OriginWorldY + region.LabelPoint.Y * binding.SurfaceCellWorldSize });
        geography["landmarks"] = labels;
    }
}
