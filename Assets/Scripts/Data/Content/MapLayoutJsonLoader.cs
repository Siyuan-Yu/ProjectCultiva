using System.IO;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Data.Serialization;

namespace XianXia.Data.Content
{
    /// <summary>
    /// 从单份 mapLayout JSON（definitions 文件或单对象）解析地图，供 Level Tester 覆盖／热换。
    /// </summary>
    public static class MapLayoutJsonLoader
    {
        public static Result<MapLayoutDefinition> LoadFromFile(string path, string preferId = null)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return Result.Fail<MapLayoutDefinition>(
                    ErrorCode.ContentLoadFailed,
                    "mapLayout file missing: " + path);
            return LoadFromText(File.ReadAllText(path), preferId, path);
        }

        public static Result<MapLayoutDefinition> LoadFromText(
            string json,
            string preferId = null,
            string sourceLabel = "mapLayout")
        {
            if (string.IsNullOrWhiteSpace(json))
                return Result.Fail<MapLayoutDefinition>(
                    ErrorCode.ContentLoadFailed,
                    "mapLayout JSON empty: " + sourceLabel);

            JsonValue root;
            try
            {
                root = SimpleJson.Parse(json);
            }
            catch (System.Exception ex)
            {
                return Result.Fail<MapLayoutDefinition>(
                    ErrorCode.ContentLoadFailed,
                    "Failed to parse " + sourceLabel + ": " + ex.Message);
            }

            if (root.Kind != JsonValueKind.Object)
                return Result.Fail<MapLayoutDefinition>(
                    ErrorCode.ContentLoadFailed,
                    "Root must be object: " + sourceLabel);

            if (root.TryGetProperty("definitions", out var defs) && defs.Kind == JsonValueKind.Array)
            {
                MapLayoutDefinition first = null;
                foreach (var item in defs.Array)
                {
                    if (item.Kind != JsonValueKind.Object)
                        continue;
                    var type = item.GetString("type", string.Empty);
                    if (!string.Equals(type, "mapLayout", System.StringComparison.Ordinal))
                        continue;
                    var parsed = TryParseItem(item, sourceLabel);
                    if (parsed.IsFailure)
                        return parsed;
                    if (!string.IsNullOrWhiteSpace(preferId) &&
                        parsed.Value.Id.ToString() == preferId.Trim())
                        return parsed;
                    if (first == null)
                        first = parsed.Value;
                }

                if (first != null)
                    return Result.Ok(first);
                return Result.Fail<MapLayoutDefinition>(
                    ErrorCode.ContentLoadFailed,
                    "No mapLayout in definitions: " + sourceLabel);
            }

            var typeRoot = root.GetString("type", "mapLayout");
            if (!string.Equals(typeRoot, "mapLayout", System.StringComparison.Ordinal) &&
                !root.TryGetProperty("placements", out _))
            {
                return Result.Fail<MapLayoutDefinition>(
                    ErrorCode.ContentLoadFailed,
                    "Not a mapLayout JSON: " + sourceLabel);
            }

            return TryParseItem(root, sourceLabel);
        }

        static Result<MapLayoutDefinition> TryParseItem(JsonValue item, string sourceLabel)
        {
            var idRaw = item.GetString("id", string.Empty);
            if (string.IsNullOrWhiteSpace(idRaw))
                return Result.Fail<MapLayoutDefinition>(
                    ErrorCode.MissingRequiredField,
                    "mapLayout.id required: " + sourceLabel);

            var idParsed = DefinitionId.Parse(idRaw);
            if (idParsed.IsFailure)
                return Result.Fail<MapLayoutDefinition>(idParsed.Error);

            var layout = new MapLayoutDefinition
            {
                Id = idParsed.Value,
                Name = item.GetString("name", string.Empty),
                WorldRegionId = item.GetString("worldRegionId", string.Empty),
                OriginX = (float)item.GetNumber("originX", 0),
                OriginY = (float)item.GetNumber("originY", 0),
                CellSize = (float)item.GetNumber("cellSize", 1),
                Width = (int)item.GetNumber("width", 0),
                Height = (int)item.GetNumber("height", 0),
                ExitTriggerDepth = (float)item.GetNumber("exitTriggerDepth", 0)
            };

            if (layout.Width <= 0 || layout.Height <= 0 || layout.CellSize <= 0f)
                return Result.Fail<MapLayoutDefinition>(
                    ErrorCode.MissingRequiredField,
                    "mapLayout.width/height/cellSize must be positive: " + sourceLabel);

            if (item.TryGetProperty("placements", out var placementsNode))
            {
                if (placementsNode.Kind != JsonValueKind.Array)
                    return Result.Fail<MapLayoutDefinition>(
                        ErrorCode.ContentLoadFailed,
                        "mapLayout.placements must be array: " + sourceLabel);

                foreach (var pNode in placementsNode.Array)
                {
                    if (pNode.Kind != JsonValueKind.Object)
                        continue;
                    var placement = new MapPlacement
                    {
                        Id = pNode.GetString("id", string.Empty),
                        Kind = pNode.GetString("kind", "wall"),
                        X = (int)pNode.GetNumber("x", 0),
                        Y = (int)pNode.GetNumber("y", 0),
                        W = (int)pNode.GetNumber("w", 1),
                        H = (int)pNode.GetNumber("h", 1),
                        BlocksMovement = pNode.GetBool("blocksMovement", false),
                        BoundLocationId = pNode.GetString("boundLocationId", string.Empty),
                        Label = pNode.GetString("label", string.Empty),
                        LootItemId = pNode.GetString("lootItemId", string.Empty),
                        SpawnTableId = pNode.GetString("spawnTableId", string.Empty),
                        SpawnCount = (int)pNode.GetNumber("spawnCount", 0)
                    };
                    if (string.IsNullOrWhiteSpace(placement.Id))
                        placement.Id = "p_" + layout.Placements.Count;
                    layout.Placements.Add(placement);
                }
            }

            return Result.Ok(layout);
        }
    }
}
