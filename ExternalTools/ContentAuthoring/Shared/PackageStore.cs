using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;

namespace ContentAuthoring.Shared;

public sealed class ContentFile
{
    public required string Path { get; init; }
    public int SchemaVersion { get; set; } = 1;
    public required List<JsonObject> Definitions { get; set; }
    internal string? LoadedContentHash { get; set; }
    internal bool MustCreateNew { get; set; }
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

public sealed record CharacterDefinitionInfo(
    string Id,
    string Name,
    string SourceRelativePath,
    string PackageId);

public sealed class ContentPackage
{
    public required string Root { get; init; }
    public required List<ContentFile> Files { get; init; }
    public required List<DefRef> Definitions { get; init; }
    public List<ValidationIssue> LoadIssues { get; init; } = new();

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
        var loadIssues = new List<ValidationIssue>();

        foreach (var path in Directory.EnumerateFiles(dataDir, "*.json", SearchOption.AllDirectories)
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var sourceText = File.ReadAllText(path);
                if (JsonNode.Parse(sourceText) is not JsonObject obj)
                    throw new InvalidDataException("JSON 根节点必须是对象。");
                if (obj["definitions"] is not JsonArray arr)
                    throw new InvalidDataException("缺少 definitions 数组。");

                var schemaVersion = obj["schemaVersion"]?.GetValue<int>() ?? 1;
                var definitions = new List<JsonObject>();
                var fileDefinitions = new List<DefRef>();
                for (var i = 0; i < arr.Count; i++)
                {
                    if (arr[i] is not JsonObject def)
                        throw new InvalidDataException($"definitions[{i}] 必须是对象。");
                    var id = def["id"]?.GetValue<string>() ?? "";
                    var type = def["type"]?.GetValue<string>() ?? "";
                    if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(type))
                        throw new InvalidDataException($"definitions[{i}] 缺少 id 或 type。");
                    definitions.Add(def);
                    fileDefinitions.Add(new DefRef
                    {
                        Id = id,
                        Type = type,
                        Name = def["name"]?.GetValue<string>() ?? "",
                        FilePath = path,
                        Index = i,
                        Raw = def
                    });
                }

