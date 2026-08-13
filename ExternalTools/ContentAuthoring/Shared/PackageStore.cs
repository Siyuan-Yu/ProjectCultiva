using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ContentAuthoring.Shared;

public sealed class ContentFile
{
    public required string Path { get; init; }
    public int SchemaVersion { get; set; } = 1;
    public required List<JsonObject> Definitions { get; set; }
}

public sealed class DefRef
{
    public required string Id { get; set; }
    public required string Type { get; set; }
    public required string Name { get; set; }
    public required string FilePath { get; set; }
    public int Index { get; set; }
    public required JsonObject Raw { get; set; }
}

public sealed class ContentPackage
{
    public required string Root { get; init; }
    public required List<ContentFile> Files { get; init; }
    public required List<DefRef> Definitions { get; init; }

    public IEnumerable<DefRef> OfType(string type) =>
        Definitions.Where(d => string.Equals(d.Type, type, StringComparison.Ordinal));

    public DefRef? Find(string id) =>
        Definitions.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.Ordinal));
}

public sealed class ValidationIssue
{
    public required string Level { get; init; } // error | warn
    public required string Message { get; init; }
    public string? DefinitionId { get; init; }
    public string? FilePath { get; init; }
}

public static class PackagePaths
{
    public static string? FindDefaultBaseGame()
    {
        // ExternalTools/ContentAuthoring/Apps/<App>/ 或 .build/... → 向上找 repo/Content/BaseGame
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 10 && dir != null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "Content", "BaseGame");
            if (File.Exists(Path.Combine(candidate, "manifest.json")))
                return candidate;

            // 兼容从 ContentAuthoring 旁路探测
            var sibling = Path.Combine(dir.FullName, "..", "Content", "BaseGame");
            var full = Path.GetFullPath(sibling);
            if (File.Exists(Path.Combine(full, "manifest.json")))
                return full;
        }

        // ContentAuthoring → ExternalTools → repo
        var fromTools = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Content", "BaseGame"));
        if (File.Exists(Path.Combine(fromTools, "manifest.json")))
            return fromTools;

        fromTools = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "Content", "BaseGame"));
        if (File.Exists(Path.Combine(fromTools, "manifest.json")))
            return fromTools;

        return null;
    }

    /// <summary>Content/BaseGame/Data — 任务／事件／mapLayout 等内容 JSON 真源。</summary>
    public static string? FindContentDataDir(string? packageRoot = null) =>
        ContentPathRules.FindDataDir(packageRoot);

    [Obsolete("Use FindContentDataDir")]
    public static string? FindDefaultLevelsDir() => FindContentDataDir();
}

