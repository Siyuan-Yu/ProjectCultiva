using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace EventEditor;

public sealed record GraphSelection(string StepId, string? ChoiceId = null);

public sealed class EventFlowGraph : UserControl
{
    readonly ScrollViewer _scroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    readonly Canvas _canvas = new() { Width = 4200, Height = 2600, Background = new SolidColorBrush(Color.FromRgb(29, 32, 38)) };
    readonly ScaleTransform _scale = new(1, 1);
    readonly HashSet<string> _selectedSteps = new(StringComparer.Ordinal);
    readonly HashSet<string> _errorSteps = new(StringComparer.Ordinal);
    readonly Dictionary<string, Border> _nodes = new(StringComparer.Ordinal);
    JsonArray _clipboard = new();
    JsonObject? _event;
    EventGraphLayout? _layout;
    Func<string, string> _speakerLabel = x => x;
    GraphSelection? _selection;
    Point _pointerStart;
    bool _draggingNodes, _panning, _boxing;
    Dictionary<string, Point> _dragOrigins = new(StringComparer.Ordinal);
    double _panH, _panV;
    Rectangle? _selectionBox;
    PortSource? _connecting;
    Line? _previewLine;

    public event EventHandler? MutationStarting;
    public event EventHandler? Mutated;
    public event EventHandler? SelectionChanged;
    public event EventHandler? FocusTextRequested;

    public EventFlowGraph()
    {
        Focusable = true;
        _canvas.LayoutTransform = _scale;
        _scroll.Content = _canvas;
        Content = _scroll;
        PreviewKeyDown += OnKeyDown;
        _canvas.PreviewMouseLeftButtonDown += CanvasLeftDown;
        _canvas.PreviewMouseMove += CanvasMouseMove;
        _canvas.PreviewMouseLeftButtonUp += CanvasLeftUp;
        _canvas.PreviewMouseDown += CanvasMouseDown;
        _canvas.PreviewMouseUp += CanvasMouseUp;
        _canvas.PreviewMouseWheel += CanvasMouseWheel;
    }

    public GraphSelection? Selection => _selection;

    public void Load(JsonObject raw, EventGraphLayout layout, Func<string, string> speakerLabel)
    {
        _event = raw;
        _layout = layout;
        _speakerLabel = speakerLabel;
        _selection = null;
        _selectedSteps.Clear();
        RefreshGraph();
    }

    public void SetErrors(IEnumerable<string> stepIds)
    {
        _errorSteps.Clear();
        foreach (var id in stepIds) _errorSteps.Add(id);
        RefreshGraph();
    }

