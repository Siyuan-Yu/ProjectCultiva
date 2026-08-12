using System.Collections.ObjectModel;
using System.ComponentModel;
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

namespace MapEditor;

public partial class MainWindow : Window
{
    const double BaseCellPx = 10;
    const double ZoomMin = 0.25;
    const double ZoomMax = 4.0;
    const int UndoLimit = 80;

    double CellPx => BaseCellPx * _zoom;

    static readonly PaletteItem[] Palette =
    {
        new(null, "选择（点选／拖移，不放置）", 0, 0, false, Colors.Transparent),
        new("wall", "墙／岩壁", 6, 1, true, Color.FromRgb(90, 90, 100)),
        new("house", "小房子", 20, 20, true, Color.FromRgb(140, 100, 70)),
        new("rock", "岩石／棚", 4, 4, true, Color.FromRgb(110, 110, 110)),
        new("herbField", "药田", 12, 12, false, Color.FromRgb(70, 150, 90)),
        new("grainField", "麦田／农田", 16, 12, false, Color.FromRgb(180, 170, 70)),
        new("forest", "树林", 14, 12, false, Color.FromRgb(40, 110, 60)),
        new("mine", "矿洞区", 10, 8, false, Color.FromRgb(100, 90, 70)),
        new("spring", "灵泉", 8, 8, false, Color.FromRgb(80, 160, 200)),
        new("cave", "洞府区", 10, 8, false, Color.FromRgb(120, 90, 140)),
        new("road", "道路", 1, 1, false, Color.FromRgb(150, 130, 100)),
        new("roadHub", "道路枢纽", 8, 8, false, Color.FromRgb(160, 140, 120))
    };

    readonly Dictionary<string, Color> _kindColors = new(StringComparer.Ordinal);
    readonly ObservableCollection<PlacementVm> _placements = new();
    readonly List<string> _undo = new();
    readonly List<string> _redo = new();

    ContentPackage? _package;
    DefRef? _layout;
    PlacementVm? _selected;
    PlacementVm? _drag;
    bool _resizing;
    Point _dragStart;
    int _origX, _origY, _origW, _origH;
    PaletteItem? _tool;
    double _zoom = 1.0;
    bool _zoomUiSync;

    bool _panning;
    Point _panStart;
    double _panOriginX, _panOriginY;
    readonly TranslateTransform _viewPan = new();

    public MainWindow()
    {
        _zoomUiSync = true;
        InitializeComponent();
        _zoomUiSync = false;
        MapCanvas.RenderTransform = _viewPan;
        Title = "XianXia · MapEditor（格点地图）";
        foreach (var p in Palette.Where(p => p.Kind != null))
            _kindColors[p.Kind!] = p.Color;
        PaletteList.ItemsSource = Palette;
        PaletteList.SelectedIndex = 0;
        _tool = Palette[0];
        PlacementList.ItemsSource = _placements;
        SyncZoomUi();
        TryLoadDefault();
        Loaded += (_, _) => Focus();
    }

    static bool IsTyping() => Keyboard.FocusedElement is TextBoxBase;

    void SyncZoomUi()
    {
        _zoomUiSync = true;
        try
        {
            if (ZoomSlider != null) ZoomSlider.Value = _zoom * 100;
            if (ZoomLabel != null) ZoomLabel.Text = $"{_zoom * 100:0}%";
        }
        finally
        {
            _zoomUiSync = false;
        }
    }

