using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ContentAuthoring.Shared;
using Microsoft.Win32;

namespace MapEditor;

public partial class MainWindow : Window
{
    const double CellPx = 10;

    static readonly (string Kind, string Label, int W, int H, bool Block, Color Color)[] Palette =
    {
        ("wall", "墙／岩壁", 6, 4, true, Color.FromRgb(90, 90, 100)),
        ("house", "房子", 8, 8, true, Color.FromRgb(140, 100, 70)),
        ("rock", "岩石／棚", 4, 4, true, Color.FromRgb(110, 110, 110)),
        ("herbField", "药田", 50, 50, false, Color.FromRgb(70, 150, 90)),
        ("grainField", "麦田／农田", 20, 16, false, Color.FromRgb(180, 170, 70)),
        ("forest", "树林", 14, 12, false, Color.FromRgb(40, 110, 60)),
        ("mine", "矿洞区", 10, 8, false, Color.FromRgb(100, 90, 70)),
        ("spring", "灵泉", 8, 8, false, Color.FromRgb(80, 160, 200)),
        ("cave", "洞府区", 10, 8, false, Color.FromRgb(120, 90, 140)),
        ("roadHub", "道路枢纽", 8, 8, false, Color.FromRgb(160, 140, 120))
    };

    readonly Dictionary<string, Color> _kindColors = new(StringComparer.Ordinal);
    readonly ObservableCollection<PlacementVm> _placements = new();

    ContentPackage? _package;
    DefRef? _layout;
    PlacementVm? _selected;
    PlacementVm? _drag;
    bool _resizing;
    Point _dragStart;
    int _origX, _origY, _origW, _origH;
    string? _paletteKind;

    public MainWindow()
    {
        InitializeComponent();
        Title = "XianXia · MapEditor（格点地图）";
        foreach (var p in Palette)
            _kindColors[p.Kind] = p.Color;
        PaletteList.ItemsSource = Palette.Select(p => $"{p.Kind} — {p.Label} ({p.W}×{p.H})").ToList();
        PlacementList.ItemsSource = _placements;
        TryLoadDefault();
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
        else StatusText.Text = "包中尚无 mapLayout，可点「从 Ch01 模板新建」";
        StatusText.Text = $"mapLayout {_package.OfType("mapLayout").Count()} · 包已加载";
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
        RebuildCanvas();
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

        // Minimal empty map matching demo extents
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
        if (PaletteList.SelectedItem is not string label) return;
        _paletteKind = label.Split(' ')[0];
        StatusText.Text = $"已选设施 {_paletteKind}：在画布空白处单击放置";
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
        _selected.Id = PropId.Text?.Trim() ?? _selected.Id;
        _selected.Label = PropLabel.Text ?? "";
        _selected.BoundLocationId = PropBound.Text ?? "";
        _selected.BlocksMovement = PropBlock.IsChecked == true;
        if (int.TryParse(PropX.Text, out var x)) _selected.X = x;
        if (int.TryParse(PropY.Text, out var y)) _selected.Y = y;
        if (int.TryParse(PropW.Text, out var w) && w >= 1) _selected.W = w;
        if (int.TryParse(PropH.Text, out var h) && h >= 1) _selected.H = h;
        RebuildCanvas();
    }

