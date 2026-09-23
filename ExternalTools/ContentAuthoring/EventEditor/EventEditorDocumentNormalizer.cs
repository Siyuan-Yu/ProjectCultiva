using System.Text.Json.Nodes;

namespace EventEditor;

/// <summary>
/// Expands Runtime-supported sparse defaults inside the editor working copy.
/// Disk content is untouched until the user performs a real edit and saves.
/// </summary>
public static class EventEditorDocumentNormalizer
{
    public static void NormalizeForEditor(JsonObject raw)
    {
        Default(raw, "name", "");
        Default(raw, "trigger", "manual");
        Default(raw, "priority", 0);
        Default(raw, "topicText", "");
        Default(raw, "once", true);
        Default(raw, "onceScope", "global");
        DefaultArray(raw, "conditions");
        RemoveBlankString(raw, "locationId");
        RemoveBlankString(raw, "questId");
        RemoveBlankString(raw, "npcDefinitionId");
        RemoveBlankString(raw, "worldOpportunityId");
        RemoveBlankString(raw, "worldObjectKind");
        RemoveBlankString(raw, "worldObjectId");

        if (raw["choices"] is JsonArray legacyChoices)
            NormalizeChoices(legacyChoices);

        if (raw["steps"] is not JsonArray steps) return;
        foreach (var step in steps.OfType<JsonObject>())
        {
            Default(step, "speakerRef", "");
            Default(step, "text", "");
            DefaultArray(step, "outcomes");
            DefaultArray(step, "choices");
            if (step["choices"] is JsonArray choices) NormalizeChoices(choices);
        }
    }

    static void NormalizeChoices(JsonArray choices)
    {
        foreach (var choice in choices.OfType<JsonObject>())
        {
            Default(choice, "text", "");
            DefaultArray(choice, "conditions");
            DefaultArray(choice, "outcomes");
            Default(choice, "nextStepId", "");
            Default(choice, "unavailableMode", "disabled");
            Default(choice, "requirementText", "");
        }
    }

    static void Default(JsonObject obj, string key, string value)
    {
        if (!obj.ContainsKey(key)) obj[key] = value;
    }

    static void Default(JsonObject obj, string key, int value)
    {
        if (!obj.ContainsKey(key)) obj[key] = value;
    }

    static void Default(JsonObject obj, string key, bool value)
    {
        if (!obj.ContainsKey(key)) obj[key] = value;
    }

    static void DefaultArray(JsonObject obj, string key)
    {
        if (!obj.ContainsKey(key)) obj[key] = new JsonArray();
    }

    static void RemoveBlankString(JsonObject obj, string key)
    {
        if (obj[key] is JsonValue value && value.TryGetValue<string>(out var text) && string.IsNullOrWhiteSpace(text))
            obj.Remove(key);
    }
}