    void SetZoom(double zoom, Point? anchorInScroll = null)
    {
        var oldZoom = _zoom;
        _zoom = Math.Clamp(zoom, ZoomMin, ZoomMax);
        if (Math.Abs(_zoom - oldZoom) < 0.0001)
        {
            SyncZoomUi();
            return;
        }

        // XAML 加载期 Slider 会触发 ValueChanged，此时 MapScroll 尚未就绪
        if (MapScroll == null)
        {
            SyncZoomUi();
            return;
        }

        Point mouseInScroll;
        if (anchorInScroll.HasValue)
            mouseInScroll = anchorInScroll.Value;
        else if (MapScroll.IsMouseOver)
            mouseInScroll = Mouse.GetPosition(MapScroll);
        else
            mouseInScroll = new Point(MapScroll.ActualWidth / 2, MapScroll.ActualHeight / 2);

        // 自由相机：视口坐标 − 平移 = 内容像素
        var contentX = mouseInScroll.X - _viewPan.X;
        var contentY = mouseInScroll.Y - _viewPan.Y;
        var cellX = contentX / (BaseCellPx * oldZoom);
        var cellY = contentY / (BaseCellPx * oldZoom);

        SyncZoomUi();
        RebuildCanvas();

        _viewPan.X = mouseInScroll.X - cellX * CellPx;
        _viewPan.Y = mouseInScroll.Y - cellY * CellPx;
        UpdateSizeHint();
        StatusText.Text = $"画布缩放 {_zoom * 100:0}%（滑条／＋－／Ctrl+滚轮）";
    }

    void ZoomIn_Click(object sender, RoutedEventArgs e) => SetZoom(_zoom * 1.25);
    void ZoomOut_Click(object sender, RoutedEventArgs e) => SetZoom(_zoom / 1.25);

