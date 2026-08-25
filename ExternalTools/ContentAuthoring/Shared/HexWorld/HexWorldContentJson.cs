using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ContentAuthoring.Shared.HexWorld;

public static class HexWorldContentJson
{
    static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static HexWorldContentFile LoadFile(string path)
    {
        var json = File.ReadAllText(path);
        return Load(json);
    }

    public static HexWorldContentFile Load(string json)
    {
        var file = JsonSerializer.Deserialize<HexWorldContentFile>(json, ReadOptions)
                   ?? throw new InvalidDataException("Hex world JSON root is null.");
        return file;
    }

    public static HexWorldDefinitionDto LoadDefinition(string path) =>
        LoadDefinition(LoadFile(path));

    public static HexWorldDefinitionDto LoadDefinition(HexWorldContentFile file)
    {
        if (file.Definitions == null || file.Definitions.Count == 0)
            throw new InvalidDataException("Hex world JSON has no definitions.");
        if (file.Definitions.Count > 1)
            throw new InvalidDataException("Hex world JSON must contain exactly one definition per file.");
        return file.Definitions[0];
    }

    public static void SaveFile(string path, HexWorldDefinitionDto definition)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, Serialize(definition));
    }

    public static string Serialize(HexWorldDefinitionDto definition)
    {
        var file = new HexWorldContentFile
        {
            SchemaVersion = HexWorldContentSchema.CurrentVersion,
            Definitions = new List<HexWorldDefinitionDto> { definition },
        };
        return JsonSerializer.Serialize(file, WriteOptions);
    }

    public static void NormalizeForSave(HexWorldDefinitionDto definition)
    {
        definition.Type = HexWorldContentSchema.DefinitionType;
        definition.Cells = definition.Cells
            .OrderBy(c => c.R)
            .ThenBy(c => c.Q)
            .ToList();
        definition.Sites = definition.Sites
            .OrderBy(s => s.SiteId, StringComparer.Ordinal)
            .ToList();
        foreach (var site in definition.Sites)
        {
            HexWorldPresenceRules.EnsurePresenceDefaults(site);
            site.Footprint = site.Footprint
                .Distinct()
                .OrderBy(h => h.R)
                .ThenBy(h => h.Q)
                .ToList();
        }
    }
}
