using System.Text.Json.Nodes;

namespace ContentAuthoring.Shared;

public sealed record FactionReferenceHit(string FilePath, string Context, string Key, string Value);

/// <summary>
/// 删除势力前的引用保护扫描（WorldGraphEditor Faction Manager 用）：
/// 只读 Data 下的 hexWorld / formalArmy / scenario / roster JSON，找出所有引用了指定 factionId 的字段。
/// 通用 JsonNode 遍历（不绑定强类型 schema）：凡值为目标 factionId 的字段都被记录，
/// Context 取所在 definition 的 id / siteId / regionId 便于设计师定位。
/// </summary>
public static class FactionReferenceScanner
{
    static readonly string[] ReferenceKeys =
    {
        "defaultFactionId", "factionId", "ownerFactionId", "controlFactionId",
        "playerFactionId", "vassalFactionId", "overlordFactionId",
        "factionAId", "factionBId", "declarerFactionId", "targetFactionId",
    };

    /// <summary>扫描单个 JSON 文件（不存在时返回空）。</summary>
    public static List<FactionReferenceHit> ScanFile(string path, string factionId)
    {
        var hits = new List<FactionReferenceHit>();
        if (!File.Exists(path) || string.IsNullOrWhiteSpace(factionId))
            return hits;
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(File.ReadAllText(path));
        }
        catch
        {
            return hits;
        }

        if (root is not JsonObject obj)
            return hits;
        if (obj["definitions"] is not JsonArray defs)
            return hits;

        var fileLabel = Path.GetFileName(path);
        for (var i = 0; i < defs.Count; i++)
        {
            if (defs[i] is not JsonObject def)
                continue;
            var context = ReadContext(def);
            var filePath = string.IsNullOrEmpty(context) ? fileLabel : fileLabel + " / " + context;
            WalkNode(def, string.Empty, factionId, filePath, hits);
        }

        return hits;
    }

    /// <summary>递归扫描整个 Data 目录，避免 Character、Army、Scenario、World 引用漏报。</summary>
    public static List<FactionReferenceHit> ScanPackage(string dataDir, string factionId)
    {
        var hits = new List<FactionReferenceHit>();
        if (string.IsNullOrEmpty(dataDir) || !Directory.Exists(dataDir))
            return hits;

        foreach (var file in Directory.EnumerateFiles(dataDir, "*.json", SearchOption.AllDirectories)
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            hits.AddRange(ScanFile(file, factionId));
        }

        return hits;
    }

    static string ReadContext(JsonObject def)
    {
        foreach (var key in new[] { "id", "siteId", "regionId", "runtimeArmyId", "name" })
        {
            if (def[key] is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s))
                return s;
        }

        return string.Empty;
    }

    static void WalkNode(
        JsonNode? node,
        string path,
        string factionId,
        string filePath,
        List<FactionReferenceHit> hits)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var kv in obj)
                {
                    var key = kv.Key;
                    var childPath = string.IsNullOrEmpty(path) ? key : path + "." + key;
                    if (kv.Value is JsonValue val && val.TryGetValue<string>(out var text))
                    {
                        if (string.Equals(text, factionId, StringComparison.Ordinal) &&
                            ReferenceKeys.Contains(key, StringComparer.Ordinal))
                        {
                            hits.Add(new FactionReferenceHit(filePath, path, key, text));
                        }
                    }
                    else
                    {
                        WalkNode(kv.Value, childPath, factionId, filePath, hits);
                    }
                }

                break;
            case JsonArray arr:
                for (var i = 0; i < arr.Count; i++)
                {
                    var childPath = string.IsNullOrEmpty(path) ? $"[{i}]" : path + $"[{i}]";
                    WalkNode(arr[i], childPath, factionId, filePath, hits);
                }

                break;
        }
    }
}