    void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        _placements.Remove(_selected);
        SelectPlacement(null);
        RebuildCanvas();
    }

    void ResizeMap_Click(object sender, RoutedEventArgs e) => RebuildCanvas();

    void RebuildCanvas()
    {
        if (!int.TryParse(WidthBox.Text, out var gw) || gw < 1) gw = 80;
        if (!int.TryParse(HeightBox.Text, out var gh) || gh < 1) gh = 50;

        MapCanvas.Children.Clear();
        MapCanvas.Width = gw * CellPx;
        MapCanvas.Height = gh * CellPx;

        // background
        MapCanvas.Children.Add(new Rectangle
        {
            Width = MapCanvas.Width,
            Height = MapCanvas.Height,
            Fill = new SolidColorBrush(Color.FromRgb(232, 226, 214))
        });

        // grid lines (every cell lightly, every 10 stronger)
        for (var x = 0; x <= gw; x++)
        {
            var line = new Line
            {
                X1 = x * CellPx,
                Y1 = 0,
                X2 = x * CellPx,
                Y2 = gh * CellPx,
                Stroke = new SolidColorBrush(x % 10 == 0 ? Color.FromRgb(180, 170, 150) : Color.FromRgb(210, 200, 185)),
                StrokeThickness = x % 10 == 0 ? 1.2 : 0.5
            };
            MapCanvas.Children.Add(line);
        }
        for (var y = 0; y <= gh; y++)
        {
            var line = new Line
            {
                X1 = 0,
                Y1 = y * CellPx,
                X2 = gw * CellPx,
                Y2 = y * CellPx,
                Stroke = new SolidColorBrush(y % 10 == 0 ? Color.FromRgb(180, 170, 150) : Color.FromRgb(210, 200, 185)),
                StrokeThickness = y % 10 == 0 ? 1.2 : 0.5
            };
            MapCanvas.Children.Add(line);
        }

        // note: canvas Y grows down; cell Y matches data (origin bottom-left in world, but we draw Y up from top as row index)
        foreach (var p in _placements)
        {
            if (!_kindColors.TryGetValue(p.Kind, out var c))
                c = Colors.SteelBlue;
            var fill = new SolidColorBrush(Color.FromArgb(p.BlocksMovement ? (byte)220 : (byte)140, c.R, c.G, c.B));
            var border = p == _selected ? Brushes.OrangeRed : Brushes.Black;
            var rect = new Rectangle
            {
                Width = Math.Max(1, p.W) * CellPx,
                Height = Math.Max(1, p.H) * CellPx,
                Fill = fill,
                Stroke = border,
                StrokeThickness = p == _selected ? 2.5 : 1,
                Tag = p,
                Cursor = Cursors.SizeAll
            };
            // Flip Y for display: row 0 at bottom of world-feeling — use top-left data as-is (cell y from bottom of origin in game is different)
            // Editor uses same cell indices as JSON (x right, y up in world). Canvas: y increases down → invert.
            Canvas.SetLeft(rect, p.X * CellPx);
            Canvas.SetTop(rect, (gh - p.Y - Math.Max(1, p.H)) * CellPx);
            rect.MouseLeftButtonDown += Placement_MouseDown;
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
                    Width = 8,
                    Height = 8,
                    Fill = Brushes.OrangeRed,
                    Tag = "resize",
                    Cursor = Cursors.SizeNWSE
                };
                Canvas.SetLeft(handle, (p.X + Math.Max(1, p.W)) * CellPx - 4);
                Canvas.SetTop(handle, (gh - p.Y) * CellPx - 4);
                handle.MouseLeftButtonDown += ResizeHandle_MouseDown;
                MapCanvas.Children.Add(handle);
            }
        }
    }

    void Placement_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not PlacementVm vm) return;
        SelectPlacement(vm);
        _drag = vm;
        _resizing = false;
        _dragStart = e.GetPosition(MapCanvas);
        _origX = vm.X;
        _origY = vm.Y;
        _origW = vm.W;
        _origH = vm.H;
        fe.CaptureMouse();
        e.Handled = true;
    }

    void ResizeHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_selected == null) return;
        _drag = _selected;
        _resizing = true;
        _dragStart = e.GetPosition(MapCanvas);
        _origX = _selected.X;
        _origY = _selected.Y;
        _origW = _selected.W;
        _origH = _selected.H;
        ((UIElement)sender).CaptureMouse();
        e.Handled = true;
    }

    void MapCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_drag == null || e.LeftButton != MouseButtonState.Pressed) return;
        if (!int.TryParse(HeightBox.Text, out var gh) || gh < 1) gh = 50;
        var pos = e.GetPosition(MapCanvas);
        var dx = (int)Math.Round((pos.X - _dragStart.X) / CellPx);
        var dyCanvas = (int)Math.Round((pos.Y - _dragStart.Y) / CellPx);
        // canvas Y down → data Y up
        var dy = -dyCanvas;

        if (_resizing)
        {
            _drag.W = Math.Max(1, _origW + dx);
            _drag.H = Math.Max(1, _origH + dy);
        }
        else
        {
            _drag.X = Math.Max(0, _origX + dx);
            _drag.Y = Math.Max(0, _origY + dy);
        }

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
            Mouse.Capture(null);
        }
    }

    void MapCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_paletteKind == null || _package == null) return;
        if (e.OriginalSource is Rectangle { Tag: PlacementVm }) return;
        if (!int.TryParse(HeightBox.Text, out var gh) || gh < 1) gh = 50;
        if (!int.TryParse(WidthBox.Text, out var gw) || gw < 1) gw = 80;

        var pos = e.GetPosition(MapCanvas);
        var cx = (int)Math.Floor(pos.X / CellPx);
        var cyTop = (int)Math.Floor(pos.Y / CellPx);
        var spec = Palette.First(p => p.Kind == _paletteKind);
        var w = spec.W > 0 ? spec.W : 4;
        var h = spec.H > 0 ? spec.H : 4;
        var cy = gh - cyTop - h;
        if (cx < 0 || cy < 0 || cx >= gw || cy >= gh) return;

        var vm = new PlacementVm
        {
            Id = $"place_{_paletteKind}_{DateTime.Now:HHmmss}",
            Kind = _paletteKind,
            Label = spec.Label ?? _paletteKind,
            X = cx,
            Y = Math.Max(0, cy),
            W = w,
            H = h,
            BlocksMovement = spec.Block
        };
        _placements.Add(vm);
        SelectPlacement(vm);
        RebuildCanvas();
        e.Handled = true;
    }

    void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null || _layout == null)
        {
            MessageBox.Show("没有选中 mapLayout");
            return;
        }

        if (!int.TryParse(WidthBox.Text, out var w) || !int.TryParse(HeightBox.Text, out var h) ||
            !double.TryParse(OriginXBox.Text, out var ox) || !double.TryParse(OriginYBox.Text, out var oy) ||
            !double.TryParse(CellSizeBox.Text, out var cs) || w < 1 || h < 1 || cs <= 0)
        {
            MessageBox.Show("地图尺寸／原点／cellSize 无效");
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
        foreach (var p in _placements) arr.Add(p.ToJson());
        _layout.Raw["placements"] = arr;

        try
        {
            PackageStore.SaveDefinition(_package, _layout);
            var keep = _layout.Id;
            LoadRoot(_package.Root);
            MapCombo.SelectedItem = keep;
            StatusText.Text = "已保存 mapLayout → Unity Play 将用此网格寻路";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
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