public static class PackageStore
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        // Unity SimpleJson 可读 UTF-8 中文；默认 Encoder 会写成 \uXXXX 导致旧解析器失败
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static ContentPackage Load(string packageRoot)
    {
        var dataDir = Path.Combine(packageRoot, "Data");
        if (!Directory.Exists(dataDir))
            throw new DirectoryNotFoundException("包内缺少 Data 目录: " + dataDir);

        var files = new List<ContentFile>();
        var defs = new List<DefRef>();

        foreach (var path in Directory.EnumerateFiles(dataDir, "*.json", SearchOption.AllDirectories)
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            JsonNode? root;
            try
            {
                root = JsonNode.Parse(File.ReadAllText(path));
            }
            catch
            {
                continue;
            }

            if (root is not JsonObject obj) continue;
            if (obj["definitions"] is not JsonArray arr) continue;

            var schemaVersion = obj["schemaVersion"]?.GetValue<int>() ?? 1;
            var definitions = new List<JsonObject>();
            for (var i = 0; i < arr.Count; i++)
            {
                if (arr[i] is not JsonObject def) continue;
                definitions.Add(def);
                var id = def["id"]?.GetValue<string>() ?? "";
                var type = def["type"]?.GetValue<string>() ?? "";
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(type)) continue;
                defs.Add(new DefRef
                {
                    Id = id,
                    Type = type,
                    Name = def["name"]?.GetValue<string>() ?? "",
                    FilePath = path,
                    Index = definitions.Count - 1,
                    Raw = def
                });
            }

            files.Add(new ContentFile
            {
                Path = path,
                SchemaVersion = schemaVersion,
                Definitions = definitions
            });
        }

        return new ContentPackage
        {
            Root = packageRoot,
            Files = files,
            Definitions = defs
        };
    }

    public static void SaveFile(ContentFile file)
    {
        var root = new JsonObject
        {
            ["schemaVersion"] = file.SchemaVersion,
            ["definitions"] = new JsonArray(file.Definitions.Select(d => JsonNode.Parse(d.ToJsonString())!).ToArray())
        };
        var text = root.ToJsonString(WriteOptions) + Environment.NewLine;
        if (string.IsNullOrWhiteSpace(text) || text.Length < 16)
            throw new InvalidOperationException("拒绝写入空内容: " + file.Path);

        Directory.CreateDirectory(Path.GetDirectoryName(file.Path)!);
        var tmp = file.Path + ".tmp";
        File.WriteAllText(tmp, text, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Copy(tmp, file.Path, overwrite: true);
        File.Delete(tmp);
    }

    public static void SaveDefinition(ContentPackage package, DefRef def)
    {
        var file = package.Files.FirstOrDefault(f => f.Path == def.FilePath)
                   ?? throw new InvalidOperationException("找不到文件: " + def.FilePath);
        if (def.Index < 0 || def.Index >= file.Definitions.Count)
            throw new InvalidOperationException("定义索引越界: " + def.Id);
        file.Definitions[def.Index] = def.Raw;
        def.Id = def.Raw["id"]?.GetValue<string>() ?? def.Id;
        def.Name = def.Raw["name"]?.GetValue<string>() ?? def.Name;
        def.Type = def.Raw["type"]?.GetValue<string>() ?? def.Type;
        SaveFile(file);
    }

    public static DefRef AppendDefinition(ContentPackage package, string fileNameHint, JsonObject raw)
    {
        var dataDir = Path.Combine(package.Root, "Data");
        var type = raw["type"]?.GetValue<string>() ?? "";
        var id = raw["id"]?.GetValue<string>() ?? "";
        if (string.Equals(type, "quest", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(id))
            fileNameHint = ContentPathRules.ResolveQuestFile(id);
        else if (string.Equals(type, "contentEvent", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(id))
            fileNameHint = ContentPathRules.ResolveEventFile(id);

        var hint = Path.GetFileNameWithoutExtension(fileNameHint);
        var file = package.Files.FirstOrDefault(f =>
            Path.GetFileName(f.Path).Contains(hint, StringComparison.OrdinalIgnoreCase));

        if (file == null)
        {
            // fallback: quest → *quest*.json, event → *event*.json
            var token = hint.Contains("quest", StringComparison.OrdinalIgnoreCase) ? "quest"
                : hint.Contains("event", StringComparison.OrdinalIgnoreCase) ? "event"
                : hint;
            file = package.Files.FirstOrDefault(f =>
                Path.GetFileName(f.Path).Contains(token, StringComparison.OrdinalIgnoreCase));
        }

        if (file == null)
        {
            var relative = fileNameHint.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? fileNameHint
                : fileNameHint + ".json";
            if (!relative.Contains('/') && !relative.Contains('\\') && !string.IsNullOrEmpty(type))
                relative = ContentPathRules.RelativeTypePath(type, relative);
            var path = ContentPathRules.CombineDataPath(dataDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            file = new ContentFile { Path = path, SchemaVersion = 1, Definitions = new List<JsonObject>() };
            package.Files.Add(file);
        }

        file.Definitions.Add(raw);
        var def = new DefRef
        {
            Id = raw["id"]?.GetValue<string>() ?? "",
            Type = raw["type"]?.GetValue<string>() ?? "",
            Name = raw["name"]?.GetValue<string>() ?? "",
            FilePath = file.Path,
            Index = file.Definitions.Count - 1,
            Raw = raw
        };
        package.Definitions.Add(def);
        SaveFile(file);
        return def;
    }

    public static IReadOnlyList<string> AllLocationIds(ContentPackage package)
    {
        var ids = new List<string>();
        foreach (var region in package.OfType("worldRegion"))
        {
            if (region.Raw["locations"] is not JsonArray locs) continue;
            foreach (var node in locs)
            {
                if (node is JsonObject loc && loc["id"] is JsonValue v)
                    ids.Add(v.GetValue<string>());
            }
        }

        ids.Sort(StringComparer.Ordinal);
        return ids;
    }

    public static bool LocationExists(ContentPackage package, string locationId) =>
        AllLocationIds(package).Contains(locationId, StringComparer.Ordinal);

    public static IReadOnlyList<string> AllQuestIds(ContentPackage package) =>
        package.OfType("quest").Select(q => q.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();

    public static IReadOnlyList<string> AllCharacterIds(ContentPackage package) =>
        package.OfType("character").Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();

    public static IReadOnlyList<string> AllResourceIds(ContentPackage package) =>
        package.OfType("resource").Select(r => r.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();

    public static IReadOnlyList<string> AllSiteIds(ContentPackage package) =>
        package.OfType("opportunitySite").Select(s => s.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();

    public static IReadOnlyList<string> AllManualIds(ContentPackage package) =>
        package.OfType("cultivation").Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();

    public static IReadOnlyList<string> AllEventIds(ContentPackage package) =>
        package.OfType("contentEvent").Select(e => e.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();

    public static JsonObject? FindLocationObject(ContentPackage package, string locationId)
    {
        foreach (var region in package.OfType("worldRegion"))
        {
            if (region.Raw["locations"] is not JsonArray locs) continue;
            foreach (var node in locs)
            {
                if (node is JsonObject loc &&
                    string.Equals(loc["id"]?.GetValue<string>(), locationId, StringComparison.Ordinal))
                    return loc;
            }
        }

        return null;
    }

    public static DefRef? FindRegionContainingLocation(ContentPackage package, string locationId)
    {
        foreach (var region in package.OfType("worldRegion"))
        {
            if (region.Raw["locations"] is not JsonArray locs) continue;
            foreach (var node in locs)
            {
                if (node is JsonObject loc &&
                    string.Equals(loc["id"]?.GetValue<string>(), locationId, StringComparison.Ordinal))
                    return region;
            }
        }

        return null;
    }

    public static IReadOnlyList<string> LocationsOfferingQuest(ContentPackage package, string questId)
    {
        var hits = new List<string>();
        foreach (var region in package.OfType("worldRegion"))
        {
            if (region.Raw["locations"] is not JsonArray locs) continue;
            foreach (var node in locs)
            {
                if (node is not JsonObject loc) continue;
                var locId = loc["id"]?.GetValue<string>() ?? "";
                if (loc["questOfferIds"] is not JsonArray offers) continue;
                foreach (var o in offers)
                {
                    if (string.Equals(o?.GetValue<string>(), questId, StringComparison.Ordinal))
                    {
                        hits.Add(locId);
                        break;
                    }
                }
            }
        }

        hits.Sort(StringComparer.Ordinal);
        return hits;
    }

    public static void SetLocationQuestOffer(ContentPackage package, string locationId, string questId, bool enabled)
    {
        var region = FindRegionContainingLocation(package, locationId)
                     ?? throw new InvalidOperationException("找不到地点: " + locationId);
        var loc = FindLocationObject(package, locationId)
                  ?? throw new InvalidOperationException("找不到地点: " + locationId);
        var arr = loc["questOfferIds"] as JsonArray ?? new JsonArray();
        var list = arr.Select(x => x?.GetValue<string>() ?? "")
            .Where(s => s.Length > 0)
            .ToList();
        list.RemoveAll(s => string.Equals(s, questId, StringComparison.Ordinal));
        if (enabled)
            list.Add(questId);
        if (list.Count == 0)
            loc.Remove("questOfferIds");
        else
            loc["questOfferIds"] = new JsonArray(list.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());
        SaveDefinition(package, region);
    }

    public static IReadOnlyList<string> EventsStartingQuest(ContentPackage package, string questId)
    {
        var hits = new List<string>();
        foreach (var ev in package.OfType("contentEvent"))
        {
            if (EventStartsQuest(ev.Raw, questId))
                hits.Add(ev.Id);
        }

        hits.Sort(StringComparer.Ordinal);
        return hits;
    }

    public static bool EventStartsQuest(JsonObject ev, string questId)
    {
        if (ev["choices"] is not JsonArray choices) return false;
        foreach (var choiceNode in choices)
        {
            if (choiceNode is not JsonObject choice) continue;
            if (choice["outcomes"] is not JsonArray outcomes) continue;
            foreach (var outcomeNode in outcomes)
            {
                if (outcomeNode is not JsonObject outcome) continue;
                var kind = outcome["kind"]?.GetValue<string>() ?? "";
                var id = outcome["id"]?.GetValue<string>() ?? "";
                if (string.Equals(kind, "startQuest", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(id, questId, StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }

    /// <summary>写入独立 mapLayout 文件（definitions 仅含一张图）。</summary>
    public static void SaveStandaloneMapLayout(string filePath, JsonObject raw) =>
        SaveStandaloneDefinition(filePath, raw);

    /// <summary>写入独立 definition 文件（definitions 仅含一条）。</summary>
    public static void SaveStandaloneDefinition(string filePath, JsonObject raw)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("filePath empty");
        var clone = JsonNode.Parse(raw.ToJsonString()) as JsonObject
                    ?? throw new InvalidOperationException("无法克隆 definition");
        var file = new ContentFile
        {
            Path = filePath,
            SchemaVersion = 1,
            Definitions = new List<JsonObject> { clone }
        };
        SaveFile(file);
    }

    /// <summary>登记／刷新一份独立 definition 文件；preferOverrideSameId 时去掉包内同 id 的旧项。</summary>
    public static DefRef RegisterStandaloneDefinition(
        ContentPackage package,
        string filePath,
        JsonObject raw,
        bool preferOverrideSameId = true)
    {
        var clone = JsonNode.Parse(raw.ToJsonString()) as JsonObject
                    ?? throw new InvalidOperationException("无法克隆 definition");

        package.Definitions.RemoveAll(d =>
            string.Equals(d.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        package.Files.RemoveAll(f =>
            string.Equals(f.Path, filePath, StringComparison.OrdinalIgnoreCase));

        var id = clone["id"]?.GetValue<string>() ?? "";
        if (preferOverrideSameId && !string.IsNullOrEmpty(id))
            package.Definitions.RemoveAll(d => string.Equals(d.Id, id, StringComparison.Ordinal));

        var file = new ContentFile
        {
            Path = filePath,
            SchemaVersion = 1,
            Definitions = new List<JsonObject> { clone }
        };
        package.Files.Add(file);
        var def = new DefRef
        {
            Id = id,
            Type = clone["type"]?.GetValue<string>() ?? "",
            Name = clone["name"]?.GetValue<string>() ?? "",
            FilePath = filePath,
            Index = 0,
            Raw = clone
        };
        package.Definitions.Add(def);
        return def;
    }

    /// <summary>把 Levels 目录下的 mapLayout 合并进包（同 id 时 Levels 覆盖包内项）。</summary>
    public static void MergeLevelsDirectory(ContentPackage package, string? levelsDir)
    {
        if (package == null || string.IsNullOrWhiteSpace(levelsDir) || !Directory.Exists(levelsDir))
            return;

        foreach (var path in Directory.EnumerateFiles(levelsDir, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                RegisterStandaloneMap(package, path, preferOverrideSameId: true);
            }
            catch
            {
                // 跳过非 map 文件
            }
        }
    }

    /// <summary>登记／刷新一份独立地图文件；preferOverrideSameId 时去掉包内同 id 的旧项。</summary>
    public static DefRef RegisterStandaloneMap(
        ContentPackage package,
        string filePath,
        bool preferOverrideSameId = true)
    {
        var loaded = LoadMapLayoutFile(filePath);
        var raw = loaded.Raw;

        package.Definitions.RemoveAll(d =>
            string.Equals(d.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        package.Files.RemoveAll(f =>
            string.Equals(f.Path, filePath, StringComparison.OrdinalIgnoreCase));

        if (preferOverrideSameId && !string.IsNullOrEmpty(loaded.Id))
        {
            // 仅从内存列表移除同 id，不改 Content 包磁盘上的其它文件
            package.Definitions.RemoveAll(d =>
                string.Equals(d.Id, loaded.Id, StringComparison.Ordinal));
        }

        var file = new ContentFile
        {
            Path = filePath,
            SchemaVersion = 1,
            Definitions = new List<JsonObject> { raw }
        };
        package.Files.Add(file);
        var def = new DefRef
        {
            Id = loaded.Id,
            Type = "mapLayout",
            Name = loaded.Name,
            FilePath = filePath,
            Index = 0,
            Raw = raw
        };
        package.Definitions.Add(def);
        return def;
    }

    public static DefRef LoadMapLayoutFile(string filePath, string? preferId = null)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("地图文件不存在", filePath);

        var root = JsonNode.Parse(File.ReadAllText(filePath)) as JsonObject
                   ?? throw new InvalidOperationException("根节点不是对象: " + filePath);
        if (root["definitions"] is not JsonArray arr || arr.Count == 0)
            throw new InvalidOperationException("缺少 definitions: " + filePath);

        JsonObject? chosen = null;
        var index = -1;
        for (var i = 0; i < arr.Count; i++)
        {
            if (arr[i] is not JsonObject def) continue;
            var type = def["type"]?.GetValue<string>() ?? "";
            if (!string.Equals(type, "mapLayout", StringComparison.Ordinal)) continue;
            var id = def["id"]?.GetValue<string>() ?? "";
            if (!string.IsNullOrWhiteSpace(preferId) &&
                string.Equals(id, preferId, StringComparison.Ordinal))
            {
                chosen = def;
                index = i;
                break;
            }

            if (chosen == null)
            {
                chosen = def;
                index = i;
            }
        }

        if (chosen == null)
            throw new InvalidOperationException("文件中没有 mapLayout: " + filePath);

        return new DefRef
        {
            Id = chosen["id"]?.GetValue<string>() ?? "",
            Type = "mapLayout",
            Name = chosen["name"]?.GetValue<string>() ?? "",
            FilePath = filePath,
            Index = index,
            Raw = chosen
        };
    }
}
