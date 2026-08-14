using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using IOPath = System.IO.Path;
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

    static readonly PaletteItem SelectTool =
        new(null, "选择（点选／拖移，不放置）", 0, 0, false, Colors.Transparent);

        /// <summary>第 1 页：地表／物件／建筑（会进游戏表现或交互）。
        /// 挡路约定：大建筑／墙／岩默认 Block=true；树／矿／蒲团等装饰默认 false，需在属性里手勾。</summary>
        static readonly PaletteItem[] PaletteObjects =
        {
            SelectTool,
            new("herbField", "药田（可耕作）", 12, 12, false, Color.FromRgb(70, 150, 90)),
            new("grainField", "农田（可耕作）", 16, 12, false, Color.FromRgb(180, 170, 70)),
            new("road", "道路（纯贴图）", 1, 1, false, Color.FromRgb(150, 130, 100)),
            new("wall", "墙 1×n（默认挡路）", 6, 1, true, Color.FromRgb(90, 90, 100)),
            new("treeS", "小树 1×1（装饰·手勾挡路）", 1, 1, false, Color.FromRgb(35, 120, 55)),
            new("treeM", "中树 2×2（装饰·手勾挡路）", 2, 2, false, Color.FromRgb(30, 100, 45)),
            new("treeL", "大树 3×3（装饰·手勾挡路）", 3, 3, false, Color.FromRgb(25, 85, 40)),
            new("ore", "矿石 2×2（可采·手勾挡路）", 2, 2, false, Color.FromRgb(140, 120, 80)),
            new("cushion", "蒲团 1×1（可修炼）", 1, 1, false, Color.FromRgb(120, 100, 170)),
            new("rock", "岩石／棚（默认挡路）", 4, 4, true, Color.FromRgb(110, 110, 110)),
            new("cave", "洞府区（默认挡路）", 10, 8, true, Color.FromRgb(120, 90, 140)),
            new("controlCore", "主管府（城镇控制核心·默认挡路）", 8, 8, true, Color.FromRgb(160, 140, 120)),
            new("rallyPoint", "集合点 2×2", 2, 2, false, Color.FromRgb(220, 140, 50))
        };

    /// <summary>第 2 页：分区标记。住房区须填 boundLocationId，并在 WorkArea／人物里配休息归属。</summary>
    static readonly PaletteItem[] PaletteZones =
    {
        SelectTool,
        new("zoneHerb", "药田区", 12, 12, false, Color.FromArgb(90, 70, 180, 100)),
        new("zoneGrain", "农田区", 16, 12, false, Color.FromArgb(90, 200, 180, 60)),
        new("zoneHousing", "住房区（须绑地点＝休息落点）", 20, 20, false, Color.FromArgb(80, 180, 140, 100)),
        new("zoneForest", "林地区", 14, 12, false, Color.FromArgb(90, 40, 130, 70)),
        new("zoneMine", "矿区", 10, 8, false, Color.FromArgb(90, 130, 110, 80)),
        new("zoneSpring", "灵泉区", 8, 8, false, Color.FromArgb(90, 80, 170, 210))
    };

    static IEnumerable<PaletteItem> AllPaletteItems()
    {
        foreach (var p in PaletteObjects)
            yield return p;
        foreach (var p in PaletteZones)
        {
            if (p.Kind != null)
                yield return p;
        }
    }

    readonly Dictionary<string, Color> _kindColors = new(StringComparer.Ordinal);
    readonly ObservableCollection<PlacementVm> _placements = new();
    readonly List<string> _undo = new();
    readonly List<string> _redo = new();

    ContentPackage? _package;
    DefRef? _layout;
    PlacementVm? _selected;
    PlacementVm? _drag;
    bool _resizing;
    string? _resizeEdge; // N/S/E/W/NE/NW/SE/SW
    Point _dragStart;
    int _origX, _origY, _origW, _origH;
    PaletteItem? _tool;
    double _zoom = 1.0;
    bool _zoomUiSync;
    bool _paletteSync;

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
        foreach (var p in AllPaletteItems().Where(p => p.Kind != null))
            _kindColors[p.Kind!] = p.Color;
        PaletteObjectsList.ItemsSource = PaletteObjects;
        PaletteZonesList.ItemsSource = PaletteZones;
        _paletteSync = true;
        PaletteObjectsList.SelectedIndex = 0;
        PaletteZonesList.SelectedIndex = 0;
        _paletteSync = false;
        _tool = SelectTool;
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
            if (shift) SaveAs_Click(sender, e);
            else Save_Click(sender, e);
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
        _paletteSync = true;
        PaletteObjectsList.SelectedIndex = 0;
        PaletteZonesList.SelectedIndex = 0;
        _paletteSync = false;
        _tool = SelectTool;
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
        RefreshMapCombo(selectId: null);
        _undo.Clear();
        _redo.Clear();
        StatusText.Text = MapCombo.Items.Count == 0
            ? "尚无 mapLayout，可点「新建空图」或「打开地图…」"
            : $"mapLayout {_package.OfType("mapLayout").Count()} · Content/BaseGame/Data/Maps";
    }

    void RefreshMapCombo(string? selectId)
    {
        if (_package == null) return;
        var ids = _package.OfType("mapLayout").Select(d => d.Id).Distinct(StringComparer.Ordinal).ToList();
        MapCombo.ItemsSource = ids;
        if (!string.IsNullOrEmpty(selectId) && ids.Contains(selectId))
            MapCombo.SelectedItem = selectId;
        else if (MapCombo.Items.Count > 0 && MapCombo.SelectedItem == null)
            MapCombo.SelectedIndex = 0;
    }

    void OpenPackage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "选择 Content/BaseGame" };
        if (dlg.ShowDialog() == true) LoadRoot(dlg.FolderName);
    }

    void OpenMapFile_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null)
        {
            MessageBox.Show("请先打开 Content 包（打开包…）");
            return;
        }

        var mapsDir = ContentPathRules.TypeDataDir(_package?.Root, "mapLayout");
        if (string.IsNullOrEmpty(mapsDir))
            mapsDir = IOPath.Combine(PackagePaths.FindContentDataDir() ?? "", "Maps");
        var dlg = new OpenFileDialog
        {
            Title = "打开地图 JSON",
            Filter = "mapLayout JSON|*.json|所有文件|*.*",
            InitialDirectory = mapsDir
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var def = PackageStore.RegisterStandaloneMap(_package, dlg.FileName);
            RefreshMapCombo(def.Id);
            MapCombo.SelectedItem = def.Id;
            StatusText.Text = "已打开 " + dlg.FileName;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "打开失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
        if (_package == null)
        {
            MessageBox.Show("请先打开 Content 包（打开包…）");
            return;
        }

        var dataDir = PackagePaths.FindContentDataDir();
        var mapsDir = ContentPathRules.TypeDataDir(_package?.Root, "mapLayout");
        if (string.IsNullOrEmpty(mapsDir) && !string.IsNullOrEmpty(dataDir))
            mapsDir = IOPath.Combine(dataDir, "Maps");
        if (string.IsNullOrEmpty(mapsDir))
        {
            MessageBox.Show("找不到 Content/BaseGame/Data/Maps，请确认工程路径。");
            return;
        }

        Directory.CreateDirectory(mapsDir);
        if (!TryPromptText("新建空图", "地图 Id（例如 base:map_huangcun）",
                "base:map_" + DateTime.Now.ToString("MMddHHmm"), out var mapId))
            return;
        mapId = mapId.Trim();
        if (string.IsNullOrWhiteSpace(mapId))
        {
            MessageBox.Show("Id 不能为空");
            return;
        }

        if (!LooksLikeDefinitionId(mapId))
        {
            MessageBox.Show("地图 Id 必须是 namespace:local_id 格式，例如 base:map_huangcun_01");
            return;
        }

        if (_package.Find(mapId) != null)
        {
            var overwrite = MessageBox.Show(
                "已存在 " + mapId + "，是否打开现有地图？\n（选「否」则取消新建）",
                "新建空图",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (overwrite == MessageBoxResult.Yes)
            {
                MapCombo.SelectedItem = mapId;
                return;
            }

            return;
        }

        var safeFile = SanitizeFileName(mapId.Replace(':', '_')) + ".json";
        var dlg = new SaveFileDialog
        {
            Title = "新建地图保存到…",
            Filter = "mapLayout JSON|*.json",
            InitialDirectory = mapsDir,
            FileName = safeFile
        };
        if (dlg.ShowDialog() != true) return;

        var displayName = string.IsNullOrWhiteSpace(NameBox.Text) ? mapId : NameBox.Text.Trim();
        if (!TryPromptText("新建空图", "显示名称", displayName, out var name) || string.IsNullOrWhiteSpace(name))
            name = mapId;

        var raw = CreateEmptyMapRaw(mapId, name.Trim());
        try
        {
            PackageStore.SaveStandaloneMapLayout(dlg.FileName, raw);
            var def = PackageStore.RegisterStandaloneMap(_package, dlg.FileName);
            RefreshMapCombo(def.Id);
            MapCombo.SelectedItem = def.Id;
            StatusText.Text = "已新建空图 → " + dlg.FileName;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "新建失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    static JsonObject CreateEmptyMapRaw(string id, string name) => new()
    {
        ["id"] = id,
        ["type"] = "mapLayout",
        ["name"] = name,
        ["worldRegionId"] = "",
        ["originX"] = -40,
        ["originY"] = -25,
        ["cellSize"] = 1,
        ["width"] = 80,
        ["height"] = 50,
        ["placements"] = new JsonArray()
    };

    static string SanitizeFileName(string name)
    {
        foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "map" : name;
    }

    /// <summary>与 Core DefinitionId 一致：必须含且仅含一个冒号分隔的 namespace:local。</summary>
    static bool LooksLikeDefinitionId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        var i = id.IndexOf(':');
        return i > 0 && i < id.Length - 1 && id.IndexOf(':', i + 1) < 0;
    }

    static bool TryPromptText(string title, string label, string initial, out string value)
    {
        value = initial;
        var win = new Window
        {
            Title = title,
            Width = 420,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false
        };
        try
        {
            if (Application.Current?.MainWindow is { IsLoaded: true } owner)
                win.Owner = owner;
        }
        catch { /* ignore */ }

        var box = new TextBox { Text = initial, Margin = new Thickness(0, 8, 0, 8) };
        var ok = false;
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var okBtn = new Button { Content = "确定", Width = 72, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var cancelBtn = new Button { Content = "取消", Width = 72, IsCancel = true };
        okBtn.Click += (_, _) => { ok = true; win.DialogResult = true; };
        cancelBtn.Click += (_, _) => { win.DialogResult = false; };
        buttons.Children.Add(okBtn);
        buttons.Children.Add(cancelBtn);
        var stack = new StackPanel { Margin = new Thickness(12) };
        stack.Children.Add(new TextBlock { Text = label });
        stack.Children.Add(box);
        stack.Children.Add(buttons);
        win.Content = stack;
        win.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };
        var result = win.ShowDialog() == true && ok;
        value = box.Text ?? "";
        return result;
    }

    void PaletteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_paletteSync) return;
        if (sender is not ListBox list) return;
        if (list.SelectedItem is not PaletteItem item) return;

        _tool = item;
        _paletteSync = true;
        if (ReferenceEquals(list, PaletteObjectsList))
            PaletteZonesList.SelectedIndex = item.Kind == null ? 0 : -1;
        else
            PaletteObjectsList.SelectedIndex = item.Kind == null ? 0 : -1;
        _paletteSync = false;

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
            if (!_kindColors.TryGetValue(p.Kind ?? string.Empty, out var c))
                c = Colors.SteelBlue;
            var isZone = !string.IsNullOrEmpty(p.Kind) &&
                         (p.Kind.StartsWith("zone", StringComparison.OrdinalIgnoreCase) ||
                          p.Kind.Equals("spring", StringComparison.OrdinalIgnoreCase) ||
                          p.Kind.Equals("forest", StringComparison.OrdinalIgnoreCase));
            byte a = isZone ? (byte)70 : (p.BlocksMovement ? (byte)220 : (byte)140);
            var fill = new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B));
            var dash = isZone
                ? new DoubleCollection { 4, 2 }
                : null;
            var rect = new Rectangle
            {
                Width = Math.Max(1, p.W) * CellPx,
                Height = Math.Max(1, p.H) * CellPx,
                Fill = fill,
                Stroke = p == _selected ? Brushes.OrangeRed : (isZone ? Brushes.DimGray : Brushes.Black),
                StrokeThickness = p == _selected ? 2.5 : 1,
                StrokeDashArray = dash,
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
                AddResizeHandles(p, gh);
        }

        UpdateSizeHint();
    }

    void AddResizeHandles(PlacementVm p, int gh)
    {
        var w = Math.Max(1, p.W);
        var h = Math.Max(1, p.H);
        var left = p.X * CellPx;
        var right = (p.X + w) * CellPx;
        var top = (gh - p.Y - h) * CellPx;
        var bottom = (gh - p.Y) * CellPx;
        var cx = (left + right) / 2;
        var cy = (top + bottom) / 2;
        var showMidX = right - left >= 18;
        var showMidY = bottom - top >= 18;

        AddResizeHandle("NW", left, top, Cursors.SizeNWSE);
        if (showMidX) AddResizeHandle("N", cx, top, Cursors.SizeNS);
        AddResizeHandle("NE", right, top, Cursors.SizeNESW);
        if (showMidY) AddResizeHandle("E", right, cy, Cursors.SizeWE);
        AddResizeHandle("SE", right, bottom, Cursors.SizeNWSE);
        if (showMidX) AddResizeHandle("S", cx, bottom, Cursors.SizeNS);
        AddResizeHandle("SW", left, bottom, Cursors.SizeNESW);
        if (showMidY) AddResizeHandle("W", left, cy, Cursors.SizeWE);
    }

    void AddResizeHandle(string edge, double x, double y, Cursor cursor)
    {
        const double size = 10;
        var handle = new Rectangle
        {
            Width = size,
            Height = size,
            Fill = Brushes.OrangeRed,
            Stroke = Brushes.White,
            StrokeThickness = 1,
            Tag = "resize:" + edge,
            Cursor = cursor
        };
        Canvas.SetLeft(handle, x - size / 2);
        Canvas.SetTop(handle, y - size / 2);
        MapCanvas.Children.Add(handle);
    }

    void ApplyResize(int dx, int dCanvasY)
    {
        if (_drag == null || string.IsNullOrEmpty(_resizeEdge)) return;
        var edge = _resizeEdge;
        var x1 = _origX;
        var x2 = _origX + Math.Max(1, _origW);
        var y1 = _origY;
        var y2 = _origY + Math.Max(1, _origH);

        if (edge is "E" or "NE" or "SE") x2 = _origX + Math.Max(1, _origW) + dx;
        if (edge is "W" or "NW" or "SW") x1 = _origX + dx;
        // 屏幕向上 = canvas Y 减小 = 地图北侧（Y 增大）
        if (edge is "N" or "NE" or "NW") y2 = _origY + Math.Max(1, _origH) - dCanvasY;
        // 屏幕向下拖南侧：地图原点 Y 减小，高度增大
        if (edge is "S" or "SE" or "SW") y1 = _origY - dCanvasY;

        var left = Math.Min(x1, x2);
        var right = Math.Max(x1, x2);
        var bottom = Math.Min(y1, y2);
        var top = Math.Max(y1, y2);
        _drag.X = left;
        _drag.Y = bottom;
        _drag.W = Math.Max(1, right - left);
        _drag.H = Math.Max(1, top - bottom);
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

        if (source is Rectangle rect && rect.Tag is string tag &&
            tag.StartsWith("resize:", StringComparison.Ordinal))
        {
            if (_selected == null) return;
            PushUndo();
            _drag = _selected;
            _resizing = true;
            _resizeEdge = tag["resize:".Length..];
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
            _resizeEdge = null;
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
        var dCanvasY = (int)Math.Round((pos.Y - _dragStart.Y) / CellPx);

        if (_resizing)
            ApplyResize(dx, dCanvasY);
        else
        {
            _drag.X = _origX + dx;
            _drag.Y = _origY - dCanvasY;
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
            _resizeEdge = null;
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

        if (!TryApplyEditorStateToLayout(out var err))
        {
            MessageBox.Show(err, "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            PackageStore.SaveDefinition(_package, _layout);
            var keep = _layout.Id;
            LoadRoot(_package.Root);
            MapCombo.SelectedItem = keep;
            StatusText.Text = "已保存 → " + _layout.FilePath;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null || _layout == null)
        {
            MessageBox.Show("没有选中 mapLayout");
            return;
        }

        if (!TryApplyEditorStateToLayout(out var err))
        {
            MessageBox.Show(err, "另存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dataDir = PackagePaths.FindContentDataDir();
        var mapsDir = ContentPathRules.TypeDataDir(_package?.Root, "mapLayout");
        if (string.IsNullOrEmpty(mapsDir) && !string.IsNullOrEmpty(dataDir))
            mapsDir = IOPath.Combine(dataDir, "Maps");
        if (!string.IsNullOrEmpty(mapsDir))
            Directory.CreateDirectory(mapsDir);

        var defaultName = ContentPathRules.SuggestMapFileName(_layout.Id);
        var dlg = new SaveFileDialog
        {
            Title = "另存为地图 JSON",
            Filter = "mapLayout JSON|*.json",
            InitialDirectory = mapsDir ?? IOPath.GetDirectoryName(_layout.FilePath) ?? dataDir ?? "",
            FileName = defaultName
        };
        if (dlg.ShowDialog() != true) return;

        if (!TryPromptText("另存为", "地图 Id（可改成新关卡 id）", _layout.Id, out var newId) ||
            string.IsNullOrWhiteSpace(newId))
            return;
        newId = newId.Trim();
        if (!LooksLikeDefinitionId(newId))
        {
            MessageBox.Show("地图 Id 必须是 namespace:local_id 格式，例如 base:map_huangcun_01");
            return;
        }

        _layout.Raw["id"] = newId;
        _layout.Id = newId;

        try
        {
            PackageStore.SaveStandaloneMapLayout(dlg.FileName, _layout.Raw);
            var def = PackageStore.RegisterStandaloneMap(_package, dlg.FileName);
            RefreshMapCombo(def.Id);
            MapCombo.SelectedItem = def.Id;
            StatusText.Text = "已另存为 → " + dlg.FileName;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "另存失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    bool TryApplyEditorStateToLayout(out string err)
    {
        err = "";
        if (_layout == null)
        {
            err = "没有选中 mapLayout";
            return false;
        }

        if (!TryReadMapSize(out var w, out var h, out var ox, out var oy, out var cs, out err))
            return false;

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
        _layout.Name = NameBox.Text ?? "";
        return true;
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