                files.Add(new ContentFile
                {
                    Path = path,
                    SchemaVersion = schemaVersion,
                    Definitions = definitions,
                    LoadedContentHash = ContentHash(sourceText)
                });
                defs.AddRange(fileDefinitions);
            }
            catch (Exception ex) when (ex is JsonException or IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or FormatException)
            {
                var location = ex is JsonException json
                    ? $"（第 {(json.LineNumber ?? 0) + 1} 行，字节列 {(json.BytePositionInLine ?? 0) + 1}）"
                    : "";
                loadIssues.Add(new ValidationIssue
                {
                    Level = "error",
                    FilePath = path,
                    Message = $"{Path.GetRelativePath(packageRoot, path)}：无法加载{location}，{ex.Message}"
                });
            }
        }

        return new ContentPackage
        {
            Root = packageRoot,
            Files = files,
            Definitions = defs,
            LoadIssues = loadIssues
        };
    }

    public static void SaveFile(ContentFile file)
    {
        var root = new JsonObject
        {
            ["schemaVersion"] = file.SchemaVersion,
            ["definitions"] = new JsonArray(file.Definitions.Select(d => d.DeepClone()).ToArray())
        };
        var text = root.ToJsonString(WriteOptions) + Environment.NewLine;
        if (string.IsNullOrWhiteSpace(text) || text.Length < 16)
            throw new InvalidOperationException("拒绝写入空内容: " + file.Path);

        var fullPath = Path.GetFullPath(file.Path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var tmp = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        using var writeLock = new System.Threading.Mutex(false,
            "Local\\XianXia.ContentAuthoring." + ContentHash(fullPath.ToUpperInvariant()));
        var lockTaken = false;
        try
        {
            try { lockTaken = writeLock.WaitOne(0); }
            catch (System.Threading.AbandonedMutexException) { lockTaken = true; }
            if (!lockTaken)
                throw new IOException("另一个编辑器正在保存此文件，请稍后重试：\n" + fullPath);
            EnsureFileUnchanged(file);
            File.WriteAllText(tmp, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            EnsureFileUnchanged(file);
            if (file.MustCreateNew) File.Move(tmp, fullPath);
            else if (File.Exists(fullPath)) File.Replace(tmp, fullPath, null);
            else File.Move(tmp, fullPath);
            file.LoadedContentHash = ContentHash(text);
            file.MustCreateNew = false;
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); }
            finally { if (lockTaken) writeLock.ReleaseMutex(); }
        }
    }

    private static string ContentHash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static void EnsureFileUnchanged(ContentFile file)
    {
        if (file.LoadedContentHash == null) return; // Explicit standalone save/new file.
        if (!File.Exists(file.Path) || ContentHash(File.ReadAllText(file.Path)) != file.LoadedContentHash)
            throw new IOException("文件已被其他编辑器或外部操作修改，未覆盖磁盘内容。请先保留当前修改，再重新加载包后编辑：\n" + file.Path);
    }

    private static void EnsureWritable(ContentPackage package)
    {
        if (package.LoadIssues.Count > 0)
            throw new InvalidOperationException("内容包有加载错误，不能保存不完整的包。请修复下列文件并重新加载：\n" +
                string.Join("\n", package.LoadIssues.Take(5).Select(issue => issue.Message)));
    }

    public static void SaveDefinition(ContentPackage package, DefRef def)
    {
        EnsureWritable(package);
        var newId = def.Raw["id"]?.GetValue<string>() ?? "";
        if (string.IsNullOrWhiteSpace(newId)) throw new InvalidOperationException("定义 ID 不能为空。");
        if (package.Definitions.Any(other => !ReferenceEquals(other, def) && other.Id == newId))
            throw new InvalidOperationException("定义 ID 已存在，未保存：" + newId);
        var file = package.Files.FirstOrDefault(f => f.Path == def.FilePath)
                   ?? throw new InvalidOperationException("找不到文件: " + def.FilePath);
        if (def.Index < 0 || def.Index >= file.Definitions.Count)
            throw new InvalidOperationException("定义索引越界: " + def.Id);
        file.Definitions[def.Index] = def.Raw;
        SaveFile(file);
        def.Id = newId;
        def.Name = def.Raw["name"]?.GetValue<string>() ?? def.Name;
        def.Type = def.Raw["type"]?.GetValue<string>() ?? def.Type;
    }

    /// <summary>
    /// 从包内删除一条 definition：写回 JSON；若文件空则删文件。
    /// 若是 quest，会顺带清掉各地点 questOfferIds 中的引用（不删关联 contentEvent）。
    /// </summary>
    public static void DeleteDefinition(ContentPackage package, DefRef def)
    {
        if (package == null || def == null)
            throw new ArgumentNullException(nameof(def));
        EnsureWritable(package);

        var file = package.Files.FirstOrDefault(f =>
                       string.Equals(f.Path, def.FilePath, StringComparison.OrdinalIgnoreCase))
                   ?? throw new InvalidOperationException("找不到文件: " + def.FilePath);
        EnsureFileUnchanged(file);

        var removed = file.Definitions.RemoveAll(d =>
            string.Equals(d["id"]?.GetValue<string>(), def.Id, StringComparison.Ordinal));
        if (removed == 0 && def.Index >= 0 && def.Index < file.Definitions.Count)
            file.Definitions.RemoveAt(def.Index);

        package.Definitions.RemoveAll(d =>
            string.Equals(d.Id, def.Id, StringComparison.Ordinal) ||
            (string.Equals(d.FilePath, def.FilePath, StringComparison.OrdinalIgnoreCase) &&
             d.Index == def.Index));

        if (string.Equals(def.Type, "quest", StringComparison.Ordinal))
            ClearQuestOfferReferences(package, def.Id);

        if (file.Definitions.Count == 0)
        {
            package.Files.RemoveAll(f =>
                string.Equals(f.Path, file.Path, StringComparison.OrdinalIgnoreCase));
            if (File.Exists(file.Path))
                File.Delete(file.Path);
            return;
        }

        // 重排同文件内 Index
        for (var i = 0; i < package.Definitions.Count; i++)
        {
            var d = package.Definitions[i];
            if (!string.Equals(d.FilePath, file.Path, StringComparison.OrdinalIgnoreCase))
                continue;
            var idx = file.Definitions.FindIndex(x =>
                string.Equals(x["id"]?.GetValue<string>(), d.Id, StringComparison.Ordinal));
            if (idx >= 0)
                d.Index = idx;
        }

        SaveFile(file);
    }

    static void ClearQuestOfferReferences(ContentPackage package, string questId)
    {
        if (string.IsNullOrWhiteSpace(questId))
            return;
        var touched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var region in package.OfType("worldRegion").ToList())
        {
            if (region.Raw["locations"] is not JsonArray locs)
                continue;
            var changed = false;
            foreach (var node in locs)
            {
                if (node is not JsonObject loc)
                    continue;
                if (loc["questOfferIds"] is not JsonArray offers)
                    continue;
                var list = offers.Select(x => x?.GetValue<string>() ?? "")
                    .Where(s => s.Length > 0 && !string.Equals(s, questId, StringComparison.Ordinal))
                    .ToList();
                if (list.Count == offers.Count)
                    continue;
                changed = true;
                if (list.Count == 0)
                    loc.Remove("questOfferIds");
                else
                    loc["questOfferIds"] = new JsonArray(list.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());
            }

            if (changed)
                touched.Add(region.Id);
        }

        foreach (var id in touched)
        {
            var region = package.Find(id);
            if (region != null)
                SaveDefinition(package, region);
        }
    }

    public static DefRef AppendDefinition(ContentPackage package, string fileNameHint, JsonObject raw)
    {
        EnsureWritable(package);
        var dataDir = Path.Combine(package.Root, "Data");
        var type = raw["type"]?.GetValue<string>() ?? "";
        var id = raw["id"]?.GetValue<string>() ?? "";
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(type))
            throw new InvalidOperationException("新定义必须填写 id 和 type。");
        if (package.Find(id) != null)
            throw new InvalidOperationException("定义 ID 已存在，未新增：" + id);
        if (string.Equals(type, "quest", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(id))
            fileNameHint = ContentPathRules.ResolveQuestFile(id);
        else if (string.Equals(type, "contentEvent", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(id))
            fileNameHint = ContentPathRules.ResolveEventFile(id);

        var relative = fileNameHint.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? fileNameHint : fileNameHint + ".json";
        if (!relative.Contains('/') && !relative.Contains('\\'))
            relative = ContentPathRules.RelativeTypePath(type, relative);
        var path = Path.GetFullPath(ContentPathRules.CombineDataPath(dataDir, relative));
        var dataRoot = Path.GetFullPath(dataDir) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(dataRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("新定义必须保存到当前包的 Data 目录：" + path);
        var file = package.Files.FirstOrDefault(f =>
            string.Equals(Path.GetFullPath(f.Path), path, StringComparison.OrdinalIgnoreCase));
        var isNewFile = file == null;
        if (file == null)
        {
            if (File.Exists(path))
                throw new IOException("目标文件已存在但尚未加载，请重新加载包后再新增：" + path);
            file = new ContentFile { Path = path, SchemaVersion = 1, Definitions = new List<JsonObject>(), MustCreateNew = true };
        }

        file.Definitions.Add(raw);
        try { SaveFile(file); }
        catch { file.Definitions.RemoveAt(file.Definitions.Count - 1); throw; }
        if (isNewFile) package.Files.Add(file);
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
        return def;
    }

    public static IReadOnlyList<string> AllLocationIds(ContentPackage package)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var region in package.OfType("worldRegion"))
        {
            if (region.Raw["locations"] is not JsonArray locs) continue;
            foreach (var node in locs)
            {
                if (node is JsonObject loc && loc["id"] is JsonValue v)
                    ids.Add(v.GetValue<string>());
            }
        }

        // Continuous Surface authority no longer requires worldRegion definitions.  Current
        // locations are also referenced by WorkAreas and Surface place/site records.
        static void Collect(JsonNode? node, HashSet<string> target)
        {
            if (node is JsonObject obj)
            {
                foreach (var pair in obj)
                {
                    if (pair.Value is JsonValue value &&
                        (pair.Key is "locationId" or "boundLocationId" or "sourceLocationId" or "localLocationId") &&
                        value.TryGetValue<string>(out var id) && !string.IsNullOrWhiteSpace(id))
                        target.Add(id);
                    else
                        Collect(pair.Value, target);
                }
            }
            else if (node is JsonArray array)
                foreach (var item in array) Collect(item, target);
        }
        foreach (var def in package.Definitions) Collect(def.Raw, ids);
        return ids.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    public static bool LocationExists(ContentPackage package, string locationId) =>
        AllLocationIds(package).Contains(locationId, StringComparer.Ordinal);

    public static IReadOnlyList<string> AllQuestIds(ContentPackage package) =>
        package.OfType("quest").Select(q => q.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();

    public static IReadOnlyList<string> AllCharacterIds(ContentPackage package) =>
        package.OfType("character").Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();

    /// <summary>从当前已加载内容包生成可供各编辑器共用的人物目录。</summary>
    public static IReadOnlyList<CharacterDefinitionInfo> AllCharacterDefinitions(ContentPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        var packageId = Path.GetFileName(Path.TrimEndingDirectorySeparator(package.Root));
        return package.OfType("character")
            .Select(character => new CharacterDefinitionInfo(
                character.Id,
                string.IsNullOrWhiteSpace(character.Name) ? character.Id : character.Name,
                Path.GetRelativePath(package.Root, character.FilePath).Replace('\\', '/'),
                packageId))
            .OrderBy(character => character.Name, StringComparer.CurrentCulture)
            .ThenBy(character => character.Id, StringComparer.Ordinal)
            .ToList();
    }

    public static IReadOnlyList<string> AllResourceIds(ContentPackage package) =>
        package.OfType("resource").Select(r => r.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();

    public static IReadOnlyList<string> AllSiteIds(ContentPackage package) =>
        package.OfType("opportunitySite").Select(s => s.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();

    public static IReadOnlyList<string> AllManualIds(ContentPackage package) =>
        package.OfType("cultivation").Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();

    public static IReadOnlyList<string> AllCombatArtIds(ContentPackage package) =>
        package.OfType("combatArt").Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();

    public static IReadOnlyList<string> AllEventIds(ContentPackage package) =>
        package.OfType("contentEvent").Select(e => e.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();

    public static IReadOnlyList<string> AllSpawnTableIds(ContentPackage package) =>
        package.OfType("spawnTable").Select(t => t.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();

    public static DefRef? FindSpawnTable(ContentPackage package, string spawnTableId)
    {
        if (package == null || string.IsNullOrWhiteSpace(spawnTableId))
            return null;
        foreach (var def in package.OfType("spawnTable"))
        {
            if (string.Equals(def.Id, spawnTableId, StringComparison.Ordinal))
                return def;
        }

        return null;
    }

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

    /// <summary>onTalk 且 npcDefinitionId 指向该人物的 contentEvent id 列表。</summary>
    public static IReadOnlyList<string> EventsTalkingToNpc(ContentPackage package, string characterId)
    {
        var hits = new List<string>();
        if (string.IsNullOrWhiteSpace(characterId))
            return hits;

        foreach (var ev in package.OfType("contentEvent"))
        {
            var trigger = ev.Raw["trigger"]?.GetValue<string>() ?? "";
            if (!string.Equals(trigger, "onTalk", StringComparison.OrdinalIgnoreCase))
                continue;
            var npc = ev.Raw["npcDefinitionId"]?.GetValue<string>() ?? "";
            if (string.Equals(npc, characterId, StringComparison.Ordinal))
                hits.Add(ev.Id);
        }

        hits.Sort(StringComparer.Ordinal);
        return hits;
    }

    public static IReadOnlyList<string> WorldObjectIds(ContentPackage package, string kind)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (package == null || string.IsNullOrWhiteSpace(kind)) return ids.ToList();
        if (kind is "controlCore" or "housing" or "workArea")
        {
            foreach (var area in package.OfType("workArea"))
            {
                var isCore = JsonEdit.GetBool(area.Raw, "isControlCore", false);
                var isHousing = area.Raw["residentTags"] is JsonArray residents && residents.Count > 0 ||
                    area.Raw["tags"] is JsonArray tags &&
                    tags.Any(x => string.Equals(x?.GetValue<string>(), "home", StringComparison.Ordinal));
                if ((kind == "controlCore" && isCore) || (kind == "housing" && !isCore && isHousing) ||
                    (kind == "workArea" && !isCore && !isHousing)) ids.Add(area.Id);
            }
        }
        foreach (var surface in package.OfType("outdoorSurface"))
        {
            if (kind == "factionFlag" && surface.Raw["factionFlags"] is JsonArray flags)
                foreach (var flag in flags.OfType<JsonObject>())
                {
                    var id = JsonEdit.GetString(flag, "flagId");
                    if (id.Length > 0) ids.Add(id);
                }
            if (surface.Raw["sitePlacements"] is not JsonArray placements) continue;
            foreach (var placement in placements.OfType<JsonObject>())
            {
                var placementKind = JsonEdit.GetString(placement, "kind");
                var stableId = JsonEdit.GetString(placement, "stableId");
                if (stableId.Length == 0) continue;
                if (kind == "destructible" && placementKind is "treeL" or "treeM" or "wall" or "ore")
                {
                    var w = Math.Max(1, JsonEdit.GetInt(placement, "sourceCellsW", 1));
                    var h = Math.Max(1, JsonEdit.GetInt(placement, "sourceCellsH", 1));
                    for (var y = 0; y < h; y++) for (var x = 0; x < w; x++)
                        ids.Add(stableId + ":" + x + ":" + y);
                }
                else if ((kind == "recoverySpot" && placementKind == "recoverySpot") ||
                         (kind == "storageRoom" && placementKind == "storageRoom")) ids.Add(stableId);
            }
        }
        return ids.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    public static bool HasStaticWorldObjectCatalog(string kind) => kind is
        "controlCore" or "factionFlag" or "destructible" or "housing" or "workArea" or
        "recoverySpot" or "storageRoom";

    public static bool EventStartsQuest(JsonObject ev, string questId)
    {
        bool Starts(JsonObject owner) => owner["outcomes"] is JsonArray outcomes &&
            outcomes.OfType<JsonObject>().Any(o =>
                (string.Equals(JsonEdit.GetString(o, "kind"), "startQuest", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(JsonEdit.GetString(o, "kind"), "acceptQuestFromTarget", StringComparison.OrdinalIgnoreCase)) &&
                JsonEdit.GetString(o, "id") == questId);
        bool ChoiceStarts(JsonObject owner) => owner["choices"] is JsonArray choices &&
            choices.OfType<JsonObject>().Any(Starts);
        if (ev["steps"] is JsonArray steps)
            return steps.OfType<JsonObject>().Any(step => Starts(step) || ChoiceStarts(step));
        return ChoiceStarts(ev);
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
            Definitions = new List<JsonObject> { clone },
            LoadedContentHash = File.Exists(filePath) ? ContentHash(File.ReadAllText(filePath)) : null
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
        var sourceText = File.ReadAllText(filePath);
        var source = JsonNode.Parse(sourceText)!.AsObject();
        if (source["definitions"]!.AsArray().Count != 1)
            throw new InvalidOperationException("该文件包含多条定义，请使用「打开包」后选择地图；独立打开不会截断同文件的其他内容。\n" + filePath);

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
            SchemaVersion = source["schemaVersion"]?.GetValue<int>() ?? 1,
            Definitions = new List<JsonObject> { raw },
            LoadedContentHash = ContentHash(sourceText)
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
