using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ContentAuthoring.Shared;
using Microsoft.Win32;
using IOPath = System.IO.Path;

namespace WorldGraphEditor;

/// <summary>
/// 可视化 WorldGraph 编辑器。节点外形／坐标系对齐 HostWorldMapPanel：
/// 站点框 128×44、深灰底、道路棕线；世界 Y 向上、屏幕 Y 向下。
/// </summary>
public partial class MainWindow : Window
{
    // —— 与 HostWorldMapPanel 对齐 ——
    const double NodeHitW = 128;
    const double NodeHitH = 44;
    const double MapPad = 48;
    const double MinViewHalf = 1.5;

    static readonly Color BgColor = Color.FromRgb(0x1E, 0x22, 0x28);
    static readonly Color RouteColor = Color.FromArgb(0xD9, 0x8C, 0x80, 0x66); // ≈ (0.55,0.5,0.4)*255
    static readonly Color NodeNormal = Color.FromArgb(0xEB, 0x38, 0x3D, 0x45); // ≈ (0.22,0.24,0.27)
    static readonly Color NodeStart = Color.FromArgb(0xF2, 0x59, 0x6B, 0x47); // ≈ focus green
    static readonly Color NodeSelected = Color.FromArgb(0xF2, 0xC9, 0xA0, 0x3C);
    static readonly Color RouteSelected = Color.FromRgb(0xE0, 0xC0, 0x60);

    ContentPackage? _package;
    DefRef? _graph;
    readonly ObservableCollection<NodeRow> _nodes = new();
    readonly ObservableCollection<RouteRow> _routes = new();

    readonly Dictionary<NodeRow, Border> _nodeVisuals = new();
    readonly Dictionary<RouteRow, Line> _routeVisuals = new();

    double _viewCx;
    double _viewCy;
    double _viewHalf = MinViewHalf;
    double _fullHalf = MinViewHalf;
    bool _viewReady;

    NodeRow? _selectedNode;
    RouteRow? _selectedRoute;
    bool _suppressProp;
    bool _linkMode;
    NodeRow? _linkFrom;

    bool _draggingNode;
    NodeRow? _dragNode;
    Point _dragLastCanvas;

    bool _panning;
    Point _panLast;
    bool _zoomUiSync;

    public MainWindow()
    {
        InitializeComponent();
        Title = "XianXia · 大世界图编辑器（节点拖动）";
        MapCanvas.SizeChanged += (_, _) =>
        {
            if (!_viewReady) FitView();
            else RebuildVisuals();
        };
        _nodes.CollectionChanged += (_, _) => RebuildVisuals();
        _routes.CollectionChanged += (_, _) => RebuildVisuals();
        TryLoadDefault();
    }

    void TryLoadDefault()
    {
        var root = PackagePaths.FindDefaultBaseGame();
        if (root != null) LoadRoot(root);
        else StatusText.Text = "未找到默认 Content/BaseGame，请点「打开包…」";
    }

    void LoadRoot(string root)
    {
        _package = PackageStore.Load(root);
        RootText.Text = root;
        GraphCombo.ItemsSource = _package.OfType("worldGraph").Select(d => d.Id).ToList();
        if (GraphCombo.Items.Count > 0) GraphCombo.SelectedIndex = 0;
        StatusText.Text = $"已加载 WorldGraph {GraphCombo.Items.Count} 个";
    }

