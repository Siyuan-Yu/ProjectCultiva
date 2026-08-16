namespace ContentAuthoring.Shared;

/// <summary>
/// Content/BaseGame/Data 真源路径：按 definition type 分子目录。
/// </summary>
public static class ContentPathRules
{
    public static string? FindDataDir(string? packageRoot = null)
    {
        var root = packageRoot ?? PackagePaths.FindDefaultBaseGame();
        return string.IsNullOrEmpty(root) ? null : Path.Combine(root, "Data");
    }

    /// <summary>相对工程根，供 Unity Host / Level Tester 默认路径。</summary>
    public const string RelativeDataDir = "Content/BaseGame/Data";

    /// <summary>definition type → Data 下子目录（PascalCase，与磁盘一致）。</summary>
    public static readonly IReadOnlyDictionary<string, string> TypeSubdirs =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["character"] = "Characters",
            ["quest"] = "Quests",
            ["contentEvent"] = "Events",
            ["mapLayout"] = "Maps",
            ["worldRegion"] = "Regions",
            ["localPlaceSet"] = "LocalPlaces",
            ["worldGraph"] = "WorldGraphs",
            ["chapter"] = "Chapters",
            ["openingScenario"] = "Scenarios",
            ["characterRoster"] = "Rosters",
            ["cultivation"] = "Cultivation",
            ["combatArt"] = "CombatArts",
            ["item"] = "Items",
            ["opportunitySite"] = "Sites",
            ["resource"] = "Resources",
            ["facility"] = "Facilities",
            ["settlement"] = "Settlements",
            ["job"] = "Jobs",
            ["workArea"] = "WorkAreas",
            ["schedule"] = "Schedules",
            ["spawnTable"] = "SpawnTables",
        };

    public static string SubdirForType(string type) =>
        TypeSubdirs.TryGetValue(type, out var sub) ? sub : "Misc";

    public static string TypeDataDir(string? packageRoot, string type)
    {
        var data = FindDataDir(packageRoot);
        return string.IsNullOrEmpty(data) ? "" : Path.Combine(data, SubdirForType(type));
    }

    public static string RelativeTypePath(string type, string fileName) =>
        SubdirForType(type) + "/" + fileName;

    public static string RelativeMapPath(string fileName) =>
        RelativeTypePath("mapLayout", fileName);

    public static void EnsureTypeDir(string? packageRoot, string type)
    {
        var dir = TypeDataDir(packageRoot, type);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    public static string ResolveQuestFile(string questId)
    {
        if (questId.Contains("ch01", StringComparison.OrdinalIgnoreCase) ||
            questId.Contains("_ref_", StringComparison.OrdinalIgnoreCase))
            return RelativeTypePath("quest", "ch01_reference_quests.json");
        if (questId.Contains("harness", StringComparison.OrdinalIgnoreCase))
            return RelativeTypePath("quest", "chapter1_harness_quests.json");
        return RelativeTypePath("quest", "quests.json");
    }

    public static string ResolveEventFile(string eventId)
    {
        if (eventId.Contains("ch01", StringComparison.OrdinalIgnoreCase) ||
            eventId.Contains("_ref_", StringComparison.OrdinalIgnoreCase))
            return RelativeTypePath("contentEvent", "ch01_reference_events.json");
        if (eventId.Contains("harness", StringComparison.OrdinalIgnoreCase))
            return RelativeTypePath("contentEvent", "chapter1_harness_events.json");
        return RelativeTypePath("contentEvent", "content_events.json");
    }

    public static string SuggestMapFileName(string mapId)
    {
        if (mapId.Contains("ch01", StringComparison.OrdinalIgnoreCase) ||
            mapId.Contains("reference", StringComparison.OrdinalIgnoreCase))
            return "ch01_reference_map.json";
        var slug = mapId.Replace("base:", "", StringComparison.OrdinalIgnoreCase)
            .Replace(':', '_')
            .Replace('/', '_');
        foreach (var c in Path.GetInvalidFileNameChars())
            slug = slug.Replace(c, '_');
        return string.IsNullOrWhiteSpace(slug) ? "map_new.json" : slug + ".json";
    }

    public static string SuggestQuestFileName(string questId)
    {
        var routed = ResolveQuestFile(questId);
        if (!routed.EndsWith("/quests.json", StringComparison.OrdinalIgnoreCase))
            return Path.GetFileName(routed);
        var slug = questId.Replace("base:", "", StringComparison.OrdinalIgnoreCase)
            .Replace(':', '_')
            .Replace('/', '_');
        foreach (var c in Path.GetInvalidFileNameChars())
            slug = slug.Replace(c, '_');
        return string.IsNullOrWhiteSpace(slug) ? "quest_new.json" : slug + ".json";
    }

    public static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "quest" : name;
    }

    /// <summary>把相对 Data 的路径（可含子目录）拼成绝对路径。</summary>
    public static string CombineDataPath(string dataDir, string relativePath) =>
        Path.Combine(dataDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
}
