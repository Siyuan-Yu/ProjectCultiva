using System.Text.Json.Nodes;

namespace EventEditor;

public sealed class EditorSession
{
    readonly Stack<Snapshot> _undo = new();
    readonly Stack<Snapshot> _redo = new();
    Snapshot _cleanBaseline;
    bool _forceDirtyUntilSaved;

    public ContentAuthoring.Shared.DefRef? Source { get; private set; }
    public JsonObject Working { get; private set; }
    public EventGraphLayout Layout { get; private set; }
    public bool IsNew => Source == null;
    public bool IsDirty { get; private set; }

    public event EventHandler? Changed;

    public EditorSession(ContentAuthoring.Shared.DefRef? source, JsonObject working, EventGraphLayout layout)
    {
        Source = source;
        Working = working;
        Layout = layout;
        _cleanBaseline = Capture();
        _forceDirtyUntilSaved = source == null;
        RecalculateDirty();
    }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Mutate(Action<JsonObject, EventGraphLayout> action)
    {
        var before = Capture();
        action(Working, Layout);
        if (before.RawJson == Working.ToJsonString() && before.LayoutJson == Layout.ToJson())
            return;
        _undo.Push(before);
        _redo.Clear();
        RecalculateDirty();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void MarkGraphMutation(Snapshot before)
    {
        if (before.RawJson == Working.ToJsonString() && before.LayoutJson == Layout.ToJson())
            return;
        _undo.Push(before);
        _redo.Clear();
        RecalculateDirty();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public Snapshot Capture() => new(Working.ToJsonString(), Layout.ToJson());

    public void Undo()
    {
        if (_undo.Count == 0) return;
        _redo.Push(Capture());
        Restore(_undo.Pop());
    }

    public void Redo()
    {
        if (_redo.Count == 0) return;
        _undo.Push(Capture());
        Restore(_redo.Pop());
    }

    void Restore(Snapshot snapshot)
    {
        Working = JsonNode.Parse(snapshot.RawJson)!.AsObject();
        Layout = EventGraphLayout.FromJson(snapshot.LayoutJson);
        RecalculateDirty();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void AcceptSaved(ContentAuthoring.Shared.DefRef source)
    {
        Source = source;
        _undo.Clear();
        _redo.Clear();
        _cleanBaseline = Capture();
        _forceDirtyUntilSaved = false;
        RecalculateDirty();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    void RecalculateDirty()
    {
        var current = Capture();
        IsDirty = _forceDirtyUntilSaved || current.RawJson != _cleanBaseline.RawJson || current.LayoutJson != _cleanBaseline.LayoutJson;
    }

    public readonly record struct Snapshot(string RawJson, string LayoutJson);
}