    public void Select(string stepId, string? choiceId = null, bool bringIntoView = true)
    {
        _selection = new GraphSelection(stepId, choiceId);
        _selectedSteps.Clear();
        _selectedSteps.Add(stepId);
        RefreshGraph();
        if (bringIntoView && _nodes.TryGetValue(stepId, out var node)) node.BringIntoView();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AddStep()
    {
        if (_event == null || _layout == null) return;
        MutationStarting?.Invoke(this, EventArgs.Empty);
        var steps = Steps();
        var id = NextId("step", steps.OfType<JsonObject>().Select(s => S(s, "id")));
        var step = NewStep(id, "");
        steps.Add(step);
        if (S(_event, "entryStepId").Length == 0) _event["entryStepId"] = id;
        var layout = _layout.GetOrCreate(id, steps.Count - 1);
        layout.X = 120 + (steps.Count - 1) * 40;
        layout.Y = 120 + (steps.Count - 1) * 35;
        _selection = new GraphSelection(id);
        _selectedSteps.Clear(); _selectedSteps.Add(id);
        Mutated?.Invoke(this, EventArgs.Empty);
        RefreshGraph();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        FocusTextRequested?.Invoke(this, EventArgs.Empty);
    }

    public void AddChoice(string stepId)
    {
        var step = FindStep(stepId);
        if (step == null) return;
        MutationStarting?.Invoke(this, EventArgs.Empty);
        step.Remove("nextStepId");
        var choices = step["choices"] as JsonArray ?? new JsonArray();
        step["choices"] = choices;
        var allIds = Steps().OfType<JsonObject>().SelectMany(s => (s["choices"] as JsonArray ?? new JsonArray()).OfType<JsonObject>()).Select(c => S(c, "id"));
        var id = NextId("choice", allIds);
        choices.Add(new JsonObject { ["id"] = id, ["text"] = "新选项", ["conditions"] = new JsonArray(), ["outcomes"] = new JsonArray(), ["nextStepId"] = "", ["unavailableMode"] = "disabled", ["requirementText"] = "" });
        _selection = new GraphSelection(stepId, id);
        Mutated?.Invoke(this, EventArgs.Empty);
        RefreshGraph();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteChoice(string stepId, string choiceId)
    {
        var step = FindStep(stepId);
        if (step?["choices"] is not JsonArray choices) return;
        var choice = choices.OfType<JsonObject>().FirstOrDefault(c => S(c, "id") == choiceId);
        if (choice == null) return;
        MutationStarting?.Invoke(this, EventArgs.Empty);
        choices.Remove(choice);
        _selection = new GraphSelection(stepId);
        _selectedSteps.Clear(); _selectedSteps.Add(stepId);
        Mutated?.Invoke(this, EventArgs.Empty);
        RefreshGraph(); SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetEntry(string stepId)
    {
        if (_event == null || FindStep(stepId) == null) return;
        MutationStarting?.Invoke(this, EventArgs.Empty);
        _event["entryStepId"] = stepId;
        Mutated?.Invoke(this, EventArgs.Empty);
        RefreshGraph();
    }

    public void AutoLayout()
    {
        if (_event == null || _layout == null) return;
        MutationStarting?.Invoke(this, EventArgs.Empty);
        var steps = Steps().OfType<JsonObject>().ToDictionary(s => S(s, "id"), StringComparer.Ordinal);
        var entry = S(_event, "entryStepId");
        var ranks = new Dictionary<string, int>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        if (steps.ContainsKey(entry)) { ranks[entry] = 0; queue.Enqueue(entry); }
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            foreach (var next in Edges(steps[id]))
                if (next.Length > 0 && steps.ContainsKey(next) && !ranks.ContainsKey(next)) { ranks[next] = ranks[id] + 1; queue.Enqueue(next); }
        }
        var tailRank = ranks.Count == 0 ? 0 : ranks.Values.Max() + 1;
        foreach (var id in steps.Keys.Where(id => !ranks.ContainsKey(id))) ranks[id] = tailRank++;
        foreach (var group in ranks.GroupBy(x => x.Value).OrderBy(x => x.Key))
        {
            var row = 0;
            foreach (var item in group.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var node = _layout.GetOrCreate(item.Key);
                node.X = 100 + group.Key * 330;
                node.Y = 90 + row++ * 230;
            }
        }
        Mutated?.Invoke(this, EventArgs.Empty);
        RefreshGraph();
    }

    public void ZoomToFit()
    {
        if (_layout == null || _layout.Nodes.Count == 0) return;
        var maxX = _layout.Nodes.Max(n => n.X) + 300;
        var maxY = _layout.Nodes.Max(n => n.Y) + 260;
        var scale = Math.Clamp(Math.Min((_scroll.ViewportWidth - 40) / maxX, (_scroll.ViewportHeight - 40) / maxY), .35, 1.4);
        _scale.ScaleX = _scale.ScaleY = double.IsFinite(scale) ? scale : 1;
        _scroll.ScrollToHorizontalOffset(0);
        _scroll.ScrollToVerticalOffset(0);
    }

    void RefreshGraph()
    {
        _canvas.Children.Clear(); _nodes.Clear();
        if (_event == null || _layout == null) return;
        var steps = Steps().OfType<JsonObject>().ToList();
        _layout.Prune(steps.Select(s => S(s, "id")));
        for (var i = 0; i < steps.Count; i++) _layout.GetOrCreate(S(steps[i], "id"), i);
        DrawConnections(steps);
        foreach (var step in steps) DrawNode(step);
    }

    void DrawConnections(List<JsonObject> steps)
    {
        if (_layout == null) return;
        var positions = _layout.Nodes.ToDictionary(n => n.StepId, StringComparer.Ordinal);
        foreach (var step in steps)
        {
            var id = S(step, "id");
            if (!positions.TryGetValue(id, out var from)) continue;
            var choices = step["choices"] as JsonArray;
            if (choices is { Count: > 0 })
            {
                var i = 0;
                foreach (var choice in choices.OfType<JsonObject>())
                {
                    DrawEdge(from.X + 260, from.Y + 92 + i * 32, S(choice, "nextStepId"), positions);
                    i++;
                }
            }
            else DrawEdge(from.X + 260, from.Y + 118, S(step, "nextStepId"), positions);
        }
    }

    void DrawEdge(double x, double y, string target, Dictionary<string, GraphNodeLayout> positions)
    {
        if (target.Length == 0 || !positions.TryGetValue(target, out var to)) return;
        var line = new Line { X1 = x, Y1 = y, X2 = to.X, Y2 = to.Y + 55, Stroke = new SolidColorBrush(Color.FromRgb(108, 177, 255)), StrokeThickness = 2 };
        Panel.SetZIndex(line, 0); _canvas.Children.Add(line);
    }

    void DrawNode(JsonObject step)
    {
        if (_event == null || _layout == null) return;
        var id = S(step, "id");
        var pos = _layout.GetOrCreate(id);
        var selected = _selectedSteps.Contains(id);
        var border = new Border
        {
            Width = 260, MinHeight = 135, Padding = new Thickness(10), CornerRadius = new CornerRadius(7),
            Background = new SolidColorBrush(Color.FromRgb(48, 53, 62)),
            BorderBrush = new SolidColorBrush(_errorSteps.Contains(id) ? Color.FromRgb(235, 82, 82) : selected ? Color.FromRgb(255, 190, 70) : Color.FromRgb(92, 102, 118)),
            BorderThickness = new Thickness(selected || _errorSteps.Contains(id) ? 2 : 1), Tag = new NodeTag(id)
        };
        var panel = new StackPanel(); border.Child = panel;
        var header = new DockPanel { LastChildFill = true };
        var badges = new StackPanel { Orientation = Orientation.Horizontal };
        if (S(_event, "entryStepId") == id) badges.Children.Add(Badge("入口", Color.FromRgb(48, 130, 90)));
        if (_errorSteps.Contains(id)) badges.Children.Add(Badge("错误", Color.FromRgb(175, 55, 55)));
        DockPanel.SetDock(badges, Dock.Right); header.Children.Add(badges);
        header.Children.Add(new TextBlock { Text = _speakerLabel(S(step, "speakerRef")), Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        panel.Children.Add(header);
        panel.Children.Add(new TextBlock { Text = Preview(S(step, "text"), 105), Foreground = new SolidColorBrush(Color.FromRgb(221, 225, 232)), TextWrapping = TextWrapping.Wrap, MaxHeight = 58, Margin = new Thickness(0, 8, 0, 8) });
        var conditions = Count(step, "conditions"); var outcomes = Count(step, "outcomes");
        if (conditions + outcomes > 0)
            panel.Children.Add(new TextBlock { Text = $"{(conditions > 0 ? "🔒 条件 " + conditions + "  " : "")}{(outcomes > 0 ? "✓ 结果 " + outcomes : "")}", Foreground = new SolidColorBrush(Color.FromRgb(185, 194, 207)), FontSize = 11 });
        var choices = step["choices"] as JsonArray;
        if (choices is { Count: > 0 })
        {
            foreach (var choice in choices.OfType<JsonObject>())
            {
                var row = new Grid { Margin = new Thickness(0, 4, 0, 0), Tag = new ChoiceTag(id, S(choice, "id")) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var prefix = Count(choice, "conditions") > 0 ? "🔒 " : "○ ";
                var text = new Button { Content = prefix + Preview(S(choice, "text"), 34), HorizontalContentAlignment = HorizontalAlignment.Left, Background = Brushes.Transparent, Foreground = Brushes.White, BorderThickness = new Thickness(0), Padding = new Thickness(2) };
                text.Click += (_, _) => Select(id, S(choice, "id"), false);
                row.Children.Add(text);
                var port = PortButton(new PortSource(id, S(choice, "id"))); Grid.SetColumn(port, 1); row.Children.Add(port);
                panel.Children.Add(row);
            }
        }
        else
        {
            var row = new DockPanel { Margin = new Thickness(0, 5, 0, 0) };
            var port = PortButton(new PortSource(id, null)); DockPanel.SetDock(port, Dock.Right); row.Children.Add(port);
            row.Children.Add(new TextBlock { Text = S(step, "nextStepId").Length == 0 ? "事件结束" : "下一句", Foreground = new SolidColorBrush(Color.FromRgb(160, 170, 185)), VerticalAlignment = VerticalAlignment.Center });
            panel.Children.Add(row);
        }
        border.PreviewMouseLeftButtonDown += NodeMouseDown;
        var menu = new ContextMenu();
        var entry = new MenuItem { Header = "设为入口" }; entry.Click += (_, _) => SetEntry(id); menu.Items.Add(entry);
        var addChoice = new MenuItem { Header = "添加选项" }; addChoice.Click += (_, _) => AddChoice(id); menu.Items.Add(addChoice);
        var delete = new MenuItem { Header = "删除节点" }; delete.Click += (_, _) => { Select(id, null, false); DeleteSelected(); }; menu.Items.Add(delete);
        border.ContextMenu = menu;
        Canvas.SetLeft(border, pos.X); Canvas.SetTop(border, pos.Y); Panel.SetZIndex(border, 2);
        _canvas.Children.Add(border); _nodes[id] = border;
    }

    static Border Badge(string text, Color color) => new() { Background = new SolidColorBrush(color), CornerRadius = new CornerRadius(3), Padding = new Thickness(5, 1, 5, 1), Margin = new Thickness(4, 0, 0, 0), Child = new TextBlock { Text = text, Foreground = Brushes.White, FontSize = 10 } };
    Button PortButton(PortSource source)
    {
        var button = new Button { Content = "●", Foreground = new SolidColorBrush(Color.FromRgb(108, 177, 255)), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(5, 0, 2, 0), ToolTip = "拖到节点或空白处创建连接" };
        button.PreviewMouseLeftButtonDown += (_, e) =>
        {
            Focus(); _connecting = source; _pointerStart = e.GetPosition(_canvas);
            _previewLine = new Line { X1 = _pointerStart.X, Y1 = _pointerStart.Y, X2 = _pointerStart.X, Y2 = _pointerStart.Y, Stroke = Brushes.Orange, StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 4, 3 } };
            _canvas.Children.Add(_previewLine); _canvas.CaptureMouse(); e.Handled = true;
        };
        var menu = new ContextMenu();
        var disconnect = new MenuItem { Header = "断开连接" };
        disconnect.Click += (_, _) => Disconnect(source);
        menu.Items.Add(disconnect); button.ContextMenu = menu;
        return button;
    }

    void Disconnect(PortSource source)
    {
        var step = FindStep(source.StepId); if (step == null) return;
        var current = source.ChoiceId == null ? S(step, "nextStepId") : (step["choices"] as JsonArray)?.OfType<JsonObject>().Where(c => S(c, "id") == source.ChoiceId).Select(c => S(c, "nextStepId")).FirstOrDefault() ?? "";
        if (current.Length == 0) return;
        MutationStarting?.Invoke(this, EventArgs.Empty); SetNext(source, ""); Mutated?.Invoke(this, EventArgs.Empty); RefreshGraph();
    }

    void NodeMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_connecting != null || sender is not Border border || border.Tag is not NodeTag tag) return;
        if (e.ClickCount == 2) { Select(tag.StepId, null, false); FocusTextRequested?.Invoke(this, EventArgs.Empty); e.Handled = true; return; }
        Focus();
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            if (!_selectedSteps.Add(tag.StepId)) _selectedSteps.Remove(tag.StepId);
        }
        else if (!_selectedSteps.Contains(tag.StepId)) { _selectedSteps.Clear(); _selectedSteps.Add(tag.StepId); }
        _selection = new GraphSelection(tag.StepId);
        MutationStarting?.Invoke(this, EventArgs.Empty);
        _dragOrigins = _selectedSteps.ToDictionary(id => id, id => { var p = _layout!.GetOrCreate(id); return new Point(p.X, p.Y); }, StringComparer.Ordinal);
        _pointerStart = e.GetPosition(_canvas); _draggingNodes = true; _canvas.CaptureMouse();
        SelectionChanged?.Invoke(this, EventArgs.Empty); RefreshGraph(); e.Handled = true;
    }

    void CanvasLeftDown(object sender, MouseButtonEventArgs e)
    {
        if (_connecting != null || e.OriginalSource != _canvas) return;
        Focus(); _pointerStart = e.GetPosition(_canvas); _boxing = true;
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) _selectedSteps.Clear();
        _selectionBox = new Rectangle { Stroke = Brushes.DodgerBlue, StrokeThickness = 1, Fill = new SolidColorBrush(Color.FromArgb(40, 30, 144, 255)) };
        Canvas.SetLeft(_selectionBox, _pointerStart.X); Canvas.SetTop(_selectionBox, _pointerStart.Y); Panel.SetZIndex(_selectionBox, 5); _canvas.Children.Add(_selectionBox); _canvas.CaptureMouse();
    }

