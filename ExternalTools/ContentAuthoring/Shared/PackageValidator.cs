using System.Text.Json.Nodes;

namespace ContentAuthoring.Shared;

public static class PackageValidator
{
    public static List<ValidationIssue> Validate(ContentPackage package)
    {
        var issues = new List<ValidationIssue>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var def in package.Definitions)
        {
            if (!seen.Add(def.Id))
            {
                issues.Add(new ValidationIssue
                {
                    Level = "error",
                    Message = $"重复 id：{def.Id}",
                    DefinitionId = def.Id,
                    FilePath = def.FilePath
                });
            }

            if (!SchemaFields.TypeFields.TryGetValue(def.Type, out var allow))
            {
                issues.Add(new ValidationIssue
                {
                    Level = "error",
                    Message = $"未知 type：{def.Type}",
                    DefinitionId = def.Id,
                    FilePath = def.FilePath
                });
                continue;
            }

            foreach (var prop in def.Raw)
            {
                if (!allow.Contains(prop.Key))
                {
                    issues.Add(new ValidationIssue
                    {
                        Level = "error",
                        Message = $"{def.Id} 含未知字段「{prop.Key}」（type={def.Type}）",
                        DefinitionId = def.Id,
                        FilePath = def.FilePath
                    });
                }
            }

            if (def.Type == "worldRegion")
                ValidateRegion(def, issues);
            if (def.Type is "quest" or "contentEvent")
                ValidateRefs(def, package, issues);
        }

        return issues;
    }

    private static void ValidateRegion(DefRef def, List<ValidationIssue> issues)
    {
        if (def.Raw["locations"] is not JsonArray locs || locs.Count == 0)
        {
            issues.Add(new ValidationIssue
            {
                Level = "error",
                Message = $"{def.Id} 缺少 locations",
                DefinitionId = def.Id,
                FilePath = def.FilePath
            });
            return;
        }

        foreach (var node in locs)
        {
            if (node is not JsonObject loc) continue;
            var lid = loc["id"]?.GetValue<string>() ?? "?";
            foreach (var prop in loc)
            {
                if (!SchemaFields.LocationFields.Contains(prop.Key))
                {
                    issues.Add(new ValidationIssue
                    {
                        Level = "error",
                        Message = $"{def.Id} 地点 {lid} 未知字段「{prop.Key}」",
                        DefinitionId = def.Id,
                        FilePath = def.FilePath
                    });
                }
            }
        }

        var start = def.Raw["startLocationId"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(start) &&
            locs.OfType<JsonObject>().All(l => l["id"]?.GetValue<string>() != start))
        {
            issues.Add(new ValidationIssue
            {
                Level = "error",
                Message = $"{def.Id} startLocationId 不存在：{start}",
                DefinitionId = def.Id,
                FilePath = def.FilePath
            });
        }
    }

    private static void ValidateRefs(DefRef def, ContentPackage package, List<ValidationIssue> issues)
    {
        void CheckCond(JsonNode? arr, string label)
        {
            if (arr is not JsonArray list) return;
            foreach (var item in list.OfType<JsonObject>())
            {
                var kind = item["kind"]?.GetValue<string>() ?? "";
                var id = item["id"]?.GetValue<string>() ?? "";
                if (string.IsNullOrEmpty(kind))
                {
                    issues.Add(new ValidationIssue
                    {
                        Level = "warn",
                        Message = $"{def.Id} {label} 缺 kind",
                        DefinitionId = def.Id,
                        FilePath = def.FilePath
                    });
                }

                if (!string.IsNullOrEmpty(id) &&
                    kind is "exploredLocation" or "atLocation" &&
                    !PackageStore.LocationExists(package, id))
                {
                    issues.Add(new ValidationIssue
                    {
                        Level = "error",
                        Message = $"{def.Id} {label} 地点不存在：{id}",
                        DefinitionId = def.Id,
                        FilePath = def.FilePath
                    });
                }
            }
        }

        if (def.Type == "quest")
        {
            CheckCond(def.Raw["offerConditions"], "offerConditions");
            CheckCond(def.Raw["completeConditions"], "completeConditions");
        }

        if (def.Type == "contentEvent")
        {
            CheckCond(def.Raw["conditions"], "conditions");
            var loc = def.Raw["locationId"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(loc) && !PackageStore.LocationExists(package, loc))
            {
                issues.Add(new ValidationIssue
                {
                    Level = "error",
                    Message = $"{def.Id} locationId 不存在：{loc}",
                    DefinitionId = def.Id,
                    FilePath = def.FilePath
                });
            }
        }
    }
}
