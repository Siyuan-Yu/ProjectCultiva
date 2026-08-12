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
            var path = Path.Combine(dataDir, fileNameHint.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? fileNameHint
                : fileNameHint + ".json");
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
}
