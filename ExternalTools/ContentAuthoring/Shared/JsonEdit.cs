using System.Text.Json.Nodes;

namespace ContentAuthoring.Shared;

public static class JsonEdit
{
    public static string GetString(JsonObject obj, string key, string fallback = "") =>
        obj[key]?.GetValue<string>() ?? fallback;

    public static bool GetBool(JsonObject obj, string key, bool fallback = false)
    {
        if (obj[key] is JsonValue v && v.TryGetValue<bool>(out var b)) return b;
        return fallback;
    }

    public static int GetInt(JsonObject obj, string key, int fallback = 0)
    {
        if (obj[key] is JsonValue v && v.TryGetValue<int>(out var n)) return n;
        return fallback;
    }

    public static double GetDouble(JsonObject obj, string key, double fallback = 0)
    {
        if (obj[key] is JsonValue v)
        {
            if (v.TryGetValue<double>(out var d)) return d;
            if (v.TryGetValue<int>(out var n)) return n;
        }
        return fallback;
    }

    public static void SetString(JsonObject obj, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) obj.Remove(key);
        else obj[key] = value;
    }

    public static JsonArray EnsureArray(JsonObject obj, string key)
    {
        if (obj[key] is JsonArray arr) return arr;
        arr = new JsonArray();
        obj[key] = arr;
        return arr;
    }

    public static string JoinStringArray(JsonNode? node)
    {
        if (node is not JsonArray arr) return "";
        return string.Join(", ", arr.Select(x => x?.GetValue<string>() ?? "").Where(s => s.Length > 0));
    }

    public static JsonArray ParseStringList(string text)
    {
        var arr = new JsonArray();
        foreach (var part in text.Split([',', '，', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            arr.Add(part.Trim());
        return arr;
    }

    public static string ConditionsToEditable(JsonNode? node) =>
        node?.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) ?? "[]";

    public static bool TryParseJsonArray(string text, out JsonArray? array, out string? error)
    {
        array = null;
        error = null;
        try
        {
            var node = JsonNode.Parse(string.IsNullOrWhiteSpace(text) ? "[]" : text);
            if (node is not JsonArray arr)
            {
                error = "必须是 JSON 数组";
                return false;
            }
            array = arr;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
