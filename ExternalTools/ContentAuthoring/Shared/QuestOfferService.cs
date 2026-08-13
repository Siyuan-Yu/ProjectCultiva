using System.Text.Json.Nodes;

namespace ContentAuthoring.Shared;

public enum QuestOfferMode
{
    Auto,
    AfterQuest,
    AtLocation,
    NpcDialogue,
    Custom
}

public static class QuestOfferService
{
    public static QuestOfferMode DetectMode(ContentPackage package, JsonObject quest)
    {
        var questId = JsonEdit.GetString(quest, "id");
        if (PackageStore.EventsStartingQuest(package, questId).Count > 0)
            return QuestOfferMode.NpcDialogue;

        var locs = PackageStore.LocationsOfferingQuest(package, questId);
        if (locs.Count > 0 && !JsonEdit.GetBool(quest, "autoOffer", false))
            return QuestOfferMode.AtLocation;

        if (TryGetSingleQuestCompletedOffer(quest, out _))
            return QuestOfferMode.AfterQuest;

        if (JsonEdit.GetBool(quest, "autoOffer", false))
            return QuestOfferMode.Auto;

        return QuestOfferMode.Custom;
    }

    public static bool TryGetSingleQuestCompletedOffer(JsonObject quest, out string previousQuestId)
    {
        previousQuestId = "";
        if (quest["offerConditions"] is not JsonArray arr || arr.Count != 1)
            return false;
        if (arr[0] is not JsonObject cond)
            return false;
        var kind = cond["kind"]?.GetValue<string>() ?? "";
        if (!string.Equals(kind, "questCompleted", StringComparison.OrdinalIgnoreCase))
            return false;
        previousQuestId = cond["id"]?.GetValue<string>() ?? "";
        return previousQuestId.Length > 0;
    }

    public static void ApplyAfterQuest(JsonObject quest, string previousQuestId)
    {
        quest["autoOffer"] = true;
        quest["offerConditions"] = new JsonArray
        {
            new JsonObject
            {
                ["kind"] = "questCompleted",
                ["id"] = previousQuestId
            }
        };
    }

    public static void ApplyAutoOffer(JsonObject quest, JsonArray? offerConditions)
    {
        quest["autoOffer"] = true;
        quest["offerConditions"] = offerConditions ?? new JsonArray();
    }

    public static void ApplyAtLocation(ContentPackage package, JsonObject quest, string locationId)
    {
        var questId = JsonEdit.GetString(quest, "id");
        quest["autoOffer"] = false;
        quest["offerConditions"] = new JsonArray();
        foreach (var loc in PackageStore.LocationsOfferingQuest(package, questId))
            PackageStore.SetLocationQuestOffer(package, loc, questId, enabled: false);

        PackageStore.SetLocationQuestOffer(package, locationId, questId, enabled: true);
    }

    public static DefRef EnsureNpcQuestEvent(
        ContentPackage package,
        JsonObject quest,
        string locationId,
        string? preferredEventId = null)
    {
        var questId = JsonEdit.GetString(quest, "id");
        var questName = JsonEdit.GetString(quest, "name", questId);
        var loc = PackageStore.FindLocationObject(package, locationId);
        var npcId = loc?["residentNpcDefinitionId"]?.GetValue<string>() ?? "";
        var npcName = package.Find(npcId)?.Name ?? "NPC";

        DefRef? existing = null;
        if (!string.IsNullOrWhiteSpace(preferredEventId))
            existing = package.Find(preferredEventId);
        if (existing == null)
        {
            var linked = PackageStore.EventsStartingQuest(package, questId);
            if (linked.Count > 0)
                existing = package.Find(linked[0]);
        }

        if (existing != null)
        {
            PatchNpcQuestEvent(existing.Raw, questId, locationId, questName, npcName);
            PackageStore.SaveDefinition(package, existing);
            quest["autoOffer"] = false;
            return existing;
        }

        var suffix = questId.Replace("base:", "", StringComparison.Ordinal).Replace(":", "_");
        var eventId = $"base:event_offer_{suffix}";
        var raw = new JsonObject
        {
            ["id"] = eventId,
            ["type"] = "contentEvent",
            ["name"] = $"发放·{questName}",
            ["body"] = $"{npcName}看向你：「{questName}，你可愿意接下？」",
            ["trigger"] = "onArrive",
            ["locationId"] = locationId,
            ["once"] = true,
            ["conditions"] = new JsonArray(),
            ["choices"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "accept",
                    ["text"] = "接下任务",
                    ["conditions"] = new JsonArray(),
                    ["outcomes"] = new JsonArray
                    {
                        new JsonObject { ["kind"] = "startQuest", ["id"] = questId }
                    }
                },
                new JsonObject
                {
                    ["id"] = "decline",
                    ["text"] = "稍后再说",
                    ["conditions"] = new JsonArray(),
                    ["outcomes"] = new JsonArray()
                }
            }
        };
        quest["autoOffer"] = false;
        return PackageStore.AppendDefinition(package, "events.json", raw);
    }

    static void PatchNpcQuestEvent(
        JsonObject ev,
        string questId,
        string locationId,
        string questName,
        string npcName)
    {
        ev["trigger"] = "onArrive";
        ev["locationId"] = locationId;
        if (string.IsNullOrWhiteSpace(JsonEdit.GetString(ev, "body")))
            ev["body"] = $"{npcName}看向你：「{questName}，你可愿意接下？」";

        var choices = ev["choices"] as JsonArray ?? new JsonArray();
        var hasStart = false;
        foreach (var choiceNode in choices)
        {
            if (choiceNode is not JsonObject choice) continue;
            if (choice["outcomes"] is not JsonArray outcomes) continue;
            foreach (var outcomeNode in outcomes)
            {
                if (outcomeNode is not JsonObject outcome) continue;
                if (string.Equals(outcome["kind"]?.GetValue<string>(), "startQuest", StringComparison.OrdinalIgnoreCase))
                {
                    outcome["id"] = questId;
                    hasStart = true;
                }
            }
        }

        if (!hasStart)
        {
            choices.Add(new JsonObject
            {
                ["id"] = "accept",
                ["text"] = "接下任务",
                ["conditions"] = new JsonArray(),
                ["outcomes"] = new JsonArray
                {
                    new JsonObject { ["kind"] = "startQuest", ["id"] = questId }
                }
            });
            ev["choices"] = choices;
        }
    }

    public static string DescribeLinks(ContentPackage package, JsonObject quest)
    {
        var questId = JsonEdit.GetString(quest, "id");
        var lines = new List<string>();
        var locs = PackageStore.LocationsOfferingQuest(package, questId);
        if (locs.Count > 0)
            lines.Add("地点 questOfferIds: " + string.Join(", ", locs));
        var events = PackageStore.EventsStartingQuest(package, questId);
        if (events.Count > 0)
            lines.Add("事件 startQuest: " + string.Join(", ", events));
        if (JsonEdit.GetBool(quest, "autoOffer", false))
            lines.Add("autoOffer = true");
        return lines.Count == 0 ? "（暂无关联）" : string.Join("\n", lines);
    }
}