    void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_zoomUiSync || !IsLoaded) return;
        SetZoom(e.NewValue / 100.0);
    }

    void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Alt 在 WPF/Windows 上常进菜单模式，不可靠；主推 Ctrl+滚轮，Shift+滚轮也行
        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        var shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        var alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
        if (!ctrl && !shift && !alt) return;

        var overMap = MapScroll.IsMouseOver;
        if (!overMap && !ctrl) return;

        var anchor = overMap
            ? Mouse.GetPosition(MapScroll)
            : new Point(MapScroll.ActualWidth / 2, MapScroll.ActualHeight / 2);
        SetZoom(_zoom * (e.Delta > 0 ? 1.1 : 1.0 / 1.1), anchor);
        e.Handled = true;
    }

    void PushUndo()
    {
        var arr = new JsonArray();
        foreach (var p in _placements) arr.Add(p.ToJson());
        _undo.Add(arr.ToJsonString());
        if (_undo.Count > UndoLimit) _undo.RemoveAt(0);
        _redo.Clear();
    }

    void RestoreSnapshot(string json)
    {
        _placements.Clear();
        if (JsonNode.Parse(json) is JsonArray arr)
        {
            foreach (var n in arr.OfType<JsonObject>())
                _placements.Add(PlacementVm.FromJson(n));
        }
        SelectPlacement(_placements.FirstOrDefault(p => p.Id == _selected?.Id));
        RebuildCanvas();
    }

    void Undo_Click(object sender, RoutedEventArgs e) => Undo();
    void Redo_Click(object sender, RoutedEventArgs e) => Redo();

    void Undo()
    {
        if (_undo.Count == 0) return;
        var cur = SnapshotNow();
        var prev = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(cur);
        RestoreSnapshot(prev);
        StatusText.Text = "已撤销";
    }

    void Redo()
    {
        if (_redo.Count == 0) return;
        var cur = SnapshotNow();
        var next = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(cur);
        RestoreSnapshot(next);
        StatusText.Text = "已重做";
    }

    string SnapshotNow()
    {
        var arr = new JsonArray();
        foreach (var p in _placements) arr.Add(p.ToJson());
        return arr.ToJsonString();
    }

    void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        var shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

        if (ctrl && e.Key == Key.S)
        {
            Save_Click(sender, e);
            e.Handled = true;
            return;
        }

        if (ctrl && e.Key == Key.Z && !shift)
        {
            if (!IsTyping()) { Undo(); e.Handled = true; }
            return;
        }

        if (ctrl && (e.Key == Key.Y || (e.Key == Key.Z && shift)))
        {
            if (!IsTyping()) { Redo(); e.Handled = true; }
            return;
        }

        if (ctrl && e.Key == Key.D)
        {
            if (!IsTyping()) { DuplicateSelected(); e.Handled = true; }
            return;
        }

        if (ctrl && e.Key == Key.D0)
        {
            ZoomReset_Click(sender, e);
            e.Handled = true;
            return;
        }

        if (ctrl && e.Key == Key.D1)
        {
            ZoomFit_Click(sender, e);
            e.Handled = true;
            return;
        }

        if (IsTyping()) return;

        if (e.Key is Key.Delete or Key.Back)
        {
            DeleteSelected();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            SwitchToSelectTool();
            SelectPlacement(null);
            StatusText.Text = "已取消选中，回到选择工具";
            e.Handled = true;
            return;
        }

        if (_selected != null && e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            var step = shift ? 5 : 1;
            PushUndo();
            switch (e.Key)
            {
                case Key.Left: _selected.X -= step; break;
                case Key.Right: _selected.X += step; break;
                case Key.Up: _selected.Y += step; break;
                case Key.Down: _selected.Y -= step; break;
            }
            ClampPlacement(_selected);
            SelectPlacement(_selected);
            e.Handled = true;
        }
    }

    void SwitchToSelectTool()
    {
        PaletteList.SelectedIndex = 0;
        _tool = Palette[0];
        MapCanvas.Cursor = Cursors.Arrow;
    }

    void ZoomReset_Click(object sender, RoutedEventArgs e)
    {
        SetZoom(1.0, new Point(MapScroll.ActualWidth / 2, MapScroll.ActualHeight / 2));
        StatusText.Text = "缩放已重置 100%";
    }

    void ZoomFit_Click(object sender, RoutedEventArgs e)
    {
        if (MapScroll.ActualWidth <= 1 || MapScroll.ActualHeight <= 1) return;
        if (!TryReadMapSize(out var gw, out var gh, out _, out _, out _, out _)) return;
        if (gw < 1 || gh < 1) return;
        var needW = gw * BaseCellPx;
        var needH = gh * BaseCellPx;
        var zx = (MapScroll.ActualWidth - 24) / needW;
        var zy = (MapScroll.ActualHeight - 24) / needH;
        _zoom = Math.Clamp(Math.Min(zx, zy), ZoomMin, ZoomMax);
        SyncZoomUi();
        RebuildCanvas();
        _viewPan.X = (MapScroll.ActualWidth - gw * CellPx) / 2;
        _viewPan.Y = (MapScroll.ActualHeight - gh * CellPx) / 2;
        UpdateSizeHint();
        StatusText.Text = $"已适应窗口（缩放 {_zoom * 100:0}%）";
    }

    void MapScroll_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var space = Keyboard.IsKeyDown(Key.Space);
        if (e.ChangedButton == MouseButton.Middle || (e.ChangedButton == MouseButton.Left && space))
        {
            BeginPan(e.GetPosition(this));
            MapScroll.CaptureMouse();
            e.Handled = true;
        }
    }

    void BeginPan(Point startInWindow)
    {
        _panning = true;
        _panStart = startInWindow;
        _panOriginX = _viewPan.X;
        _panOriginY = _viewPan.Y;
        MapScroll.Cursor = Cursors.ScrollAll;
    }

    void MapScroll_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_panning) return;
        var pos = e.GetPosition(this);
        // 自由平移：不夹紧到地图范围，可拖到地图完全离开视口
        _viewPan.X = _panOriginX + (pos.X - _panStart.X);
        _viewPan.Y = _panOriginY + (pos.Y - _panStart.Y);
        e.Handled = true;
    }

    void MapScroll_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_panning) return;
        if (e.ChangedButton is not (MouseButton.Middle or MouseButton.Left)) return;
        EndPan();
        if (MapScroll.IsMouseCaptured)
            MapScroll.ReleaseMouseCapture();
        e.Handled = true;
    }

    void MapScroll_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_panning) EndPan();
    }

    void EndPan()
    {
        _panning = false;
        MapScroll.Cursor = Cursors.Arrow;
    }

    void MapScroll_MouseMove(object sender, MouseEventArgs e)
    {
        if (_panning) return;
        if (!TryReadMapSize(out var gw, out var gh, out _, out _, out _, out _)) return;
        var pos = e.GetPosition(MapCanvas);
        var cx = (int)Math.Floor(pos.X / CellPx);
        var cyTop = (int)Math.Floor(pos.Y / CellPx);
        var cy = gh - 1 - cyTop;
        if (cx < 0 || cy < 0 || cx >= gw || cy >= gh)
            CursorText.Text = "格：—";
        else
            CursorText.Text = $"格：({cx}, {cy})  缩放 {_zoom * 100:0}%";
    }

    void TryLoadDefault()
    {
        var root = PackagePaths.FindDefaultBaseGame();
        if (root != null) LoadRoot(root);
        else StatusText.Text = "未找到 Content/BaseGame，请打开包…";
    }

    void LoadRoot(string root)
    {
        _package = PackageStore.Load(root);
        RootText.Text = root;
        MapCombo.ItemsSource = _package.OfType("mapLayout").Select(d => d.Id).ToList();
        if (MapCombo.Items.Count > 0) MapCombo.SelectedIndex = 0;
        _undo.Clear();
        _redo.Clear();
        StatusText.Text = MapCombo.Items.Count == 0
            ? "包中尚无 mapLayout，可点「新建空图」"
            : $"mapLayout {_package.OfType("mapLayout").Count()} · 包已加载";
    }

    void OpenPackage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "选择 Content/BaseGame" };
        if (dlg.ShowDialog() == true) LoadRoot(dlg.FolderName);
    }

    void MapCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_package == null || MapCombo.SelectedItem is not string id) return;
        _layout = _package.Find(id);
        if (_layout == null) return;
        WidthBox.Text = JsonEdit.GetInt(_layout.Raw, "width", 80).ToString();
        HeightBox.Text = JsonEdit.GetInt(_layout.Raw, "height", 50).ToString();
        OriginXBox.Text = JsonEdit.GetDouble(_layout.Raw, "originX", -40).ToString("0.###");
        OriginYBox.Text = JsonEdit.GetDouble(_layout.Raw, "originY", -25).ToString("0.###");
        CellSizeBox.Text = JsonEdit.GetDouble(_layout.Raw, "cellSize", 1).ToString("0.###");
        NameBox.Text = JsonEdit.GetString(_layout.Raw, "name");
        RegionIdBox.Text = JsonEdit.GetString(_layout.Raw, "worldRegionId");
        _placements.Clear();
        if (_layout.Raw["placements"] is JsonArray arr)
        {
            foreach (var n in arr.OfType<JsonObject>())
                _placements.Add(PlacementVm.FromJson(n));
        }
        _undo.Clear();
        _redo.Clear();
        RebuildCanvas();
        UpdateSizeHint();
    }

    void NewFromTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null) return;
        if (_package.Find("base:map_ch01_reference") != null)
        {
            MessageBox.Show("已存在 base:map_ch01_reference，请直接选中编辑。");
            MapCombo.SelectedItem = "base:map_ch01_reference";
            return;
        }

        var raw = new JsonObject
        {
            ["id"] = "base:map_ch01_reference",
            ["type"] = "mapLayout",
            ["name"] = "第一章参考关·格点地图",
            ["worldRegionId"] = "base:region_ch01_reference",
            ["originX"] = -40,
            ["originY"] = -25,
            ["cellSize"] = 1,
            ["width"] = 80,
            ["height"] = 50,
            ["placements"] = new JsonArray()
        };
        PackageStore.AppendDefinition(_package, "map.json", raw);
        LoadRoot(_package.Root);
        MapCombo.SelectedItem = "base:map_ch01_reference";
    }

    void PaletteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _tool = PaletteList.SelectedItem as PaletteItem ?? Palette[0];
        MapCanvas.Cursor = _tool.Kind == null ? Cursors.Arrow : Cursors.Cross;
        StatusText.Text = _tool.Kind == null
            ? "选择模式：单击选中，拖移／缩放；中键或空格+拖平移"
            : $"放置「{_tool.Label}」：空白处单击放置";
    }

    void PlacementList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PlacementList.SelectedItem is PlacementVm vm)
            SelectPlacement(vm);
    }

    void SelectPlacement(PlacementVm? vm)
    {
        _selected = vm;
        PlacementList.SelectedItem = vm;
        RebuildCanvas();
        if (vm != null)
        {
            PropId.Text = vm.Id;
            PropLabel.Text = vm.Label;
            PropBound.Text = vm.BoundLocationId;
            PropBlock.IsChecked = vm.BlocksMovement;
            PropX.Text = vm.X.ToString();
            PropY.Text = vm.Y.ToString();
            PropW.Text = vm.W.ToString();
            PropH.Text = vm.H.ToString();
        }
    }

    void ApplyProps_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        PushUndo();
        _selected.Id = PropId.Text?.Trim() ?? _selected.Id;
        _selected.Label = PropLabel.Text ?? "";
        _selected.BoundLocationId = PropBound.Text ?? "";
        _selected.BlocksMovement = PropBlock.IsChecked == true;
        if (int.TryParse(PropX.Text, out var x)) _selected.X = x;
        if (int.TryParse(PropY.Text, out var y)) _selected.Y = y;
        if (int.TryParse(PropW.Text, out var w) && w >= 1) _selected.W = w;
        if (int.TryParse(PropH.Text, out var h) && h >= 1) _selected.H = h;
        ClampPlacement(_selected);
        RebuildCanvas();
    }

    void DeleteSelected_Click(object sender, RoutedEventArgs e) => DeleteSelected();
    void Duplicate_Click(object sender, RoutedEventArgs e) => DuplicateSelected();

    void DeleteSelected()
    {
        if (_selected == null) return;
        PushUndo();
        _placements.Remove(_selected);
        SelectPlacement(null);
        StatusText.Text = "已删除选中设施";
    }

    void DuplicateSelected()
    {
        if (_selected == null) return;
        if (!TryReadMapSize(out var gw, out var gh, out _, out _, out _, out _)) return;
        PushUndo();
        var src = _selected;
        var copy = new PlacementVm
        {
            Id = $"place_{src.Kind}_{DateTime.Now:HHmmssfff}",
            Kind = src.Kind,
            Label = src.Label,
            BoundLocationId = src.BoundLocationId,
            X = src.X + 1,
            Y = src.Y + 1,
            W = src.W,
            H = src.H,
            BlocksMovement = src.BlocksMovement
        };
        ClampPlacement(copy, gw, gh);
        _placements.Add(copy);
        SelectPlacement(copy);
        StatusText.Text = "已复制选中设施";
    }

    void MapSize_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyMapSize();
            e.Handled = true;
        }
    }

    void PresetSize_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag }) return;
        var parts = tag.Split(',');
        if (parts.Length != 2) return;
        WidthBox.Text = parts[0];
        HeightBox.Text = parts[1];
        ApplyMapSize();
    }

    void ResizeMap_Click(object sender, RoutedEventArgs e) => ApplyMapSize();

    void ApplyMapSize()
    {
        if (!TryReadMapSize(out var gw, out var gh, out var ox, out var oy, out var cs, out var err))
        {
            MessageBox.Show(err, "地图尺寸无效", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        PushUndo();
        if (_layout != null)
        {
            _layout.Raw["width"] = gw;
            _layout.Raw["height"] = gh;
            _layout.Raw["originX"] = ox;
            _layout.Raw["originY"] = oy;
            _layout.Raw["cellSize"] = cs;
        }

        foreach (var p in _placements)
            ClampPlacement(p, gw, gh);

        RebuildCanvas();
        UpdateSizeHint();
        StatusText.Text = $"地图尺寸已应用：{gw}×{gh} 格。记得 Ctrl+S 保存。";
        _viewPan.X = (MapScroll.ActualWidth - gw * CellPx) / 2;
        _viewPan.Y = (MapScroll.ActualHeight - gh * CellPx) / 2;
    }

    void UpdateSizeHint()
    {
        if (!TryReadMapSize(out var gw, out var gh, out _, out _, out var cs, out _))
        {
            SizeHintText.Text = "";
            return;
        }

        SizeHintText.Text = $"当前 {gw}×{gh} 格 · cellSize={cs} · 世界约 {gw * cs:0.#}×{gh * cs:0.#} · 缩放 {_zoom * 100:0}%";
    }

    bool TryReadMapSize(out int gw, out int gh, out double ox, out double oy, out double cs, out string err)
    {
        gw = gh = 0;
        ox = oy = cs = 0;
        err = "";
        if (!int.TryParse(WidthBox.Text?.Trim(), out gw) || gw < 1)
        {
            err = "宽必须是 ≥1 的整数格数";
            return false;
        }

        if (!int.TryParse(HeightBox.Text?.Trim(), out gh) || gh < 1)
        {
            err = "高必须是 ≥1 的整数格数";
            return false;
        }

        if (gw > 2000 || gh > 2000)
        {
            err = "宽／高过大（上限 2000）";
            return false;
        }

        if (!double.TryParse(OriginXBox.Text?.Trim(), out ox) ||
            !double.TryParse(OriginYBox.Text?.Trim(), out oy) ||
            !double.TryParse(CellSizeBox.Text?.Trim(), out cs) || cs <= 0)
        {
            err = "originX／originY／cellSize 无效";
            return false;
        }

        return true;
    }

    void RebuildCanvas()
    {
        if (!int.TryParse(WidthBox.Text, out var gw) || gw < 1) gw = 80;
        if (!int.TryParse(HeightBox.Text, out var gh) || gh < 1) gh = 50;

        MapCanvas.Children.Clear();
        MapCanvas.Width = gw * CellPx;
        MapCanvas.Height = gh * CellPx;
        MapCanvas.Background = new SolidColorBrush(Color.FromRgb(232, 226, 214));

        for (var x = 0; x <= gw; x++)
        {
            MapCanvas.Children.Add(new Line
            {
                X1 = x * CellPx, Y1 = 0, X2 = x * CellPx, Y2 = gh * CellPx,
                Stroke = new SolidColorBrush(x % 10 == 0 ? Color.FromRgb(180, 170, 150) : Color.FromRgb(210, 200, 185)),
                StrokeThickness = x % 10 == 0 ? 1.2 : 0.5,
                IsHitTestVisible = false
            });
        }

        for (var y = 0; y <= gh; y++)
        {
            MapCanvas.Children.Add(new Line
            {
                X1 = 0, Y1 = y * CellPx, X2 = gw * CellPx, Y2 = y * CellPx,
                Stroke = new SolidColorBrush(y % 10 == 0 ? Color.FromRgb(180, 170, 150) : Color.FromRgb(210, 200, 185)),
                StrokeThickness = y % 10 == 0 ? 1.2 : 0.5,
                IsHitTestVisible = false
            });
        }

        foreach (var p in _placements)
        {
            if (!_kindColors.TryGetValue(p.Kind, out var c))
                c = Colors.SteelBlue;
            var fill = new SolidColorBrush(Color.FromArgb(p.BlocksMovement ? (byte)220 : (byte)140, c.R, c.G, c.B));
            var rect = new Rectangle
            {
                Width = Math.Max(1, p.W) * CellPx,
                Height = Math.Max(1, p.H) * CellPx,
                Fill = fill,
                Stroke = p == _selected ? Brushes.OrangeRed : Brushes.Black,
                StrokeThickness = p == _selected ? 2.5 : 1,
                Tag = p,
                Cursor = Cursors.SizeAll
            };
            Canvas.SetLeft(rect, p.X * CellPx);
            Canvas.SetTop(rect, (gh - p.Y - Math.Max(1, p.H)) * CellPx);
            MapCanvas.Children.Add(rect);

            var label = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(p.Label) ? p.Kind : p.Label,
                FontSize = 10,
                Foreground = Brushes.White,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(label, p.X * CellPx + 2);
            Canvas.SetTop(label, (gh - p.Y - Math.Max(1, p.H)) * CellPx + 2);
            MapCanvas.Children.Add(label);

            if (p == _selected)
            {
                var handle = new Rectangle
                {
                    Width = 10, Height = 10, Fill = Brushes.OrangeRed,
                    Tag = "resize", Cursor = Cursors.SizeNWSE
                };
                Canvas.SetLeft(handle, (p.X + Math.Max(1, p.W)) * CellPx - 5);
                Canvas.SetTop(handle, (gh - p.Y) * CellPx - 5);
                MapCanvas.Children.Add(handle);
            }
        }

        UpdateSizeHint();
    }

    void MapCanvas_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        SwitchToSelectTool();
        StatusText.Text = "右键：已切回选择工具";
        e.Handled = true;
    }

    void MapCanvas_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_panning || Keyboard.IsKeyDown(Key.Space)) return;
        if (_package == null) return;
        if (!TryReadMapSize(out var gw, out var gh, out _, out _, out _, out _)) return;

        Focus();
        MapCanvas.Focus();
        var pos = e.GetPosition(MapCanvas);
        var source = e.OriginalSource;

        if (source is Rectangle { Tag: "resize" })
        {
            if (_selected == null) return;
            PushUndo();
            _drag = _selected;
            _resizing = true;
            _dragStart = pos;
            _origX = _selected.X; _origY = _selected.Y;
            _origW = _selected.W; _origH = _selected.H;
            MapCanvas.CaptureMouse();
            e.Handled = true;
            return;
        }

        if (source is Rectangle { Tag: PlacementVm vm })
        {
            SelectPlacement(vm);
            PushUndo();
            _drag = vm;
            _resizing = false;
            _dragStart = pos;
            _origX = vm.X; _origY = vm.Y;
            _origW = vm.W; _origH = vm.H;
            MapCanvas.CaptureMouse();
            e.Handled = true;
            return;
        }

        if (_tool?.Kind != null)
        {
            PushUndo();
            TryPlaceAt(pos, gw, gh);
            e.Handled = true;
            return;
        }

        SelectPlacement(null);
        e.Handled = true;
    }

    void TryPlaceAt(Point pos, int gw, int gh)
    {
        if (_tool?.Kind == null) return;
        var kind = _tool.Kind;
        var w = Math.Max(1, _tool.W);
        var h = Math.Max(1, _tool.H);
        if (w > gw) w = gw;
        if (h > gh) h = gh;

        var cx = (int)Math.Floor(pos.X / CellPx);
        var cyTop = (int)Math.Floor(pos.Y / CellPx);
        var cy = gh - cyTop - h;
        cx = Math.Clamp(cx, 0, Math.Max(0, gw - w));
        cy = Math.Clamp(cy, 0, Math.Max(0, gh - h));

        var vm = new PlacementVm
        {
            Id = $"place_{kind}_{DateTime.Now:HHmmssfff}",
            Kind = kind,
            Label = _tool.Label,
            X = cx, Y = cy, W = w, H = h,
            BlocksMovement = _tool.Block
        };
        _placements.Add(vm);
        SelectPlacement(vm);
        StatusText.Text = $"已放置 {_tool.Label} @({cx},{cy}) {w}×{h}";
    }

    void ClampPlacement(PlacementVm p) =>
        ClampPlacement(p,
            int.TryParse(WidthBox.Text, out var gw) ? gw : 80,
            int.TryParse(HeightBox.Text, out var gh) ? gh : 50);

    static void ClampPlacement(PlacementVm p, int gw, int gh)
    {
        if (p.W > gw) p.W = gw;
        if (p.H > gh) p.H = gh;
        if (p.W < 1) p.W = 1;
        if (p.H < 1) p.H = 1;
        p.X = Math.Clamp(p.X, 0, Math.Max(0, gw - p.W));
        p.Y = Math.Clamp(p.Y, 0, Math.Max(0, gh - p.H));
    }

    void MapCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_drag == null || e.LeftButton != MouseButtonState.Pressed) return;
        if (!int.TryParse(HeightBox.Text, out var gh) || gh < 1) gh = 50;
        if (!int.TryParse(WidthBox.Text, out var gw) || gw < 1) gw = 80;
        var pos = e.GetPosition(MapCanvas);
        var dx = (int)Math.Round((pos.X - _dragStart.X) / CellPx);
        var dy = -(int)Math.Round((pos.Y - _dragStart.Y) / CellPx);

        if (_resizing)
        {
            _drag.W = Math.Max(1, _origW + dx);
            _drag.H = Math.Max(1, _origH + dy);
        }
        else
        {
            _drag.X = _origX + dx;
            _drag.Y = _origY + dy;
        }

        ClampPlacement(_drag, gw, gh);
        PropX.Text = _drag.X.ToString();
        PropY.Text = _drag.Y.ToString();
        PropW.Text = _drag.W.ToString();
        PropH.Text = _drag.H.ToString();
        RebuildCanvas();
    }

    void MapCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_drag != null)
        {
            _drag = null;
            _resizing = false;
            MapCanvas.ReleaseMouseCapture();
        }
    }

    void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null || _layout == null)
        {
            MessageBox.Show("没有选中 mapLayout");
            return;
        }

        if (!TryReadMapSize(out var w, out var h, out var ox, out var oy, out var cs, out var err))
        {
            MessageBox.Show(err, "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _layout.Raw["name"] = NameBox.Text ?? "";
        JsonEdit.SetString(_layout.Raw, "worldRegionId", RegionIdBox.Text);
        _layout.Raw["originX"] = ox;
        _layout.Raw["originY"] = oy;
        _layout.Raw["cellSize"] = cs;
        _layout.Raw["width"] = w;
        _layout.Raw["height"] = h;
        var arr = new JsonArray();
        foreach (var p in _placements)
        {
            ClampPlacement(p, w, h);
            arr.Add(p.ToJson());
        }
        _layout.Raw["placements"] = arr;

        try
        {
            PackageStore.SaveDefinition(_package, _layout);
            var keep = _layout.Id;
            LoadRoot(_package.Root);
            MapCombo.SelectedItem = keep;
            StatusText.Text = "已保存 mapLayout";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

public sealed record PaletteItem(string? Kind, string Label, int W, int H, bool Block, Color Color)
{
    public string Display => Kind == null
        ? $"• {Label}"
        : $"{Kind} — {Label} ({W}×{H})";
}

public sealed class PlacementVm : INotifyPropertyChanged
{
    string _id = "", _kind = "wall", _label = "", _bound = "";
    int _x, _y, _w = 1, _h = 1;
    bool _block;

    public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public string Kind { get => _kind; set { _kind = value; OnPropertyChanged(); } }
    public string Label { get => _label; set { _label = value; OnPropertyChanged(); } }
    public string BoundLocationId { get => _bound; set { _bound = value; OnPropertyChanged(); } }
    public int X { get => _x; set { _x = value; OnPropertyChanged(); } }
    public int Y { get => _y; set { _y = value; OnPropertyChanged(); } }
    public int W { get => _w; set { _w = value; OnPropertyChanged(); } }
    public int H { get => _h; set { _h = value; OnPropertyChanged(); } }
    public bool BlocksMovement { get => _block; set { _block = value; OnPropertyChanged(); } }

    public string ListText => $"{Kind} {Id} @({X},{Y}) {W}×{H}" + (BlocksMovement ? " [挡路]" : "");

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? n = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ListText)));
    }

    public static PlacementVm FromJson(JsonObject o) => new()
    {
        Id = JsonEdit.GetString(o, "id"),
        Kind = JsonEdit.GetString(o, "kind", "wall"),
        Label = JsonEdit.GetString(o, "label"),
        BoundLocationId = JsonEdit.GetString(o, "boundLocationId"),
        X = JsonEdit.GetInt(o, "x"),
        Y = JsonEdit.GetInt(o, "y"),
        W = Math.Max(1, JsonEdit.GetInt(o, "w", 1)),
        H = Math.Max(1, JsonEdit.GetInt(o, "h", 1)),
        BlocksMovement = JsonEdit.GetBool(o, "blocksMovement")
    };

    public JsonObject ToJson()
    {
        var o = new JsonObject
        {
            ["id"] = Id,
            ["kind"] = Kind,
            ["x"] = X,
            ["y"] = Y,
            ["w"] = W,
            ["h"] = H,
            ["blocksMovement"] = BlocksMovement
        };
        if (!string.IsNullOrWhiteSpace(Label)) o["label"] = Label;
        if (!string.IsNullOrWhiteSpace(BoundLocationId)) o["boundLocationId"] = BoundLocationId;
        return o;
    }
}