    void OpenPackage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "选择 Content/BaseGame 包目录" };
        if (dlg.ShowDialog() == true) LoadRoot(dlg.FolderName);
    }

    void GraphCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_package == null || GraphCombo.SelectedItem is not string id) return;
        _graph = _package.Find(id);
        DetachAllRowHandlers();
        _nodes.Clear();
        _routes.Clear();
        _selectedNode = null;
        _selectedRoute = null;
        _viewReady = false;
        if (_graph == null) return;
        NameBox.Text = JsonEdit.GetString(_graph.Raw, "name");
        StartBox.Text = JsonEdit.GetString(_graph.Raw, "startNodeId");
        if (_graph.Raw["nodes"] is JsonArray nodes)
        {
            foreach (var node in nodes.OfType<JsonObject>())
            {
                var row = NodeRow.FromJson(node);
                AttachNodeHandler(row);
                _nodes.Add(row);
            }
        }

        if (_graph.Raw["routes"] is JsonArray routes)
        {
            foreach (var route in routes.OfType<JsonObject>())
            {
                var row = RouteRow.FromJson(route);
                AttachRouteHandler(row);
                _routes.Add(row);
            }
        }

        ClearInspector();
        Dispatcher.BeginInvoke(FitView);
        StatusText.Text = $"节点 {_nodes.Count} · 道路 {_routes.Count}（拖动对齐游戏大地图）";
    }

    void AttachNodeHandler(NodeRow row) => row.PropertyChanged += NodeOrRoute_PropertyChanged;
    void AttachRouteHandler(RouteRow row) => row.PropertyChanged += NodeOrRoute_PropertyChanged;

    void DetachAllRowHandlers()
    {
        foreach (var n in _nodes) n.PropertyChanged -= NodeOrRoute_PropertyChanged;
        foreach (var r in _routes) r.PropertyChanged -= NodeOrRoute_PropertyChanged;
    }

    void NodeOrRoute_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NodeRow.WorldX) or nameof(NodeRow.WorldY) or
            nameof(NodeRow.Name) or nameof(NodeRow.Id))
            UpdateNodePositions();
        else if (sender is RouteRow)
            UpdateRouteLines();
        else
            RebuildVisuals();

        if (sender == _selectedNode && !_suppressProp)
            PushNodeToInspector();
        if (sender == _selectedRoute && !_suppressProp)
            PushRouteToInspector();
    }

    void NewGraph_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null)
        {
            MessageBox.Show("请先打开包");
            return;
        }

        var id = "base:graph_new";
        ContentPathRules.EnsureTypeDir(_package.Root, "worldGraph");
        var path = IOPath.Combine(ContentPathRules.TypeDataDir(_package.Root, "worldGraph"), "graph_new.json");
        var root = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["definitions"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = id,
                    ["type"] = "worldGraph",
                    ["name"] = "新世界图",
                    ["startNodeId"] = "",
                    ["nodes"] = new JsonArray(),
                    ["routes"] = new JsonArray()
                }
            }
        };
        File.WriteAllText(
            path,
            root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) +
            Environment.NewLine,
            new System.Text.UTF8Encoding(false));
        LoadRoot(_package.Root);
        GraphCombo.SelectedItem = id;
        StatusText.Text = "已新建 " + path;
    }

    void AddNode_Click(object sender, RoutedEventArgs e)
    {
        var row = new NodeRow
        {
            Id = "base:node_new_" + (_nodes.Count + 1),
            Name = "新节点",
            Kind = "Village",
            WorldX = Math.Round(_viewCx, 2),
            WorldY = Math.Round(_viewCy, 2)
        };
        AttachNodeHandler(row);
        _nodes.Add(row);
        SelectNode(row);
        StatusText.Text = "已添加节点（可拖动）";
    }

    void LinkMode_Click(object sender, RoutedEventArgs e)
    {
        _linkMode = !_linkMode;
        _linkFrom = null;
        LinkModeBtn.Background = _linkMode
            ? new SolidColorBrush(Color.FromRgb(0x80, 0x60, 0x20))
            : null;
        StatusText.Text = _linkMode ? "连线模式：依次点两个节点" : "已退出连线模式";
    }

    void DeleteSelected_Click(object sender, RoutedEventArgs e) => DeleteSelection();

    void SetStart_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedNode == null) return;
        StartBox.Text = _selectedNode.Id;
        RebuildVisuals();
        StatusText.Text = "起点 → " + _selectedNode.Id;
    }

    void FitView_Click(object sender, RoutedEventArgs e) => FitView();
    void ResetZoom_Click(object sender, RoutedEventArgs e)
    {
        ZoomTowardCenter(MinViewHalf);
    }

    void ZoomIn_Click(object sender, RoutedEventArgs e) => ZoomByFactor(1.0 / 1.12);
    void ZoomOut_Click(object sender, RoutedEventArgs e) => ZoomByFactor(1.12);

    void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_zoomUiSync || !_viewReady) return;
        ComputeFullHalf();
        var span = Math.Max(0.01, _fullHalf - MinViewHalf);
        // 滑条 100% = 最大放大（邻站），0% = 全图 —— 与 HostWorldMapPanel zoomPct 一致
        var pct = ZoomSlider.Value / 100.0;
        ZoomTowardCenter(MinViewHalf + (1.0 - pct) * span);
    }

    void ZoomByFactor(double factor)
    {
        var cx = MapCanvas.ActualWidth * 0.5;
        var cy = MapCanvas.ActualHeight * 0.5;
        ZoomAtCanvasPoint(new Point(cx, cy), factor);
    }

    void ZoomTowardCenter(double newHalf)
    {
        ComputeFullHalf();
        _viewHalf = Math.Clamp(newHalf, MinViewHalf, Math.Max(_fullHalf, MinViewHalf));
        RebuildVisuals();
        SyncZoomUi();
    }

    void ZoomAtCanvasPoint(Point canvasPos, double factor)
    {
        ScreenToWorld(canvasPos, out var wx, out var wy);
        var before = _viewHalf;
        ComputeFullHalf();
        _viewHalf = Math.Clamp(before * factor, MinViewHalf, Math.Max(_fullHalf, MinViewHalf));
        if (before > 0.01)
        {
            var t = 1.0 - _viewHalf / before;
            _viewCx += (wx - _viewCx) * t;
            _viewCy += (wy - _viewCy) * t;
        }

        RebuildVisuals();
        SyncZoomUi();
    }

    void SyncZoomUi()
    {
        ComputeFullHalf();
        var pct = Math.Abs(_fullHalf - MinViewHalf) < 0.001
            ? 100
            : (int)Math.Round(100.0 * (1.0 - (_viewHalf - MinViewHalf) / (_fullHalf - MinViewHalf)));
        pct = Math.Clamp(pct, 0, 100);
        _zoomUiSync = true;
        if (ZoomSlider != null) ZoomSlider.Value = pct;
        if (ZoomLabel != null) ZoomLabel.Text = pct + "%";
        _zoomUiSync = false;
    }

    void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null || _graph == null)
        {
            MessageBox.Show("没有选中 worldGraph");
            return;
        }

        _graph.Raw["name"] = NameBox.Text ?? "";
        JsonEdit.SetString(_graph.Raw, "startNodeId", StartBox.Text);
        var nodes = new JsonArray();
        foreach (var row in _nodes) nodes.Add(row.ToJson());
        _graph.Raw["nodes"] = nodes;
        var routes = new JsonArray();
        foreach (var row in _routes) routes.Add(row.ToJson());
        _graph.Raw["routes"] = routes;
        try
        {
            PackageStore.SaveDefinition(_package, _graph);
            StatusText.Text = "已保存: " + IOPath.GetFileName(_graph.FilePath);
            var keep = _graph.Id;
            LoadRoot(_package.Root);
            GraphCombo.SelectedItem = keep;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBoxBase) return;
        if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            Save_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.D0 && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ResetZoom_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.D1 && Keyboard.Modifiers == ModifierKeys.Control)
        {
            FitView();
            e.Handled = true;
        }
        else if (e.Key is Key.Delete or Key.Back)
        {
            DeleteSelection();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _linkMode = false;
            _linkFrom = null;
            LinkModeBtn.Background = null;
            SelectNode(null);
            SelectRoute(null);
        }
    }

    void MapBorder_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // 与游戏大地图相同：滚轮即以鼠标为锚缩放（无需 Ctrl）
        var pos = e.GetPosition(MapCanvas);
        var factor = e.Delta < 0 ? 1.12 : 1.0 / 1.12;
        ZoomAtCanvasPoint(pos, factor);
        e.Handled = true;
    }

    void FitView()
    {
        ComputeFullHalf();
        if (_nodes.Count == 0)
        {
            _viewCx = 0;
            _viewCy = 0;
            _viewHalf = MinViewHalf;
        }
        else
        {
            double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
            foreach (var n in _nodes)
            {
                minX = Math.Min(minX, n.WorldX);
                maxX = Math.Max(maxX, n.WorldX);
                minY = Math.Min(minY, n.WorldY);
                maxY = Math.Max(maxY, n.WorldY);
            }

            _viewCx = (minX + maxX) * 0.5;
            _viewCy = (minY + maxY) * 0.5;
            _viewHalf = _fullHalf;
        }

        _viewReady = true;
        RebuildVisuals();
        SyncZoomUi();
    }

    void ComputeFullHalf()
    {
        if (_nodes.Count == 0)
        {
            _fullHalf = MinViewHalf;
            return;
        }

        double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
        foreach (var n in _nodes)
        {
            minX = Math.Min(minX, n.WorldX);
            maxX = Math.Max(maxX, n.WorldX);
            minY = Math.Min(minY, n.WorldY);
            maxY = Math.Max(maxY, n.WorldY);
        }

        var half = Math.Max((maxX - minX) * 0.5, (maxY - minY) * 0.5) + 1.5;
        _fullHalf = Math.Max(MinViewHalf, half);
    }

    double MapScale()
    {
        var innerW = Math.Max(1, MapCanvas.ActualWidth - MapPad * 2);
        var innerH = Math.Max(1, MapCanvas.ActualHeight - MapPad * 2);
        // 与游戏一致：半宽映射到视口；再乘编辑器友好系数
        var gameScale = Math.Min(innerW, innerH) / (2.0 * Math.Max(0.01, _viewHalf));
        return Math.Max(8, gameScale);
    }

    Point Project(double wx, double wy)
    {
        var scale = MapScale();
        var cx = MapCanvas.ActualWidth * 0.5;
        var cy = MapCanvas.ActualHeight * 0.5;
        return new Point(
            cx + (wx - _viewCx) * scale,
            cy - (wy - _viewCy) * scale);
    }

    void ScreenToWorld(Point gui, out double wx, out double wy)
    {
        var scale = MapScale();
        var cx = MapCanvas.ActualWidth * 0.5;
        var cy = MapCanvas.ActualHeight * 0.5;
        wx = _viewCx + (gui.X - cx) / scale;
        wy = _viewCy - (gui.Y - cy) / scale;
    }

    void RebuildVisuals()
    {
        MapCanvas.Children.Clear();
        _nodeVisuals.Clear();
        _routeVisuals.Clear();
        if (MapCanvas.ActualWidth < 10 || MapCanvas.ActualHeight < 10)
            return;

        foreach (var route in _routes)
        {
            var from = _nodes.FirstOrDefault(n => n.Id == route.FromNodeId);
            var to = _nodes.FirstOrDefault(n => n.Id == route.ToNodeId);
            if (from == null || to == null) continue;
            var a = Project(from.WorldX, from.WorldY);
            var b = Project(to.WorldX, to.WorldY);
            var line = new Line
            {
                X1 = a.X,
                Y1 = a.Y,
                X2 = b.X,
                Y2 = b.Y,
                Stroke = new SolidColorBrush(route == _selectedRoute ? RouteSelected : RouteColor),
                StrokeThickness = route == _selectedRoute ? 4 : 3,
                Cursor = Cursors.Hand
            };
            line.MouseLeftButtonDown += (_, ev) =>
            {
                SelectRoute(route);
                ev.Handled = true;
            };
            MapCanvas.Children.Add(line);
            _routeVisuals[route] = line;
        }

        var startId = StartBox.Text?.Trim() ?? "";
        foreach (var node in _nodes)
        {
            var p = Project(node.WorldX, node.WorldY);
            var isStart = string.Equals(node.Id, startId, StringComparison.Ordinal);
            var isSel = node == _selectedNode;
            var fill = isSel ? NodeSelected : isStart ? NodeStart : NodeNormal;
            var border = new Border
            {
                Width = NodeHitW,
                Height = NodeHitH,
                Background = new SolidColorBrush(fill),
                BorderBrush = isSel
                    ? new SolidColorBrush(Colors.White)
                    : new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(isSel ? 2 : 1),
                CornerRadius = new CornerRadius(2),
                Cursor = Cursors.SizeAll,
                Tag = node,
                Child = new TextBlock
                {
                    Text = (isStart ? "● " : "") + (string.IsNullOrWhiteSpace(node.Name) ? node.Id : node.Name),
                    Foreground = Brushes.White,
                    FontSize = 13,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(4, 0, 4, 0)
                }
            };
            Canvas.SetLeft(border, p.X - NodeHitW * 0.5);
            Canvas.SetTop(border, p.Y - NodeHitH * 0.5);
            border.MouseLeftButtonDown += NodeVisual_MouseLeftButtonDown;
            MapCanvas.Children.Add(border);
            _nodeVisuals[node] = border;
        }
    }

    void UpdateNodePositions()
    {
        foreach (var kv in _nodeVisuals)
        {
            var p = Project(kv.Key.WorldX, kv.Key.WorldY);
            Canvas.SetLeft(kv.Value, p.X - NodeHitW * 0.5);
            Canvas.SetTop(kv.Value, p.Y - NodeHitH * 0.5);
            if (kv.Value.Child is TextBlock tb)
            {
                var startId = StartBox.Text?.Trim() ?? "";
                var isStart = string.Equals(kv.Key.Id, startId, StringComparison.Ordinal);
                tb.Text = (isStart ? "● " : "") +
                          (string.IsNullOrWhiteSpace(kv.Key.Name) ? kv.Key.Id : kv.Key.Name);
            }
        }

        UpdateRouteLines();
    }

    void UpdateRouteLines()
    {
        foreach (var kv in _routeVisuals)
        {
            var from = _nodes.FirstOrDefault(n => n.Id == kv.Key.FromNodeId);
            var to = _nodes.FirstOrDefault(n => n.Id == kv.Key.ToNodeId);
            if (from == null || to == null) continue;
            var a = Project(from.WorldX, from.WorldY);
            var b = Project(to.WorldX, to.WorldY);
            kv.Value.X1 = a.X;
            kv.Value.Y1 = a.Y;
            kv.Value.X2 = b.X;
            kv.Value.Y2 = b.Y;
        }
    }

    void NodeVisual_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: NodeRow node }) return;
        MapCanvas.Focus();

        if (_linkMode)
        {
            if (_linkFrom == null)
            {
                _linkFrom = node;
                SelectNode(node);
                StatusText.Text = "连线：再点目标节点 ← " + node.Name;
            }
            else if (_linkFrom == node)
            {
                StatusText.Text = "请点另一个节点";
            }
            else
            {
                AddRouteBetween(_linkFrom, node);
                _linkFrom = null;
            }

            e.Handled = true;
            return;
        }

        SelectNode(node);
        SelectRoute(null);
        _draggingNode = true;
        _dragNode = node;
        _dragLastCanvas = e.GetPosition(MapCanvas);
        MapCanvas.CaptureMouse();
        e.Handled = true;
    }

    void MapCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        MapCanvas.Focus();
        if (Keyboard.Modifiers == ModifierKeys.None && e.OriginalSource == MapCanvas)
        {
            SelectNode(null);
            SelectRoute(null);
        }
    }

    void MapCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingNode)
        {
            _draggingNode = false;
            _dragNode = null;
            MapCanvas.ReleaseMouseCapture();
            if (_selectedNode != null)
                StatusText.Text =
                    $"{_selectedNode.Name}  @ ({_selectedNode.WorldX:0.##}, {_selectedNode.WorldY:0.##})";
        }
    }

    void MapCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(MapCanvas);
        if (_panning && (e.MiddleButton == MouseButtonState.Pressed ||
                         (e.LeftButton == MouseButtonState.Pressed && Keyboard.IsKeyDown(Key.Space))))
        {
            var scale = MapScale();
            var d = pos - _panLast;
            _panLast = pos;
            _viewCx -= d.X / scale;
            _viewCy += d.Y / scale;
            RebuildVisuals();
            return;
        }

        if (!_draggingNode || _dragNode == null || e.LeftButton != MouseButtonState.Pressed)
            return;

        var delta = pos - _dragLastCanvas;
        _dragLastCanvas = pos;
        var s = MapScale();
        _dragNode.WorldX = Math.Round(_dragNode.WorldX + delta.X / s, 2);
        _dragNode.WorldY = Math.Round(_dragNode.WorldY - delta.Y / s, 2);
        if (!_suppressProp)
            PushNodeToInspector();
    }

    void MapCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _linkMode = false;
        _linkFrom = null;
        LinkModeBtn.Background = null;
        StatusText.Text = "已取消连线模式";
    }

    void MapCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle ||
            (e.ChangedButton == MouseButton.Left && Keyboard.IsKeyDown(Key.Space)))
        {
            _panning = true;
            _panLast = e.GetPosition(MapCanvas);
            MapCanvas.CaptureMouse();
            e.Handled = true;
        }
    }

    void MapCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle || _panning)
        {
            _panning = false;
            MapCanvas.ReleaseMouseCapture();
        }
    }

    void AddRouteBetween(NodeRow from, NodeRow to)
    {
        if (_routes.Any(r =>
                (r.FromNodeId == from.Id && r.ToNodeId == to.Id) ||
                (!r.Directed && r.FromNodeId == to.Id && r.ToNodeId == from.Id)))
        {
            StatusText.Text = "道路已存在";
            return;
        }

        var row = new RouteRow
        {
            Id = "base:route_" + from.Id.Replace("base:node_", "") + "_" +
                 to.Id.Replace("base:node_", ""),
            FromNodeId = from.Id,
            ToNodeId = to.Id,
            Kind = "Trail",
            TravelCost = 4,
            State = "Open"
        };
        AttachRouteHandler(row);
        _routes.Add(row);
        SelectRoute(row);
        StatusText.Text = $"已连线 {from.Name} → {to.Name}";
    }

    void DeleteSelection()
    {
        if (_selectedRoute != null)
        {
            var r = _selectedRoute;
            r.PropertyChanged -= NodeOrRoute_PropertyChanged;
            _routes.Remove(r);
            SelectRoute(null);
            StatusText.Text = "已删道路";
            return;
        }

        if (_selectedNode != null)
        {
            var n = _selectedNode;
            var id = n.Id;
            for (var i = _routes.Count - 1; i >= 0; i--)
            {
                if (_routes[i].FromNodeId == id || _routes[i].ToNodeId == id)
                {
                    _routes[i].PropertyChanged -= NodeOrRoute_PropertyChanged;
                    _routes.RemoveAt(i);
                }
            }

            n.PropertyChanged -= NodeOrRoute_PropertyChanged;
            _nodes.Remove(n);
            SelectNode(null);
            StatusText.Text = "已删节点及相连道路";
        }
    }

    void SelectNode(NodeRow? node)
    {
        _selectedNode = node;
        PushNodeToInspector();
        RebuildVisuals();
    }

    void SelectRoute(RouteRow? route)
    {
        _selectedRoute = route;
        PushRouteToInspector();
        RebuildVisuals();
    }

    void ClearInspector()
    {
        _suppressProp = true;
        PropId.Text = PropName.Text = PropLocalMap.Text = PropX.Text = PropY.Text = "";
        PropOwner.Text = PropState.Text = PropTags.Text = "";
        PropKind.Text = "Village";
        RoutePropId.Text = RoutePropEnds.Text = RoutePropKind.Text = "";
        RoutePropCost.Text = RoutePropDanger.Text = RoutePropState.Text = "";
        RoutePropReq.Text = RoutePropPool.Text = "";
        RoutePropDirected.IsChecked = false;
        _suppressProp = false;
    }

    void PushNodeToInspector()
    {
        _suppressProp = true;
        if (_selectedNode == null)
        {
            PropId.Text = PropName.Text = PropLocalMap.Text = PropX.Text = PropY.Text = "";
            PropOwner.Text = PropState.Text = PropTags.Text = "";
        }
        else
        {
            var n = _selectedNode;
            PropId.Text = n.Id;
            PropName.Text = n.Name;
            PropKind.Text = n.Kind;
            PropLocalMap.Text = n.LocalMapId;
            PropX.Text = n.WorldX.ToString("0.##");
            PropY.Text = n.WorldY.ToString("0.##");
            PropOwner.Text = n.OwnerId;
            PropState.Text = n.State;
            PropTags.Text = n.Tags;
        }

        _suppressProp = false;
    }

    void PushRouteToInspector()
    {
        _suppressProp = true;
        if (_selectedRoute == null)
        {
            RoutePropId.Text = RoutePropEnds.Text = RoutePropKind.Text = "";
            RoutePropCost.Text = RoutePropDanger.Text = RoutePropState.Text = "";
            RoutePropReq.Text = RoutePropPool.Text = "";
            RoutePropDirected.IsChecked = false;
        }
        else
        {
            var r = _selectedRoute;
            RoutePropId.Text = r.Id;
            RoutePropEnds.Text = r.FromNodeId + " → " + r.ToNodeId;
            RoutePropKind.Text = r.Kind;
            RoutePropCost.Text = r.TravelCost.ToString();
            RoutePropDanger.Text = r.Danger.ToString("0.##");
            RoutePropState.Text = r.State;
            RoutePropDirected.IsChecked = r.Directed;
            RoutePropReq.Text = r.TraversalRequirements;
            RoutePropPool.Text = r.EncounterPoolId;
        }

        _suppressProp = false;
    }

    void PropNode_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppressProp || _selectedNode == null) return;
        var n = _selectedNode;
        n.Id = PropId.Text ?? "";
        n.Name = PropName.Text ?? "";
        n.LocalMapId = PropLocalMap.Text ?? "";
        if (double.TryParse(PropX.Text, out var x)) n.WorldX = x;
        if (double.TryParse(PropY.Text, out var y)) n.WorldY = y;
        n.OwnerId = PropOwner.Text ?? "";
        n.State = PropState.Text ?? "";
        n.Tags = PropTags.Text ?? "";
        UpdateNodePositions();
    }

    void PropKind_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressProp || _selectedNode == null) return;
        _selectedNode.Kind = PropKind.Text ?? "Village";
    }

    void PropRoute_Changed(object sender, RoutedEventArgs e) => ApplyRouteProps();
    void PropRoute_Changed(object sender, TextChangedEventArgs e) => ApplyRouteProps();

    void ApplyRouteProps()
    {
        if (_suppressProp || _selectedRoute == null) return;
        var r = _selectedRoute;
        r.Id = RoutePropId.Text ?? "";
        r.Kind = RoutePropKind.Text ?? "Trail";
        if (int.TryParse(RoutePropCost.Text, out var c)) r.TravelCost = c;
        if (double.TryParse(RoutePropDanger.Text, out var d)) r.Danger = d;
        r.State = RoutePropState.Text ?? "Open";
        r.Directed = RoutePropDirected.IsChecked == true;
        r.TraversalRequirements = RoutePropReq.Text ?? "";
        r.EncounterPoolId = RoutePropPool.Text ?? "";
    }
}

