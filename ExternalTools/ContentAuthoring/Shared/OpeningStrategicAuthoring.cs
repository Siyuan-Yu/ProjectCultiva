using System.Text.Json.Nodes;

namespace ContentAuthoring.Shared;

/// <summary>OpeningScenario.strategicOpening 的共享 Authoring 模型与保存前语义校验。</summary>
public sealed class OpeningStrategicAuthoringDto
{
    public string PlayerFactionId { get; set; } = string.Empty;
    public List<OpeningVassalageAuthoringDto> Vassalages { get; } = new();
    public List<OpeningAllianceAuthoringDto> Alliances { get; } = new();
    public List<OpeningWarAuthoringDto> InitialWars { get; } = new();
}

public sealed class OpeningVassalageAuthoringDto { public string VassalFactionId { get; set; } = string.Empty; public string OverlordFactionId { get; set; } = string.Empty; }
public sealed class OpeningAllianceAuthoringDto { public string FactionAId { get; set; } = string.Empty; public string FactionBId { get; set; } = string.Empty; }
public sealed class OpeningWarAuthoringDto { public string DeclarerFactionId { get; set; } = string.Empty; public string TargetFactionId { get; set; } = string.Empty; }

public static class OpeningStrategicAuthoring
{
    public static OpeningStrategicAuthoringDto? FromScenarioRaw(JsonObject raw)
    {
        if (raw?["strategicOpening"] is not JsonObject opening)
            return null;
        var result = new OpeningStrategicAuthoringDto { PlayerFactionId = Read(opening, "playerFactionId") };
        ReadPairs(opening["vassalages"] as JsonArray, (a, b) => result.Vassalages.Add(new() { VassalFactionId = a, OverlordFactionId = b }), "vassalFactionId", "overlordFactionId");
        ReadPairs(opening["alliances"] as JsonArray, (a, b) => result.Alliances.Add(new() { FactionAId = a, FactionBId = b }), "factionAId", "factionBId");
        ReadPairs(opening["initialWars"] as JsonArray, (a, b) => result.InitialWars.Add(new() { DeclarerFactionId = a, TargetFactionId = b }), "declarerFactionId", "targetFactionId");
        return result;
    }

    /// <summary>只替换目标 Scenario 的 strategicOpening 节点，不重建其余字段。</summary>
    public static void ApplyToScenarioRaw(JsonObject raw, OpeningStrategicAuthoringDto value)
    {
        raw["strategicOpening"] = new JsonObject
        {
            ["playerFactionId"] = value.PlayerFactionId,
            ["vassalages"] = new JsonArray(value.Vassalages.Select(v => (JsonNode)new JsonObject { ["vassalFactionId"] = v.VassalFactionId, ["overlordFactionId"] = v.OverlordFactionId }).ToArray()),
            ["alliances"] = new JsonArray(value.Alliances.Select(a => (JsonNode)new JsonObject { ["factionAId"] = a.FactionAId, ["factionBId"] = a.FactionBId }).ToArray()),
            ["initialWars"] = new JsonArray(value.InitialWars.Select(w => (JsonNode)new JsonObject { ["declarerFactionId"] = w.DeclarerFactionId, ["targetFactionId"] = w.TargetFactionId }).ToArray()),
        };
    }

    /// <summary>与 Runtime ContentReferenceValidator 的 strategicOpening 规则保持同一语义。</summary>
    public static List<string> Validate(OpeningStrategicAuthoringDto value, IEnumerable<string> factionIds)
    {
        var errors = new List<string>();
        var known = new HashSet<string>(factionIds ?? Array.Empty<string>(), StringComparer.Ordinal);
        void Require(string id, string label)
        {
            if (string.IsNullOrWhiteSpace(id)) errors.Add(label + "不能为空。");
            else if (!known.Contains(id)) errors.Add(label + "引用了不存在的势力：" + id);
        }

        Require(value.PlayerFactionId, "玩家势力");
        var vassals = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < value.Vassalages.Count; i++)
        {
            var v = value.Vassalages[i]; var label = "附庸关系第 " + (i + 1) + " 行";
            Require(v.VassalFactionId, label + "的附庸势力"); Require(v.OverlordFactionId, label + "的宗主势力");
            if (v.VassalFactionId == v.OverlordFactionId) errors.Add(label + "中附庸势力与宗主势力不能相同。");
            if (!vassals.TryAdd(v.VassalFactionId, v.OverlordFactionId)) errors.Add("同一势力不能同时拥有两个宗主：" + v.VassalFactionId);
        }
        foreach (var pair in vassals) if (vassals.ContainsKey(pair.Value)) errors.Add("当前版本不支持附庸层级套娃：" + pair.Key + " → " + pair.Value);

        var allianceMembers = new HashSet<string>(StringComparer.Ordinal); var alliancePairs = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < value.Alliances.Count; i++)
        {
            var a = value.Alliances[i]; var label = "开局联盟第 " + (i + 1) + " 行"; Require(a.FactionAId, label + "的势力 A"); Require(a.FactionBId, label + "的势力 B");
            var key = Pair(a.FactionAId, a.FactionBId);
            if (a.FactionAId == a.FactionBId) errors.Add(label + "的双方势力不能相同。");
            if (!alliancePairs.Add(key)) errors.Add("开局联盟重复：" + a.FactionAId + " ↔ " + a.FactionBId);
            if (!allianceMembers.Add(a.FactionAId) || !allianceMembers.Add(a.FactionBId)) errors.Add("同一势力不能加入多个开局联盟。");
            if (vassals.ContainsKey(a.FactionAId) || vassals.ContainsKey(a.FactionBId)) errors.Add("已是附庸的势力不能独立参加开局联盟。");
        }

        var wars = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < value.InitialWars.Count; i++)
        {
            var w = value.InitialWars[i]; var label = "开局战争第 " + (i + 1) + " 行"; Require(w.DeclarerFactionId, label + "的宣战方"); Require(w.TargetFactionId, label + "的目标方");
            var key = Pair(w.DeclarerFactionId, w.TargetFactionId);
            if (w.DeclarerFactionId == w.TargetFactionId) errors.Add(label + "的宣战方与目标方不能相同。");
            if (!wars.Add(key)) errors.Add("开局战争重复或存在反向重复：" + w.DeclarerFactionId + " ↔ " + w.TargetFactionId);
            if (alliancePairs.Contains(key)) errors.Add("同一势力对不能同时配置开局联盟和开局战争：" + w.DeclarerFactionId + " ↔ " + w.TargetFactionId);
        }
        return errors;
    }

    static string Pair(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;
    static string Read(JsonObject obj, string key) => obj[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text ?? string.Empty : string.Empty;
    static void ReadPairs(JsonArray? source, Action<string, string> add, string keyA, string keyB)
    {
        if (source == null) return;
        foreach (var node in source) if (node is JsonObject obj) add(Read(obj, keyA), Read(obj, keyB));
    }
}
