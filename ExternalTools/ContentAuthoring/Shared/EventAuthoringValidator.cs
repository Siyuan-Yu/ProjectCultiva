using System.Text.Json.Nodes;

namespace ContentAuthoring.Shared;

/// <summary>EventEditor and PackageBrowser share event shape/reference diagnostics.</summary>
public static class EventAuthoringValidator
{
    public static List<string> Validate(JsonObject raw, ContentPackage package)
    {
        var errors = new List<string>();
        string S(JsonObject o, string key, string fallback = "") => JsonEdit.GetString(o, key, fallback);
        void Fields(JsonObject o, IEnumerable<string> allowed, string context)
        {
            var set = allowed.ToHashSet(StringComparer.Ordinal);
            foreach (var key in o.Select(p => p.Key)) if (!set.Contains(key)) errors.Add(context + " 含未知字段：" + key);
        }
        void Character(string id)
        {
            if (id.Length > 0 && package.Find(id)?.Type != "character") errors.Add("人物定义不存在：" + id);
        }
        JsonArray Array(JsonObject o, string key)
        {
            if (o[key] == null) return new JsonArray();
            if (o[key] is JsonArray array) return array;
            errors.Add(key + " 必须为数组"); return new JsonArray();
        }
        bool Minigame(JsonObject o) => Array(o, "outcomes").OfType<JsonObject>().Any(x => string.Equals(S(x, "kind").Trim(), "startMinigame", StringComparison.OrdinalIgnoreCase));
        Fields(raw, SchemaFields.TypeFields["contentEvent"], "事件");
        if (string.IsNullOrWhiteSpace(S(raw, "id"))) errors.Add("事件标识不能为空");
        if (S(raw, "onceScope", "global") is not ("global" or "perTarget" or "perActorTarget")) errors.Add("重复范围无效");
        var trigger = S(raw, "trigger");
        var npcDefinitionId = S(raw, "npcDefinitionId");
        var worldOpportunityId = S(raw, "worldOpportunityId");
        var npcTags = Array(raw, "npcTags");
        var objectKind = S(raw, "worldObjectKind");
        var objectId = S(raw, "worldObjectId");
        if (trigger == "onTalk")
        {
            Character(npcDefinitionId);
            if (worldOpportunityId.Length > 0 && package.Find(worldOpportunityId)?.Type != "worldOpportunity")
                errors.Add("Opportunity 模板不存在：" + worldOpportunityId);
            foreach (var tag in npcTags)
                if (tag is not JsonValue value || !value.TryGetValue<string>(out var text) || string.IsNullOrWhiteSpace(text))
                    errors.Add("人物标签必须是非空文字");
        }
        if (trigger == "onInspect")
        {
            var kinds = UiLabels.WorldObjectKinds.Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
            if (!kinds.Contains(objectKind)) errors.Add("调查事件必须选择合法的世界物体类型");
            if (objectKind == "opportunityObject")
            {
                var opportunity = package.Find(worldOpportunityId);
                if (opportunity?.Type != "worldOpportunity" || JsonEdit.GetString(opportunity.Raw, "spawnKind", "npc") != "worldObject")
                    errors.Add("动态机会物体必须选择 spawnKind=worldObject 的 Opportunity 模板");
                if (objectId.Length > 0) errors.Add("动态机会物体不能填写运行时实例 ID");
            }
            if (objectId.Length > 0 && PackageStore.HasStaticWorldObjectCatalog(objectKind) &&
                !PackageStore.WorldObjectIds(package, objectKind).Contains(objectId, StringComparer.Ordinal))
                errors.Add("世界物体实例不存在于当前内容包：" + objectId);
        }
        if (S(raw, "questId").Length > 0 && package.Find(S(raw, "questId"))?.Type != "quest") errors.Add("任务定义不存在");
        if (S(raw, "locationId").Length > 0 && !PackageStore.LocationExists(package, S(raw, "locationId"))) errors.Add("地点不存在");
        var steps = Array(raw, "steps");
        var map = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var node in steps)
        {
            if (node is not JsonObject step) { errors.Add("步骤必须为对象"); continue; }
            var id = S(step, "id");
            if (string.IsNullOrWhiteSpace(id) || !map.TryAdd(id, step)) errors.Add("步骤标识为空或重复：" + id);
        }
        void Next(string next, string context)
        {
            if (next.Length > 0 && !map.ContainsKey(next)) errors.Add(context + " 下一步不存在：" + next);
        }
        void Outcomes(JsonObject owner)
        {
            foreach (var node in Array(owner, "outcomes"))
            {
                if (node is not JsonObject outcome) { errors.Add("结果必须为对象"); continue; }
                if (S(outcome, "kind") != "scheduleEvent") continue;
                if (package.Find(S(outcome, "id"))?.Type != "contentEvent") errors.Add("延迟事件必须选择存在的后续事件");
                if (outcome["amount"] is not JsonValue value || !value.TryGetValue<int>(out var days) || days < 1)
                    errors.Add("延迟天数必须为大于零的整数");
            }
        }
        void Choices(JsonObject owner, string ownerId)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in Array(owner, "choices"))
            {
                if (node is not JsonObject c) { errors.Add("选项必须为对象"); continue; }
                Fields(c, new[] { "id", "text", "conditions", "outcomes", "nextStepId", "unavailableMode", "requirementText" }, "选项");
                var id = S(c, "id");
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) errors.Add("选项标识为空或同一步骤内重复：" + id);
                if (S(c, "unavailableMode", "disabled") is not ("disabled" or "hidden")) errors.Add("选项不可用显示方式无效：" + id);
                Array(c, "conditions"); Outcomes(c);
                Next(S(c, "nextStepId"), "选项 " + id + "（Step " + ownerId + "）");
                if (Minigame(c) && S(c, "nextStepId").Length > 0) errors.Add("小游戏选项必须结束事件：" + id);
            }
        }
        Choices(raw, S(raw, "id"));
        if (!raw.ContainsKey("steps"))
        {
            if (S(raw, "entryStepId").Length > 0) errors.Add("旧单页事件不能设置入口步骤");
            return errors;
        }
        if (steps.Count == 0) errors.Add("步骤事件至少需要一个步骤");
        if (!string.IsNullOrWhiteSpace(S(raw, "body")) || Array(raw, "choices").Count > 0) errors.Add("步骤事件不能同时使用旧正文或选项");
        var entry = S(raw, "entryStepId");
        if (!map.ContainsKey(entry)) errors.Add("入口步骤不存在：" + entry);
        foreach (var step in map.Values)
        {
            Fields(step, new[] { "id", "speakerRef", "text", "nextStepId", "outcomes", "choices" }, "步骤");
            Outcomes(step);
            var speaker = S(step, "speakerRef");
            if (speaker is not ("" or "@actor" or "@target" or "@issuer")) Character(speaker);
            Choices(step, S(step, "id")); Next(S(step, "nextStepId"), "Step " + S(step, "id"));
            if (Array(step, "choices").Count > 0 && S(step, "nextStepId").Length > 0) errors.Add("有选项的步骤不能指定下一步：" + S(step, "id"));
            if (Minigame(step)) errors.Add("小游戏结果只能配置在终止选项上");
        }
        IEnumerable<string> Edges(JsonObject step) => Array(step, "choices").Count == 0
            ? new[] { S(step, "nextStepId") } : Array(step, "choices").OfType<JsonObject>().Select(c => S(c, "nextStepId"));
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var done = new HashSet<string>(StringComparer.Ordinal);
        void Visit(string id)
        {
            if (id.Length == 0 || !map.ContainsKey(id) || done.Contains(id)) return;
            if (!visiting.Add(id)) { errors.Add("禁止步骤循环：" + id); return; }
            foreach (var next in Edges(map[id])) Visit(next);
            visiting.Remove(id); done.Add(id);
        }
        foreach (var id in map.Keys) Visit(id);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        bool Terminal(string id)
        {
            if (!seen.Add(id) || !map.TryGetValue(id, out var step)) return false;
            return Edges(step).Any(next => next.Length == 0 || Terminal(next));
        }
        if (map.ContainsKey(entry) && !Terminal(entry)) errors.Add("入口必须可达至少一个结束点");
        return errors.Distinct().ToList();
    }
}