    void CanvasMouseMove(object sender, MouseEventArgs e)
    {
        var p = e.GetPosition(_canvas);
        if (_previewLine != null) { _previewLine.X2 = p.X; _previewLine.Y2 = p.Y; return; }
        if (_draggingNodes && _layout != null)
        {
            var delta = p - _pointerStart;
            foreach (var pair in _dragOrigins) { var pos = _layout.GetOrCreate(pair.Key); pos.X = Math.Max(0, pair.Value.X + delta.X); pos.Y = Math.Max(0, pair.Value.Y + delta.Y); }
            foreach (var id in _selectedSteps) if (_nodes.TryGetValue(id, out var node)) { var pos = _layout.GetOrCreate(id); Canvas.SetLeft(node, pos.X); Canvas.SetTop(node, pos.Y); }
            return;
        }
        if (_boxing && _selectionBox != null)
        {
            var x = Math.Min(_pointerStart.X, p.X); var y = Math.Min(_pointerStart.Y, p.Y);
            Canvas.SetLeft(_selectionBox, x); Canvas.SetTop(_selectionBox, y); _selectionBox.Width = Math.Abs(p.X - _pointerStart.X); _selectionBox.Height = Math.Abs(p.Y - _pointerStart.Y);
        }
    }

    void CanvasLeftUp(object sender, MouseButtonEventArgs e)
    {
        var p = e.GetPosition(_canvas);
        if (_connecting is PortSource source)
        {
            var hit = _canvas.InputHitTest(p) as DependencyObject;
            var target = AncestorTag<NodeTag>(hit)?.StepId;
            CompleteConnection(source, target, p);
            _connecting = null; if (_previewLine != null) _canvas.Children.Remove(_previewLine); _previewLine = null; _canvas.ReleaseMouseCapture(); e.Handled = true; return;
        }
        if (_draggingNodes)
        {
            _draggingNodes = false; _canvas.ReleaseMouseCapture(); Mutated?.Invoke(this, EventArgs.Empty); RefreshGraph(); e.Handled = true; return;
        }
        if (_boxing && _selectionBox != null && _layout != null)
        {
            var rect = new Rect(Canvas.GetLeft(_selectionBox), Canvas.GetTop(_selectionBox), _selectionBox.Width, _selectionBox.Height);
            foreach (var node in _layout.Nodes) if (rect.IntersectsWith(new Rect(node.X, node.Y, 260, 150))) _selectedSteps.Add(node.StepId);
            _canvas.Children.Remove(_selectionBox); _selectionBox = null; _boxing = false; _canvas.ReleaseMouseCapture();
            var first = _selectedSteps.FirstOrDefault(); _selection = first == null ? null : new GraphSelection(first); SelectionChanged?.Invoke(this, EventArgs.Empty); RefreshGraph();
        }
    }