public sealed class NodeRow : INotifyPropertyChanged
{
    private string _id = "";
    private string _name = "";
    private string _kind = "Village";
    private string _localMapId = "";
    private double _x;
    private double _y;
    private string _owner = "";
    private string _state = "";
    private string _tags = "";

    public string Id { get => _id; set => Set(ref _id, value); }
    public string Name { get => _name; set => Set(ref _name, value); }
    public string Kind { get => _kind; set => Set(ref _kind, value); }
    public string LocalMapId { get => _localMapId; set => Set(ref _localMapId, value); }
    public double WorldX { get => _x; set => Set(ref _x, value); }
    public double WorldY { get => _y; set => Set(ref _y, value); }
    public string OwnerId { get => _owner; set => Set(ref _owner, value); }
    public string State { get => _state; set => Set(ref _state, value); }
    public string Tags { get => _tags; set => Set(ref _tags, value); }

    public static NodeRow FromJson(JsonObject o) => new()
    {
        Id = JsonEdit.GetString(o, "id"),
        Name = JsonEdit.GetString(o, "name"),
        Kind = JsonEdit.GetString(o, "kind"),
        LocalMapId = JsonEdit.GetString(o, "localMapId"),
        WorldX = o["worldX"]?.GetValue<double>() ?? 0,
        WorldY = o["worldY"]?.GetValue<double>() ?? 0,
        OwnerId = JsonEdit.GetString(o, "ownerId"),
        State = JsonEdit.GetString(o, "state"),
        Tags = JoinArr(o["tags"] as JsonArray)
    };

