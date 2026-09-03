using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ContentAuthoring.Shared;

/// <summary>
/// Editor 通用 faction read model（factions.json → strategicFaction）。
/// 供 WorldGraphEditor / CharacterNpcEditor / 未来编辑器共用 —— 不要在编辑器里各自
/// 解析 factions.json；统一经 ContentAuthoring.Shared（PackageStore）读取。
/// 只含「势力身份」元数据；成员 / 领土 / WorldSite 属于各自 authority。
/// </summary>
public sealed class StrategicFactionAuthoringDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    /// <summary>#RRGGBB（authoring 原样，不转 float）。</summary>
    public required string MapColor { get; init; }
    /// <summary>是否可作为 authored Territory 的 Controller（山匪 = false）。</summary>
    public bool TerritorySelectable { get; init; } = true;
    public int SortOrder { get; init; }

    public StrategicFactionAuthoringDto With(
        string? id = null,
        string? name = null,
        string? mapColor = null,
        bool? territorySelectable = null,
        int? sortOrder = null) => new()
    {
        Id = id ?? Id,
        Name = name ?? Name,
        MapColor = mapColor ?? MapColor,
        TerritorySelectable = territorySelectable ?? TerritorySelectable,
        SortOrder = sortOrder ?? SortOrder,
    };
}

public static class StrategicFactionAuthoring
{
    const string DefinitionType = "strategicFaction";
    static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>factions.json → ContentFile（schemaVersion=1 + definitions 数组；无 def 时返回 null）。</summary>
    public static ContentFile? ReadFile(string path)
    {
        if (!File.Exists(path))
            return null;
        var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
        if (root == null || root["definitions"] is not JsonArray)
            return null;
        var schemaVersion = root["schemaVersion"]?.GetValue<int>() ?? 1;
        var defs = new List<JsonObject>();
        foreach (var node in (JsonArray)root["definitions"]!)
        {
            if (node is JsonObject obj)
                defs.Add(obj);
        }

        return new ContentFile { Path = path, SchemaVersion = schemaVersion, Definitions = defs };
    }

    /// <summary>从文件直接投影（不依赖整包 PackageStore.Load）；复用 PackageStore.SaveFile 写盘保证 roundtrip 一致。</summary>
    public static List<StrategicFactionAuthoringDto> LoadStrategicFactions(string filePath)
    {
        var file = ReadFile(filePath);
        return file == null ? new List<StrategicFactionAuthoringDto>() : FromFile(file);
    }

    public static List<StrategicFactionAuthoringDto> FromFile(ContentFile file)
    {
        var list = new List<StrategicFactionAuthoringDto>();
        if (file?.Definitions == null)
            return list;
        foreach (var raw in file.Definitions)
        {
            if (raw == null)
                continue;
            if (!string.Equals(ReadString(raw, "type"), DefinitionType, StringComparison.Ordinal))
                continue;
            var id = ReadString(raw, "id");
            if (string.IsNullOrEmpty(id))
                continue;
            list.Add(new StrategicFactionAuthoringDto
            {
                Id = id,
                Name = ReadString(raw, "name") ?? string.Empty,
                MapColor = ReadString(raw, "mapColor") ?? "#B3945C",
                TerritorySelectable = ReadBool(raw, "territorySelectable", true),
                SortOrder = ReadInt(raw, "sortOrder", 0)
            });
        }

        list.Sort(Compare);
        return list;
    }

    /// <summary>从已加载 ContentPackage 投影 faction read model（复用 PackageStore，不做二次文件 IO）。</summary>
    public static List<StrategicFactionAuthoringDto> LoadStrategicFactions(ContentPackage package)
    {
        var list = new List<StrategicFactionAuthoringDto>();
        if (package == null)
            return list;

        foreach (var def in package.OfType(DefinitionType))
        {
            var raw = def.Raw;
            if (raw == null)
                continue;
            var id = ReadString(raw, "id");
            if (string.IsNullOrEmpty(id))
                continue;

            list.Add(new StrategicFactionAuthoringDto
            {
                Id = id,
                Name = ReadString(raw, "name") ?? string.Empty,
                MapColor = ReadString(raw, "mapColor") ?? "#B3945C",
                TerritorySelectable = ReadBool(raw, "territorySelectable", true),
                SortOrder = ReadInt(raw, "sortOrder", 0)
            });
        }

        list.Sort(Compare);
        return list;
    }

    public static int Compare(StrategicFactionAuthoringDto a, StrategicFactionAuthoringDto b)
    {
        var bySort = a.SortOrder.CompareTo(b.SortOrder);
        return bySort != 0 ? bySort : string.CompareOrdinal(a.Id, b.Id);
    }

    /// <summary>校验：id 非空且唯一、name 非空、mapColor 合法 #RRGGBB。返回错误文本（空 = OK）。</summary>
    public static List<string> Validate(IReadOnlyList<StrategicFactionAuthoringDto> factions)
    {
        var errors = new List<string>();
        if (factions == null)
            return errors;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in factions)
        {
            if (string.IsNullOrWhiteSpace(f.Id))
                errors.Add("势力 Id 不能为空。");
            else if (!seen.Add(f.Id))
                errors.Add($"重复的 FactionId：{f.Id}");
            if (string.IsNullOrWhiteSpace(f.Name))
                errors.Add($"势力 {f.Id} 的 Name 不能为空。");
            if (!IsValidHexColor(f.MapColor))
                errors.Add($"势力 {f.Id} 的 mapColor 必须是合法 #RRGGBB：{f.MapColor}");
        }

        return errors;
    }

    public static bool IsValidHexColor(string? color) =>
        !string.IsNullOrWhiteSpace(color) &&
        color.Length == 7 &&
        color[0] == '#' &&
        byte.TryParse(color.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber, null, out _) &&
        byte.TryParse(color.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber, null, out _) &&
        byte.TryParse(color.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber, null, out _);

    /// <summary>把 DTO 列表写回 factions.json（经 PackageStore.SaveFile，格式与读入一致；Type/排序由调用方决定前先 Validate）。</summary>
    public static void SaveStrategicFactions(string filePath, IReadOnlyList<StrategicFactionAuthoringDto> factions)
    {
        var definitions = new List<JsonObject>();
        foreach (var f in factions)
        {
            definitions.Add(new JsonObject
            {
                ["id"] = f.Id,
                ["type"] = DefinitionType,
                ["name"] = f.Name,
                ["mapColor"] = f.MapColor,
                ["territorySelectable"] = f.TerritorySelectable,
                ["sortOrder"] = f.SortOrder,
            });
        }

        var file = new ContentFile
        {
            Path = filePath,
            SchemaVersion = 1,
            Definitions = definitions,
        };
        PackageStore.SaveFile(file);
    }

    public static string FactionDefaultFilePath(string baseGameRoot) =>
        Path.Combine(baseGameRoot, "Data", "Factions", "factions.json");

    static string? ReadString(JsonObject obj, string key)
    {
        if (!obj.TryGetPropertyValue(key, out var node) || node is not JsonValue value)
            return null;
        return value.TryGetValue<string>(out var s) ? s : null;
    }

    static bool ReadBool(JsonObject obj, string key, bool fallback)
    {
        if (!obj.TryGetPropertyValue(key, out var node) || node is not JsonValue value)
            return fallback;
        return value.TryGetValue<bool>(out var b) ? b : fallback;
    }

    static int ReadInt(JsonObject obj, string key, int fallback)
    {
        if (!obj.TryGetPropertyValue(key, out var node) || node is not JsonValue value)
            return fallback;
        return value.TryGetValue<int>(out var i) ? i : fallback;
    }
}
