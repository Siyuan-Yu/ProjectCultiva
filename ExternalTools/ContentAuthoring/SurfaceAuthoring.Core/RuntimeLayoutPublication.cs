using System.Text.Json.Nodes;

namespace SurfaceAuthoring.Core;

/// <summary>Publishes authored Site/business anchors without scaling any local geometry.</summary>
public static class RuntimeLayoutPublication
{
    private static readonly (string Key, string Id)[] Collections =
    [
        ("siteRegions", "siteId"), ("factionFlags", "flagId"),
        ("sitePlaces", "locationId"), ("openingEntityAnchors", "spawnKey"),
        ("sitePlacements", "stableId")
    ];

    public static IEnumerable<SurfaceAuthoringValidationIssue> Validate(WorldCompositionDocument source)
    {
        if (source.RuntimeLayout == null) yield break;
        if (source.RuntimeSurface == null)
            yield return Error("业务布局要求 RuntimeSurface 坐标绑定。");
        foreach (var (key, id) in Collections)
        {
            if (source.RuntimeLayout[key] is not JsonArray entries)
            {
                yield return Error($"业务布局缺少 {key} 数组。");
                continue;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry is not JsonObject item || item[id] is not JsonValue identity ||
                    !identity.TryGetValue<string>(out var value) || string.IsNullOrWhiteSpace(value) || !seen.Add(value))
                {
                    yield return Error($"业务布局 {key} 含缺失或重复身份。");
                    continue;
                }
                var validX = Number(item, "surfaceCellX", out var x);
                var validY = Number(item, "surfaceCellY", out var y);
                if (!validX || !validY ||
                    x < 0 || y < 0 || x >= source.SurfaceWidthCells || y >= source.SurfaceHeightCells)
                    yield return Error($"业务布局 {value} 坐标无效或超出世界。");
                if (key == "sitePlacements" &&
                    (!Number(item, "widthCells", out var w) || !Number(item, "heightCells", out var h) ||
                     w <= 0 || h <= 0 || x + w > source.SurfaceWidthCells || y + h > source.SurfaceHeightCells))
                    yield return Error($"业务布局物件 {value} 尺寸无效或超出世界。");
            }
        }
    }

    public static void Apply(WorldCompositionDocument source, JsonObject runtime)
    {
        if (source.RuntimeLayout == null) return;
        var errors = Validate(source).ToList();
        if (errors.Count > 0) throw new InvalidDataException(string.Join("\n", errors.Select(x => x.Message)));
        var binding = source.RuntimeSurface!;
        foreach (var (key, _) in Collections)
        {
            var result = new JsonArray();
            foreach (var node in source.RuntimeLayout[key]!.AsArray())
            {
                var item = node!.DeepClone().AsObject();
                var x = item["surfaceCellX"]!.GetValue<double>();
                var y = item["surfaceCellY"]!.GetValue<double>();
                item.Remove("surfaceCellX"); item.Remove("surfaceCellY");
                var prefix = key == "siteRegions" ? "arrivalWorld" : "world";
                item[prefix + "X"] = binding.OriginWorldX + x * binding.SurfaceCellWorldSize;
                item[prefix + "Y"] = binding.OriginWorldY + y * binding.SurfaceCellWorldSize;
                if (key == "sitePlacements")
                {
                    item["worldWidth"] = item["widthCells"]!.GetValue<double>() * binding.SurfaceCellWorldSize;
                    item["worldHeight"] = item["heightCells"]!.GetValue<double>() * binding.SurfaceCellWorldSize;
                    item.Remove("widthCells"); item.Remove("heightCells");
                    item["chunkX"] = (int)Math.Floor(x / 50);
                    item["chunkY"] = (int)Math.Floor(y / 50);
                }
                result.Add(item);
            }
            runtime[key] = result;
        }
    }

    private static bool Number(JsonObject item, string key, out double value)
    {
        value = 0;
        return item[key] is JsonValue scalar && scalar.TryGetValue<double>(out value) && double.IsFinite(value);
    }
    private static SurfaceAuthoringValidationIssue Error(string message) => new(ValidationSeverity.Error, message);
}