    void CompleteConnection(PortSource source, string? target, Point drop)
    {
        if (_event == null || _layout == null) return;
        MutationStarting?.Invoke(this, EventArgs.Empty);
        if (string.IsNullOrEmpty(target))
        {
            var steps = Steps(); var id = NextId("step", steps.OfType<JsonObject>().Select(s => S(s, "id")));
            var sourceStep = FindStep(source.StepId); var suggested = source.ChoiceId != null ? "@actor" : S(sourceStep ?? new JsonObject(), "speakerRef") switch { "@target" => "@actor", "@actor" => "@target", _ => "" };
            steps.Add(NewStep(id, suggested)); target = id;
            var pos = _layout.GetOrCreate(id); pos.X = Math.Max(20, drop.X); pos.Y = Math.Max(20, drop.Y);
            _selection = new GraphSelection(id); _selectedSteps.Clear(); _selectedSteps.Add(id);
        }
        SetNext(source, target!);
        Mutated?.Invoke(this, EventArgs.Empty); RefreshGraph(); SelectionChanged?.Invoke(this, EventArgs.Empty);
        if (_selection?.StepId == target && FindStep(target) != null) FocusTextRequested?.Invoke(this, EventArgs.Empty);
    }

    void SetNext(PortSource source, string target)
    {
        var step = FindStep(source.StepId); if (step == null) return;
        if (source.ChoiceId == null) step["nextStepId"] = target;
        else if (step["choices"] is JsonArray choices)
            foreach (var choice in choices.OfType<JsonObject>()) if (S(choice, "id") == source.ChoiceId) choice["nextStepId"] = target;
    }

