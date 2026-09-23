using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Controls.Primitives;

namespace EventEditor;

public sealed record GraphSelection(string StepId, string? ChoiceId = null);

public sealed class EventFlowGraph : UserControl
{
    const double NodeWidth = 320;
    const double PortSize = 22;
    readonly ScrollViewer _scroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    readonly Canvas _canvas = new() { Width = 4200, Height = 2600, Background = new SolidColorBrush(Color.FromRgb(29, 32, 38)) };
    readonly ScaleTransform _scale = new(1, 1);
    readonly HashSet<string> _selectedSteps = new(StringComparer.Ordinal);
    readonly HashSet<string> _errorSteps = new(StringComparer.Ordinal);
    readonly Dictionary<string, Border> _nodes = new(StringComparer.Ordinal);
    readonly Dictionary<string, TextBox> _stepEditors = new(StringComparer.Ordinal);
    readonly Dictionary<string, Button> _inputPorts = new(StringComparer.Ordinal);
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
    Path? _previewPath;
    GraphConnection? _selectedConnection;
    string? _hoverTarget;
    Point _contextPoint;

    public Func<string?>? SpecificSpeakerRequested { get; set; }

    public event EventHandler? MutationStarting;
    public event EventHandler? Mutated;
    public event EventHandler? SelectionChanged;
    public event EventHandler? FocusTextRequested;
    public event Action<string>? NoticeRequested;

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
        _canvas.ContextMenu = BuildCanvasMenu();
        _canvas.ContextMenuOpening += (_, _) => _contextPoint = Mouse.GetPosition(_canvas);
    }

    public GraphSelection? Selection => _selection;

    public void FocusSelectedText()
    {
        if (_selection != null && _stepEditors.TryGetValue(_selection.StepId, out var editor))
        {
            editor.Focus(); editor.SelectAll();
        }
    }

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
        var order = new Dictionary<string, int>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        var sequence = 0;
        if (steps.ContainsKey(entry)) { ranks[entry] = 0; order[entry] = sequence++; queue.Enqueue(entry); }
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            foreach (var next in Edges(steps[id]))
                if (next.Length > 0 && steps.ContainsKey(next) && !ranks.ContainsKey(next)) { ranks[next] = ranks[id] + 1; order[next] = sequence++; queue.Enqueue(next); }
        }
        var tailRank = ranks.Count == 0 ? 0 : ranks.Values.Max() + 1;
        foreach (var id in steps.Keys.Where(id => !ranks.ContainsKey(id))) { ranks[id] = tailRank++; order[id] = sequence++; }
        foreach (var group in ranks.GroupBy(x => x.Value).OrderBy(x => x.Key))
        {
            var y = 90d;
            foreach (var item in group.OrderBy(x => order[x.Key]))
            {
                var node = _layout.GetOrCreate(item.Key);
                node.X = 100 + group.Key * 410;
                node.Y = y;
                y += EstimateNodeHeight(steps[item.Key]) + 70;
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
        _canvas.Children.Clear(); _nodes.Clear(); _stepEditors.Clear(); _inputPorts.Clear();
        if (_event == null || _layout == null) return;
        var steps = Steps().OfType<JsonObject>().ToList();
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
                    DrawEdge(new PortSource(id, S(choice, "id")),
                        new Point(from.X + NodeWidth - 23, from.Y + 126 + i * 38),
                        S(choice, "nextStepId"), positions);
                    i++;
                }
            }
            else DrawEdge(new PortSource(id, null),
                new Point(from.X + NodeWidth - 23, from.Y + 142),
                S(step, "nextStepId"), positions);
        }
    }

    void DrawEdge(PortSource source, Point from, string target, Dictionary<string, GraphNodeLayout> positions)
    {
        if (target.Length == 0 || !positions.TryGetValue(target, out var to)) return;
        var connection = new GraphConnection(source, target);
        var selected = Equals(_selectedConnection, connection);
        var path = BezierPath(from, new Point(to.X + 23, to.Y + 24),
            selected ? Brushes.Orange : new SolidColorBrush(Color.FromRgb(108, 177, 255)),
            selected ? 5 : 3, true);
        path.Tag = connection;
        path.Cursor = Cursors.Hand;
        path.ToolTip = "单击选择；Delete 或右键可断开连接";
        path.MouseEnter += (_, _) => { if (!Equals(_selectedConnection, connection)) { path.Stroke = Brushes.Orange; path.StrokeThickness = 5; } };
        path.MouseLeave += (_, _) => { if (!Equals(_selectedConnection, connection)) { path.Stroke = new SolidColorBrush(Color.FromRgb(108, 177, 255)); path.StrokeThickness = 3; } };
        path.PreviewMouseLeftButtonDown += (_, e) =>
        {
            Focus(); _selectedConnection = connection; _selection = null; _selectedSteps.Clear();
            RefreshGraph(); SelectionChanged?.Invoke(this, EventArgs.Empty); e.Handled = true;
        };
        var menu = new ContextMenu();
        var disconnect = new MenuItem { Header = "断开连接" };
        disconnect.Click += (_, _) => Disconnect(source);
        menu.Items.Add(disconnect); path.ContextMenu = menu;
        Panel.SetZIndex(path, 0); _canvas.Children.Add(path);
    }

    void DrawNode(JsonObject step)
    {
        if (_event == null || _layout == null) return;
        var id = S(step, "id");
        var pos = _layout.Find(id); if (pos == null) return;
        var selected = _selectedSteps.Contains(id);
        var border = new Border
        {
            Width = NodeWidth, MinHeight = 155, Padding = new Thickness(12), CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromRgb(48, 53, 62)),
            BorderBrush = new SolidColorBrush(_errorSteps.Contains(id) ? Color.FromRgb(235, 82, 82) : selected ? Color.FromRgb(255, 190, 70) : Color.FromRgb(92, 102, 118)),
            BorderThickness = new Thickness(selected || _errorSteps.Contains(id) ? 2 : 1), Tag = new NodeTag(id)
        };
        var panel = new StackPanel(); border.Child = panel;
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PortSize) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var input = InputPort(id); Grid.SetColumn(input, 0); header.Children.Add(input);
        var speaker = SpeakerEditor(step, id); Grid.SetColumn(speaker, 1); header.Children.Add(speaker);
        var badges = new StackPanel { Orientation = Orientation.Horizontal };
        if (S(_event, "entryStepId") == id) badges.Children.Add(Badge("入口", Color.FromRgb(48, 130, 90)));
        if (_errorSteps.Contains(id)) badges.Children.Add(Badge("错误", Color.FromRgb(175, 55, 55)));
        Grid.SetColumn(badges, 2); header.Children.Add(badges);
        panel.Children.Add(header);
        var text = new TextBox
        {
            Text = S(step, "text"), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            MinHeight = 54, MaxHeight = 110, Margin = new Thickness(0, 8, 0, 8), Padding = new Thickness(7, 5, 7, 5),
            Foreground = new SolidColorBrush(Color.FromRgb(232, 235, 240)), Background = new SolidColorBrush(Color.FromRgb(38, 42, 50)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(75, 84, 98)), VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            ToolTip = "直接编辑正文；Ctrl+Enter 提交，Enter 换行"
        };
        WireInlineText(text, id, null); panel.Children.Add(text); _stepEditors[id] = text;
        var conditions = Count(step, "conditions"); var outcomes = Count(step, "outcomes");
        if (conditions + outcomes > 0)
            panel.Children.Add(new TextBlock { Text = $"{(conditions > 0 ? "🔒 条件 " + conditions + "  " : "")}{(outcomes > 0 ? "✓ 结果 " + outcomes : "")}", Foreground = new SolidColorBrush(Color.FromRgb(185, 194, 207)), FontSize = 11 });
        var choices = step["choices"] as JsonArray;
        if (choices is { Count: > 0 })
        {
            foreach (var choice in choices.OfType<JsonObject>())
            {
                var choiceId = S(choice, "id");
                var row = new Grid { Margin = new Thickness(0, 5, 0, 0), Tag = new ChoiceTag(id, choiceId) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PortSize + 2) });
                var choiceBox = new TextBox
                {
                    Text = S(choice, "text"), MinHeight = 29, Padding = new Thickness(5, 3, 5, 3),
                    Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(42, 47, 56)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(76, 86, 102)),
                    ToolTip = "单击选择；直接编辑玩家选项"
                };
                var prefix = Count(choice, "conditions") > 0 ? "🔒" : "○";
                if (S(choice, "unavailableMode") == "hidden") prefix += " 隐藏";
                var choicePanel = new DockPanel();
                var badge = new TextBlock { Text = prefix, Foreground = new SolidColorBrush(Color.FromRgb(190, 198, 212)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) };
                DockPanel.SetDock(badge, Dock.Left); choicePanel.Children.Add(badge); choicePanel.Children.Add(choiceBox);
                WireInlineText(choiceBox, id, choiceId); Grid.SetColumn(choicePanel, 0); row.Children.Add(choicePanel);
                var port = PortButton(new PortSource(id, choiceId), S(choice, "nextStepId").Length == 0); Grid.SetColumn(port, 1); row.Children.Add(port);
                row.PreviewMouseLeftButtonDown += (_, e) =>
                {
                    if (IsInteractive(e.OriginalSource as DependencyObject)) return;
                    _selection = new GraphSelection(id, choiceId); _selectedConnection = null; _selectedSteps.Clear(); _selectedSteps.Add(id);
                    SelectionChanged?.Invoke(this, EventArgs.Empty); e.Handled = true;
                };
                panel.Children.Add(row);
            }
            var add = new Button { Content = "＋ 添加玩家选择", Margin = new Thickness(0, 9, 0, 0), Padding = new Thickness(8, 4, 8, 4), HorizontalAlignment = HorizontalAlignment.Left };
            add.Click += (_, _) => AddChoice(id); panel.Children.Add(add);
        }
        else
        {
            var row = new DockPanel { Margin = new Thickness(0, 5, 0, 0) };
            var port = PortButton(new PortSource(id, null), S(step, "nextStepId").Length == 0); DockPanel.SetDock(port, Dock.Right); row.Children.Add(port);
            row.Children.Add(new TextBlock { Text = S(step, "nextStepId").Length == 0 ? "＋ 连接下一句" : "→ 下一句", Foreground = new SolidColorBrush(Color.FromRgb(190, 200, 214)), FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            panel.Children.Add(row);
        }
        border.PreviewMouseLeftButtonDown += NodeMouseDown;
        var menu = new ContextMenu();
        var entry = new MenuItem { Header = "设为入口" }; entry.Click += (_, _) => SetEntry(id); menu.Items.Add(entry);
        var addChoice = new MenuItem { Header = "添加选项" }; addChoice.Click += (_, _) => AddChoice(id); menu.Items.Add(addChoice);
        var end = new MenuItem { Header = "此处结束事件 / 断开下一句" }; end.Click += (_, _) => Disconnect(new PortSource(id, null)); menu.Items.Add(end);
        var delete = new MenuItem { Header = "删除节点" }; delete.Click += (_, _) => { Select(id, null, false); DeleteSelected(); }; menu.Items.Add(delete);
        border.ContextMenu = menu;
        Canvas.SetLeft(border, pos.X); Canvas.SetTop(border, pos.Y); Panel.SetZIndex(border, 2);
        _canvas.Children.Add(border); _nodes[id] = border;
    }

    static Border Badge(string text, Color color) => new() { Background = new SolidColorBrush(color), CornerRadius = new CornerRadius(3), Padding = new Thickness(5, 1, 5, 1), Margin = new Thickness(4, 0, 0, 0), Child = new TextBlock { Text = text, Foreground = Brushes.White, FontSize = 10 } };
    Button PortButton(PortSource source, bool unconnected)
    {
        var button = new Button
        {
            Content = "●", Width = PortSize, Height = PortSize, FontSize = 17, Padding = new Thickness(0),
            Foreground = unconnected ? Brushes.DeepSkyBlue : new SolidColorBrush(Color.FromRgb(108, 177, 255)),
            Background = unconnected ? new SolidColorBrush(Color.FromArgb(45, 0, 180, 255)) : Brushes.Transparent,
            BorderBrush = Brushes.Transparent, Cursor = Cursors.Cross,
            ToolTip = "拖动到另一个对话框，或拖到空白处创建下一句"
        };
        button.MouseEnter += (_, _) => { button.RenderTransformOrigin = new Point(.5, .5); button.RenderTransform = new ScaleTransform(1.22, 1.22); button.Foreground = Brushes.Orange; };
        button.MouseLeave += (_, _) => { button.RenderTransform = Transform.Identity; button.Foreground = unconnected ? Brushes.DeepSkyBlue : new SolidColorBrush(Color.FromRgb(108, 177, 255)); };
        button.PreviewMouseLeftButtonDown += (_, e) =>
        {
            Focus(); _connecting = source; _selectedConnection = null;
            _pointerStart = SourcePoint(source);
            _previewPath = BezierPath(_pointerStart, _pointerStart, Brushes.Orange, 4, true);
            _previewPath.IsHitTestVisible = false;
            _previewPath.StrokeDashArray = new DoubleCollection { 5, 3 };
            Panel.SetZIndex(_previewPath, 8); _canvas.Children.Add(_previewPath); _canvas.CaptureMouse(); RefreshPortHighlights(); e.Handled = true;
        };
        var menu = new ContextMenu();
        var disconnect = new MenuItem { Header = "断开连接" };
        disconnect.Click += (_, _) => Disconnect(source);
        menu.Items.Add(disconnect); button.ContextMenu = menu;
        return button;
    }

    Button InputPort(string stepId)
    {
        var button = new Button
        {
            Content = "○", Width = PortSize, Height = PortSize, FontSize = 19, Padding = new Thickness(0),
            Foreground = new SolidColorBrush(Color.FromRgb(112, 190, 255)), Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent, IsHitTestVisible = false, ToolTip = "连接输入口"
        };
        _inputPorts[stepId] = button;
        return button;
    }

    ComboBox SpeakerEditor(JsonObject step, string stepId)
    {
        var value = S(step, "speakerRef");
        var box = new ComboBox
        {
            MinWidth = 150, MaxWidth = 210, Margin = new Thickness(5, 0, 8, 0),
            ItemsSource = new[] { "旁白", "当前玩家角色", "当前互动对象", "指定人物…" },
            SelectedIndex = value switch { "" => 0, "@actor" => 1, "@target" => 2, _ => 3 },
            ToolTip = value.Length > 0 && value is not ("@actor" or "@target") ? _speakerLabel(value) : "直接切换说话人"
        };
        box.SelectionChanged += (_, _) =>
        {
            var next = box.SelectedIndex switch
            {
                1 => "@actor",
                2 => "@target",
                3 => SpecificSpeakerRequested?.Invoke(),
                _ => ""
            };
            if (next == null) { box.SelectedIndex = value switch { "" => 0, "@actor" => 1, "@target" => 2, _ => 3 }; return; }
            var current = S(FindStep(stepId) ?? new JsonObject(), "speakerRef");
            if (current == next) return;
            MutationStarting?.Invoke(this, EventArgs.Empty);
            var target = FindStep(stepId); if (target != null) target["speakerRef"] = next;
            Mutated?.Invoke(this, EventArgs.Empty);
        };
        return box;
    }

    void WireInlineText(TextBox box, string stepId, string? choiceId)
    {
        box.GotKeyboardFocus += (_, _) =>
        {
            _selection = new GraphSelection(stepId, choiceId);
            _selectedConnection = null; _selectedSteps.Clear(); _selectedSteps.Add(stepId);
            Dispatcher.BeginInvoke(() => SelectionChanged?.Invoke(this, EventArgs.Empty));
        };
        box.LostKeyboardFocus += (_, _) => CommitInlineText(box, stepId, choiceId);
        box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                CommitInlineText(box, stepId, choiceId); Focus(); e.Handled = true;
            }
        };
    }

    void CommitInlineText(TextBox box, string stepId, string? choiceId)
    {
        var step = FindStep(stepId); if (step == null) return;
        JsonObject? target = step;
        if (choiceId != null) target = (step["choices"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(c => S(c, "id") == choiceId);
        if (target == null || S(target, "text") == box.Text) return;
        MutationStarting?.Invoke(this, EventArgs.Empty); target["text"] = box.Text; Mutated?.Invoke(this, EventArgs.Empty);
    }

    Point SourcePoint(PortSource source)
    {
        if (_layout == null) return new Point();
        var position = _layout.GetOrCreate(source.StepId);
        if (source.ChoiceId == null) return new Point(position.X + NodeWidth - 23, position.Y + 142);
        var step = FindStep(source.StepId);
        var index = (step?["choices"] as JsonArray)?.OfType<JsonObject>().ToList().FindIndex(c => S(c, "id") == source.ChoiceId) ?? 0;
        return new Point(position.X + NodeWidth - 23, position.Y + 126 + Math.Max(0, index) * 38);
    }

    static Path BezierPath(Point from, Point to, Brush stroke, double thickness, bool arrow)
    {
        var path = new Path { Stroke = stroke, StrokeThickness = thickness, Fill = Brushes.Transparent, SnapsToDevicePixels = true };
        path.Data = BezierGeometry(from, to, arrow);
        return path;
    }

    static Geometry BezierGeometry(Point from, Point to, bool arrow)
    {
        var bend = Math.Max(70, Math.Abs(to.X - from.X) * .45);
        var c1 = new Point(from.X + bend, from.Y);
        var c2 = new Point(to.X - bend, to.Y);
        var geometry = new PathGeometry();
        var curve = new PathFigure { StartPoint = from, IsClosed = false };
        curve.Segments.Add(new BezierSegment(c1, c2, to, true)); geometry.Figures.Add(curve);
        if (arrow)
        {
            var angle = Math.Atan2(to.Y - c2.Y, to.X - c2.X);
            var left = new Point(to.X - 13 * Math.Cos(angle - .48), to.Y - 13 * Math.Sin(angle - .48));
            var right = new Point(to.X - 13 * Math.Cos(angle + .48), to.Y - 13 * Math.Sin(angle + .48));
            var head = new PathFigure { StartPoint = left, IsClosed = false };
            head.Segments.Add(new LineSegment(to, true)); head.Segments.Add(new LineSegment(right, true)); geometry.Figures.Add(head);
        }
        return geometry;
    }

    void RefreshPortHighlights()
    {
        foreach (var pair in _inputPorts)
        {
            var eligible = _connecting != null && pair.Key != _connecting.StepId;
            var hovered = eligible && pair.Key == _hoverTarget;
            pair.Value.Foreground = hovered ? Brushes.White : eligible ? Brushes.Orange : new SolidColorBrush(Color.FromRgb(112, 190, 255));
            pair.Value.Background = hovered ? Brushes.OrangeRed : eligible ? new SolidColorBrush(Color.FromArgb(70, 255, 165, 0)) : Brushes.Transparent;
            pair.Value.RenderTransformOrigin = new Point(.5, .5);
            pair.Value.RenderTransform = hovered ? new ScaleTransform(1.35, 1.35) : Transform.Identity;
        }
    }

    static double EstimateNodeHeight(JsonObject step)
    {
        var choices = (step["choices"] as JsonArray)?.Count ?? 0;
        var textLines = Math.Clamp((S(step, "text").Length / 32) + 1, 2, 5);
        return 92 + textLines * 18 + (choices > 0 ? choices * 38 + 42 : 35);
    }

    ContextMenu BuildCanvasMenu()
    {
        var menu = new ContextMenu();
        foreach (var item in SpeakerMenuItems((speaker, point) => AddStepAt(point, speaker), () => _contextPoint)) menu.Items.Add(item);
        return menu;
    }

    IEnumerable<MenuItem> SpeakerMenuItems(Action<string, Point> action, Func<Point> point)
    {
        MenuItem Item(string title, string? speaker, bool recommended = false)
        {
            var item = new MenuItem { Header = title, FontWeight = recommended ? FontWeights.Bold : FontWeights.Normal, InputGestureText = recommended ? "推荐" : "" };
            item.Click += (_, _) =>
            {
                var selected = speaker == null ? SpecificSpeakerRequested?.Invoke() : speaker;
                if (selected != null) action(selected, point());
            };
            return item;
        }
        yield return Item("创建：当前玩家说话", "@actor");
        yield return Item("创建：当前互动对象说话", "@target");
        yield return Item("创建：旁白", "");
        yield return Item("创建：指定人物…", null);
    }

    void AddStepAt(Point point, string speaker)
    {
        if (_event == null || _layout == null) return;
        MutationStarting?.Invoke(this, EventArgs.Empty);
        var steps = Steps(); var id = NextId("step", steps.OfType<JsonObject>().Select(s => S(s, "id")));
        steps.Add(NewStep(id, speaker)); if (S(_event, "entryStepId").Length == 0) _event["entryStepId"] = id;
        var pos = _layout.GetOrCreate(id); pos.X = Math.Max(20, point.X); pos.Y = Math.Max(20, point.Y);
        _selection = new GraphSelection(id); _selectedSteps.Clear(); _selectedSteps.Add(id);
        Mutated?.Invoke(this, EventArgs.Empty); RefreshGraph(); SelectionChanged?.Invoke(this, EventArgs.Empty);
        Dispatcher.BeginInvoke(() => { if (_stepEditors.TryGetValue(id, out var editor)) { editor.Focus(); editor.SelectAll(); } });
    }

    void ShowCreateMenu(PortSource source, Point point)
    {
        var sourceStep = FindStep(source.StepId);
        var recommended = source.ChoiceId != null ? "@actor" : S(sourceStep ?? new JsonObject(), "speakerRef") switch { "@target" => "@actor", "@actor" => "@target", _ => "" };
        var menu = new ContextMenu { Placement = PlacementMode.RelativePoint, PlacementTarget = _canvas, HorizontalOffset = point.X, VerticalOffset = point.Y };
        foreach (var tuple in new[] { ("当前玩家说话", "@actor"), ("当前互动对象说话", "@target"), ("旁白", "") })
        {
            var speaker = tuple.Item2;
            var item = new MenuItem { Header = "创建下一句：" + tuple.Item1, FontWeight = speaker == recommended ? FontWeights.Bold : FontWeights.Normal, InputGestureText = speaker == recommended ? "推荐" : "" };
            item.Click += (_, _) => CreateConnectedStep(source, point, speaker); menu.Items.Add(item);
        }
        var specified = new MenuItem { Header = "创建下一句：指定人物…" };
        specified.Click += (_, _) => { var speaker = SpecificSpeakerRequested?.Invoke(); if (speaker != null) CreateConnectedStep(source, point, speaker); };
        menu.Items.Add(specified);
        menu.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter || menu.Items.OfType<MenuItem>().Any(x => x.IsHighlighted)) return;
            menu.IsOpen = false; CreateConnectedStep(source, point, recommended); e.Handled = true;
        };
        menu.IsOpen = true;
    }

    void CreateConnectedStep(PortSource source, Point point, string speaker)
    {
        if (_event == null || _layout == null) return;
        MutationStarting?.Invoke(this, EventArgs.Empty);
        var steps = Steps(); var id = NextId("step", steps.OfType<JsonObject>().Select(s => S(s, "id")));
        steps.Add(NewStep(id, speaker)); var pos = _layout.GetOrCreate(id); pos.X = Math.Max(20, point.X); pos.Y = Math.Max(20, point.Y);
        SetNext(source, id); _selection = new GraphSelection(id); _selectedSteps.Clear(); _selectedSteps.Add(id);
        Mutated?.Invoke(this, EventArgs.Empty); RefreshGraph(); SelectionChanged?.Invoke(this, EventArgs.Empty);
        Dispatcher.BeginInvoke(() => { if (_stepEditors.TryGetValue(id, out var editor)) editor.Focus(); });
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
        if (IsInteractive(e.OriginalSource as DependencyObject)) return;
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
        if (_previewPath != null)
        {
            _previewPath.Data = BezierGeometry(_pointerStart, p, true);
            var hit = _canvas.InputHitTest(p) as DependencyObject;
            var target = AncestorTag<NodeTag>(hit)?.StepId;
            if (_hoverTarget != target) { _hoverTarget = target; RefreshPortHighlights(); }
            return;
        }
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
            _connecting = null; _hoverTarget = null; if (_previewPath != null) _canvas.Children.Remove(_previewPath); _previewPath = null; _canvas.ReleaseMouseCapture(); RefreshPortHighlights();
            if (target == source.StepId) NoticeRequested?.Invoke("不能把节点连接到自己；本次连接未生效。");
            else if (target == null) ShowCreateMenu(source, p);
            else CompleteConnection(source, target);
            e.Handled = true; return;
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

    void CompleteConnection(PortSource source, string target)
    {
        if (_event == null || _layout == null || FindStep(target) == null) return;
        MutationStarting?.Invoke(this, EventArgs.Empty);
        SetNext(source, target);
        Mutated?.Invoke(this, EventArgs.Empty); RefreshGraph(); SelectionChanged?.Invoke(this, EventArgs.Empty);
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
        if (e.Key == Key.Delete) { if (_selectedConnection != null) { var selected = _selectedConnection; _selectedConnection = null; Disconnect(selected.Source); } else DeleteSelected(); e.Handled = true; }
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
    static bool IsInteractive(DependencyObject? obj) { while (obj != null) { if (obj is TextBoxBase or ButtonBase or ComboBox) return true; obj = VisualTreeHelper.GetParent(obj); } return false; }
    sealed record NodeTag(string StepId);
    sealed record ChoiceTag(string StepId, string ChoiceId);
    sealed record PortSource(string StepId, string? ChoiceId);
    sealed record GraphConnection(PortSource Source, string TargetStepId);
}
