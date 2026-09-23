using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.IO;

namespace EventEditor;

public sealed class GraphNodeLayout
{
    public string StepId { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public bool Collapsed { get; set; }
}

public sealed class EventGraphLayout
{
    public string EventId { get; set; } = "";
    public List<GraphNodeLayout> Nodes { get; } = new();

    public GraphNodeLayout GetOrCreate(string stepId, int index = 0)
    {
        var found = Nodes.FirstOrDefault(n => n.StepId == stepId);
        if (found != null) return found;
        found = new GraphNodeLayout { StepId = stepId, X = 80 + index * 300, Y = 100 };
        Nodes.Add(found);
        return found;
    }

    public GraphNodeLayout? Find(string stepId) => Nodes.FirstOrDefault(n => n.StepId == stepId);

    public void Prune(IEnumerable<string> stepIds)
    {
        var keep = stepIds.ToHashSet(StringComparer.Ordinal);
        Nodes.RemoveAll(n => !keep.Contains(n.StepId));
    }

    public string ToJson()
    {
        var nodes = new JsonArray(Nodes.Select(n => (JsonNode)new JsonObject
        {
            ["stepId"] = n.StepId, ["x"] = n.X, ["y"] = n.Y, ["collapsed"] = n.Collapsed
        }).ToArray());
        return new JsonObject { ["eventId"] = EventId, ["nodes"] = nodes }.ToJsonString();
    }

    public static EventGraphLayout FromJson(string json)
    {
        var obj = JsonNode.Parse(json)?.AsObject() ?? new JsonObject();
        var layout = new EventGraphLayout { EventId = obj["eventId"]?.GetValue<string>() ?? "" };
        if (obj["nodes"] is JsonArray nodes)
            foreach (var node in nodes.OfType<JsonObject>())
                layout.Nodes.Add(new GraphNodeLayout
                {
                    StepId = node["stepId"]?.GetValue<string>() ?? "",
                    X = node["x"]?.GetValue<double>() ?? 0,
                    Y = node["y"]?.GetValue<double>() ?? 0,
                    Collapsed = node["collapsed"]?.GetValue<bool>() ?? false
                });
        return layout;
    }
}

public sealed class GraphLayoutStore
{
    readonly string _path;
    readonly Dictionary<string, EventGraphLayout> _layouts = new(StringComparer.Ordinal);
    static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public GraphLayoutStore(string packageRoot)
    {
        _path = System.IO.Path.Combine(packageRoot, "Authoring", "EventEditor", "layouts.v1.json");
        Load();
    }

    public string Path => _path;

    void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            var root = JsonNode.Parse(File.ReadAllText(_path))?.AsObject();
            if (root?["events"] is not JsonArray events) return;
            foreach (var item in events.OfType<JsonObject>())
            {
                var layout = EventGraphLayout.FromJson(item.ToJsonString());
                if (layout.EventId.Length > 0) _layouts[layout.EventId] = layout;
            }
        }
        catch { /* invalid editor metadata never blocks Runtime Content */ }
    }

    public EventGraphLayout Get(string eventId, IEnumerable<string> stepIds)
    {
        var layout = _layouts.TryGetValue(eventId, out var saved)
            ? EventGraphLayout.FromJson(saved.ToJson())
            : new EventGraphLayout { EventId = eventId };
        var ids = stepIds.ToList();
        layout.Prune(ids);
        for (var i = 0; i < ids.Count; i++) layout.GetOrCreate(ids[i], i);
        return layout;
    }

    public void Save(EventGraphLayout layout, IEnumerable<string> validEventIds)
    {
        _layouts[layout.EventId] = EventGraphLayout.FromJson(layout.ToJson());
        var valid = validEventIds.ToHashSet(StringComparer.Ordinal);
        foreach (var orphan in _layouts.Keys.Where(k => !valid.Contains(k)).ToList()) _layouts.Remove(orphan);
        var root = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["events"] = new JsonArray(_layouts.Values.OrderBy(x => x.EventId, StringComparer.Ordinal)
                .Select(x => (JsonNode)JsonNode.Parse(x.ToJson())!).ToArray())
        };
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
        var text = root.ToJsonString(WriteOptions) + Environment.NewLine;
        var temp = _path + ".tmp";
        File.WriteAllText(temp, text, new System.Text.UTF8Encoding(false));
        File.Copy(temp, _path, true);
        File.Delete(temp);
    }
}