    public JsonObject ToJson()
    {
        var o = new JsonObject
        {
            ["id"] = Id,
            ["name"] = Name,
            ["kind"] = Kind,
            ["worldX"] = WorldX,
            ["worldY"] = WorldY
        };
        if (!string.IsNullOrWhiteSpace(LocalMapId)) o["localMapId"] = LocalMapId;
        if (!string.IsNullOrWhiteSpace(OwnerId)) o["ownerId"] = OwnerId;
        if (!string.IsNullOrWhiteSpace(State)) o["state"] = State;
        var tags = SplitCsv(Tags);
        if (tags.Count > 0) o["tags"] = tags;
        return o;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    static string JoinArr(JsonArray? arr)
    {
        if (arr == null) return "";
        return string.Join(",", arr.Select(x => x?.GetValue<string>() ?? "").Where(s => s.Length > 0));
    }

    static JsonArray SplitCsv(string text)
    {
        var arr = new JsonArray();
        foreach (var p in (text ?? "").Split(new[] { ',', '，', ';' }, StringSplitOptions.RemoveEmptyEntries))
            arr.Add(p.Trim());
        return arr;
    }
}

public sealed class RouteRow : INotifyPropertyChanged
{
    private string _id = "";
    private string _from = "";
    private string _to = "";
    private string _kind = "Trail";
    private int _cost = 4;
    private double _danger;
    private string _state = "Open";
    private bool _directed;
    private string _req = "";
    private string _pool = "";