    void CanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle) return;
        _panning = true; _pointerStart = e.GetPosition(this); _panH = _scroll.HorizontalOffset; _panV = _scroll.VerticalOffset; _canvas.CaptureMouse(); e.Handled = true;
    }
    void CanvasMouseUp(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Middle) { _panning = false; _canvas.ReleaseMouseCapture(); e.Handled = true; } }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e); if (!_panning) return; var p = e.GetPosition(this); _scroll.ScrollToHorizontalOffset(_panH - (p.X - _pointerStart.X)); _scroll.ScrollToVerticalOffset(_panV - (p.Y - _pointerStart.Y));
    }
    void CanvasMouseWheel(object sender, MouseWheelEventArgs e) { var factor = e.Delta > 0 ? 1.1 : .9; var next = Math.Clamp(_scale.ScaleX * factor, .3, 2.2); _scale.ScaleX = _scale.ScaleY = next; e.Handled = true; }

    void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete) { DeleteSelected(); e.Handled = true; }
        else if (e.Key == Key.C && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { CopySelected(); e.Handled = true; }
        else if (e.Key == Key.V && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { Paste(); e.Handled = true; }
    }
    void CopySelected() { _clipboard = new JsonArray(Steps().OfType<JsonObject>().Where(s => _selectedSteps.Contains(S(s, "id"))).Select(s => s.DeepClone()).ToArray()); }
    void Paste()
    {
        if (_event == null || _layout == null || _clipboard.Count == 0) return;
        MutationStarting?.Invoke(this, EventArgs.Empty);
        var steps = Steps(); var existing = steps.OfType<JsonObject>().Select(s => S(s, "id")).ToHashSet(StringComparer.Ordinal); var map = new Dictionary<string, string>(StringComparer.Ordinal); var clones = _clipboard.OfType<JsonObject>().Select(x => (JsonObject)x.DeepClone()).ToList();
        foreach (var clone in clones) { var old = S(clone, "id"); var next = NextId("step", existing); existing.Add(next); map[old] = next; clone["id"] = next; }
        foreach (var clone in clones)
        {
            RemapNext(clone, map); if (clone["choices"] is JsonArray choices) foreach (var choice in choices.OfType<JsonObject>()) { choice["id"] = NextId("choice", Steps().OfType<JsonObject>().SelectMany(s => (s["choices"] as JsonArray ?? new JsonArray()).OfType<JsonObject>()).Select(c => S(c, "id"))); RemapNext(choice, map); }
            steps.Add(clone); var pos = _layout.GetOrCreate(S(clone, "id")); pos.X = 160 + steps.Count * 20; pos.Y = 160 + steps.Count * 20;
        }
        _selectedSteps.Clear(); foreach (var clone in clones) _selectedSteps.Add(S(clone, "id")); _selection = new GraphSelection(S(clones[0], "id")); Mutated?.Invoke(this, EventArgs.Empty); RefreshGraph(); SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
    static void RemapNext(JsonObject obj, Dictionary<string, string> map) { var next = S(obj, "nextStepId"); if (map.TryGetValue(next, out var mapped)) obj["nextStepId"] = mapped; else obj["nextStepId"] = ""; }
    void DeleteSelected()
    {
        if (_event == null || _layout == null || _selectedSteps.Count == 0) return;
        MutationStarting?.Invoke(this, EventArgs.Empty);
        var steps = Steps(); foreach (var step in steps.OfType<JsonObject>().Where(s => _selectedSteps.Contains(S(s, "id"))).ToList()) steps.Remove(step);
        foreach (var step in steps.OfType<JsonObject>())
        {
            if (_selectedSteps.Contains(S(step, "nextStepId"))) step["nextStepId"] = "";
            if (step["choices"] is JsonArray choices) foreach (var choice in choices.OfType<JsonObject>()) if (_selectedSteps.Contains(S(choice, "nextStepId"))) choice["nextStepId"] = "";
        }
        if (_selectedSteps.Contains(S(_event, "entryStepId"))) _event["entryStepId"] = steps.OfType<JsonObject>().Select(s => S(s, "id")).FirstOrDefault() ?? "";
        _layout.Prune(steps.OfType<JsonObject>().Select(s => S(s, "id"))); _selectedSteps.Clear(); _selection = null; Mutated?.Invoke(this, EventArgs.Empty); RefreshGraph(); SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    JsonArray Steps() { if (_event?["steps"] is not JsonArray arr) { arr = new JsonArray(); if (_event != null) _event["steps"] = arr; } return arr; }
    JsonObject? FindStep(string id) => Steps().OfType<JsonObject>().FirstOrDefault(s => S(s, "id") == id);
    static JsonObject NewStep(string id, string speaker) => new() { ["id"] = id, ["speakerRef"] = speaker, ["text"] = "", ["outcomes"] = new JsonArray(), ["choices"] = new JsonArray() };
    static string S(JsonObject obj, string key) => obj[key]?.GetValue<string>() ?? "";
    static int Count(JsonObject obj, string key) => obj[key] is JsonArray a ? a.Count : 0;
    static string Preview(string text, int max) { var flat = text.Replace('\r', ' ').Replace('\n', ' ').Trim(); return flat.Length > max ? flat[..max] + "…" : flat; }
    static string NextId(string prefix, IEnumerable<string> used) { var set = used.ToHashSet(StringComparer.Ordinal); for (var i = 1; ; i++) { var id = $"{prefix}_{i:000}"; if (!set.Contains(id)) return id; } }
    static IEnumerable<string> Edges(JsonObject step) => step["choices"] is JsonArray { Count: > 0 } choices ? choices.OfType<JsonObject>().Select(c => S(c, "nextStepId")) : new[] { S(step, "nextStepId") };
    static T? AncestorTag<T>(DependencyObject? obj) where T : class { while (obj != null) { if (obj is FrameworkElement e && e.Tag is T tag) return tag; obj = VisualTreeHelper.GetParent(obj); } return null; }
    sealed record NodeTag(string StepId);
    sealed record ChoiceTag(string StepId, string ChoiceId);
    sealed record PortSource(string StepId, string? ChoiceId);
}