    public string Id { get => _id; set => Set(ref _id, value); }
    public string FromNodeId { get => _from; set => Set(ref _from, value); }
    public string ToNodeId { get => _to; set => Set(ref _to, value); }
    public string Kind { get => _kind; set => Set(ref _kind, value); }
    public int TravelCost { get => _cost; set => Set(ref _cost, value); }
    public double Danger { get => _danger; set => Set(ref _danger, value); }
    public string State { get => _state; set => Set(ref _state, value); }
    public bool Directed { get => _directed; set => Set(ref _directed, value); }
    public string TraversalRequirements { get => _req; set => Set(ref _req, value); }
    public string EncounterPoolId { get => _pool; set => Set(ref _pool, value); }

    public static RouteRow FromJson(JsonObject o)
    {
        var req = "";
        if (o["traversalRequirements"] is JsonArray arr)
        {
            req = string.Join(",", arr.OfType<JsonObject>().Select(c =>
                JsonEdit.GetString(c, "kind") + ":" + JsonEdit.GetString(c, "id")));
        }

        return new RouteRow
        {
            Id = JsonEdit.GetString(o, "id"),
            FromNodeId = JsonEdit.GetString(o, "fromNodeId"),
            ToNodeId = JsonEdit.GetString(o, "toNodeId"),
            Kind = JsonEdit.GetString(o, "kind"),
            TravelCost = o["travelCost"]?.GetValue<int>() ?? 0,
            Danger = o["danger"]?.GetValue<double>() ?? 0,
            State = JsonEdit.GetString(o, "state"),
            Directed = o["directed"]?.GetValue<bool>() ?? false,
            TraversalRequirements = req,
            EncounterPoolId = JsonEdit.GetString(o, "encounterPoolId")
        };
    }

    public JsonObject ToJson()
    {
        var o = new JsonObject
        {
            ["id"] = Id,
            ["fromNodeId"] = FromNodeId,
            ["toNodeId"] = ToNodeId,
            ["kind"] = Kind,
            ["travelCost"] = TravelCost,
            ["danger"] = Danger,
            ["state"] = string.IsNullOrWhiteSpace(State) ? "Open" : State,
            ["directed"] = Directed
        };
        if (!string.IsNullOrWhiteSpace(EncounterPoolId)) o["encounterPoolId"] = EncounterPoolId;
        var reqs = new JsonArray();
        foreach (var part in (TraversalRequirements ?? "").Split(new[] { ',', '，', ';' },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var t = part.Trim();
            var idx = t.IndexOf(':');
            if (idx <= 0) continue;
            reqs.Add(new JsonObject
            {
                ["kind"] = t[..idx].Trim(),
                ["id"] = t[(idx + 1)..].Trim()
            });
        }

        if (reqs.Count > 0) o["traversalRequirements"] = reqs;
        return o;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
