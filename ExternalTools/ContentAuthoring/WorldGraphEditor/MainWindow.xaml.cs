using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ContentAuthoring.Shared;
using ContentAuthoring.Shared.HexWorld;
using Microsoft.Win32;

namespace WorldGraphEditor;

public partial class MainWindow : Window
{
    readonly HexWorldEditorDocument _document = new();
    readonly HexMapViewport _viewport = new();
    bool _panning;
    Point _panLast;
    bool _painting;
    bool _strokeUndoPushed;

    public MainWindow()
    {
        InitializeComponent();
        Title = "XianXia · WorldGraphEditor — Hex World";
        foreach (var entry in HexTerrainPalette.Legend)
            TerrainList.Items.Add($"{entry.Label} ({entry.Id})");
        TerrainList.SelectedIndex = 2;
        _document.Changed += RefreshUi;
        TryLoadDefaultWorld();
    }

    void TryLoadDefaultWorld()
    {
        var root = PackagePaths.FindDefaultBaseGame();
        if (root == null)
        {
            StatusText.Text = "未找到 Content/BaseGame；请用「打开」选择 hexWorld JSON。";
            RefreshUi();
            return;
        }

        var path = Path.Combine(root, "Data", "Worlds", "ch01_hex_world.json");
        if (File.Exists(path))
        {
            LoadFromPath(path);
            return;
        }

        StatusText.Text = "未找到 ch01_hex_world.json，已显示空白世界。";
        RefreshUi();
    }

    void LoadFromPath(string path)
    {
        var world = HexWorldContentJson.LoadDefinition(path);
        _document.Load(world, path);
        _viewport.FitWorld(world.Width, world.Height);
        RefreshUi();
    }

    void RefreshUi()
    {
        Title = (_document.IsDirty ? "* " : string.Empty) + "XianXia · WorldGraphEditor — Hex World";
        PathText.Text = string.IsNullOrEmpty(_document.FilePath) ? "(未保存)" : _document.FilePath;
        HexMapCanvasRenderer.Render(MapCanvas, _document, _viewport, _painting);
        UpdateInspector();
        UpdateValidationSummary();
    }

    void UpdateInspector()
    {
        if (_document.SelectedHex is not { } hex || hex.Q < 0)
        {
            InspectorText.Text = "点击 Hex 查看详情。";
            return;
        }

        var cell = HexWorldContentGenerator.GetCell(_document.World, hex.Q, hex.R);
        if (cell == null)
        {
            InspectorText.Text = $"Hex ({hex.Q},{hex.R}) — 无 Cell 数据";
            return;
        }

        var passable = cell.Passable ?? HexTerrainPalette.DefaultPassable(cell.Terrain);
        var site = _document.FindSiteAt(hex);
        InspectorText.Text =
            $"Coord: ({hex.Q},{hex.R})\n" +
            $"Terrain: {HexTerrainPalette.ResolveLabel(cell.Terrain)} / {cell.Terrain}\n" +
            $"Passable: {passable}\n" +
            $"Road: {cell.IsRoad}\n" +
            $"Site: {(site?.DisplayName ?? "-")}";
        if (site != null)
        {
            SiteIdBox.Text = site.SiteId;
            SiteNameBox.Text = site.DisplayName;
            SiteTypeBox.Text = site.SiteType;
            SiteLocalMapBox.Text = site.LocalMapId;
            _document.SelectedSiteId = site.SiteId;
        }
    }

    void UpdateValidationSummary()
    {
        var issues = HexWorldContentValidator.Validate(_document.World);
        ValidationText.Text = issues.Count == 0
            ? "Validation: OK"
            : string.Join("\n", issues.Take(8).Select(i => $"[{i.Level}] {i.Message}"));
    }

    void NewWorld_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscard())
            return;
        _document.NewWorld(100, 50, HexTerrainIds.Mountain, passable: false);
        _document.World.Id = "base:hex_world_new";
        _document.World.Name = "New Hex World";
        _viewport.FitWorld(_document.World.Width, _document.World.Height);
    }

    void Open_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscard())
            return;
        var dlg = new OpenFileDialog
        {
            Filter = "Hex World JSON|*.json",
            Title = "打开 Hex World Content",
        };
        if (dlg.ShowDialog() == true)
            LoadFromPath(dlg.FileName);
    }

    void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_document.FilePath))
        {
            SaveAs_Click(sender, e);
            return;
        }

        _document.Save(_document.FilePath);
        StatusText.Text = "已保存 " + _document.FilePath;
    }

    void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Hex World JSON|*.json",
            FileName = "hex_world.json",
        };
        if (dlg.ShowDialog() != true)
            return;
        _document.Save(dlg.FileName);
        StatusText.Text = "已保存 " + dlg.FileName;
    }

    void Validate_Click(object sender, RoutedEventArgs e)
    {
        UpdateValidationSummary();
        var issues = HexWorldContentValidator.Validate(_document.World);
        StatusText.Text = issues.Count == 0
            ? "Validation OK"
            : $"Validation: {issues.Count(i => i.Level == "error")} errors, {issues.Count(i => i.Level == "warn")} warnings";
    }

    void Fit_Click(object sender, RoutedEventArgs e)
    {
        _viewport.FitWorld(_document.World.Width, _document.World.Height);
        RefreshUi();
    }

    void Undo_Click(object sender, RoutedEventArgs e) => _document.Undo();
    void Redo_Click(object sender, RoutedEventArgs e) => _document.Redo();

    void ToolChanged(object sender, RoutedEventArgs e)
    {
        if (ToolSelect.IsChecked == true) _document.ActiveTool = HexEditorTool.Select;
        else if (ToolTerrain.IsChecked == true) _document.ActiveTool = HexEditorTool.Terrain;
        else if (ToolRoad.IsChecked == true) _document.ActiveTool = HexEditorTool.Road;
        else if (ToolSite.IsChecked == true) _document.ActiveTool = HexEditorTool.Site;
        else if (ToolErase.IsChecked == true) _document.ActiveTool = HexEditorTool.Erase;
    }

    void BrushRadius_Changed(object sender, SelectionChangedEventArgs e)
    {
        _document.BrushRadius = BrushRadiusCombo.SelectedIndex switch
        {
            1 => 1,
            2 => 2,
            3 => 4,
            _ => 0,
        };
    }

    void TerrainList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TerrainList.SelectedIndex < 0 || TerrainList.SelectedIndex >= HexTerrainPalette.Legend.Count)
            return;
        _document.ActiveTerrain = HexTerrainPalette.Legend[TerrainList.SelectedIndex].Id;
    }

    void SiteType_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (SiteTypeCombo.SelectedItem is ComboBoxItem item)
            _document.ActiveSiteType = item.Content?.ToString() ?? "Village";
    }

    void FootprintMode_Changed(object sender, RoutedEventArgs e) =>
        _document.EditFootprintMode = FootprintModeBox.IsChecked == true;

    void DeleteSite_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_document.SelectedSiteId))
            return;
        _document.DeleteSite(_document.SelectedSiteId);
    }

    void SiteFields_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_document.SelectedSiteId))
            return;
        var site = _document.World.Sites.FirstOrDefault(s => s.SiteId == _document.SelectedSiteId);
        if (site == null)
            return;
        _document.PushUndo();
        site.SiteId = SiteIdBox.Text.Trim();
        site.DisplayName = SiteNameBox.Text.Trim();
        site.SiteType = SiteTypeBox.Text.Trim();
        site.LocalMapId = SiteLocalMapBox.Text.Trim();
        _document.MarkDirty(true);
    }

    void MapCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_viewport.ViewHalf <= 1)
            _viewport.FitWorld(_document.World.Width, _document.World.Height);
        RefreshUi();
    }

    void MapCanvas_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var factor = e.Delta > 0 ? 1.0 / 1.12 : 1.12;
        _viewport.ViewHalf = Math.Max(2, _viewport.ViewHalf * factor);
        RefreshUi();
        e.Handled = true;
    }

    void MapCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle ||
            (e.ChangedButton == MouseButton.Left && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)))
        {
            _panning = true;
            _panLast = e.GetPosition(MapCanvas);
            MapCanvas.CaptureMouse();
            e.Handled = true;
        }
    }

    void MapCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_panning && (e.ChangedButton == MouseButton.Middle ||
                         (e.ChangedButton == MouseButton.Left && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))))
        {
            _panning = false;
            MapCanvas.ReleaseMouseCapture();
        }

        if (e.ChangedButton == MouseButton.Left && _painting)
        {
            _painting = false;
            MapCanvas.ReleaseMouseCapture();
            _strokeUndoPushed = false;
        }
    }

    void MapCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(MapCanvas);
        var hex = _viewport.ScreenToHex(pos.X, pos.Y, _document.World.Width, _document.World.Height);
        StatusText.Text = hex.Q >= 0
            ? $"Hex ({hex.Q},{hex.R}) · Tool {_document.ActiveTool} · ZoomHalf {_viewport.ViewHalf:F1}"
            : "Hex —";

        if (_panning)
        {
            var delta = pos - _panLast;
            _panLast = pos;
            _viewport.ViewCenterX -= delta.X / _viewport.Scale;
            _viewport.ViewCenterY += delta.Y / _viewport.Scale;
            RefreshUi();
            return;
        }

        if (_painting && e.LeftButton == MouseButtonState.Pressed && hex.Q >= 0)
            ApplyTool(hex, false);
    }

    void MapCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_panning)
            return;
        var pos = e.GetPosition(MapCanvas);
        var hex = _viewport.ScreenToHex(pos.X, pos.Y, _document.World.Width, _document.World.Height);
        if (hex.Q < 0)
            return;

        _document.SelectedHex = hex;
        if (_document.ActiveTool is HexEditorTool.Terrain or HexEditorTool.Road or HexEditorTool.Erase)
        {
            _painting = true;
            _strokeUndoPushed = false;
            MapCanvas.CaptureMouse();
            ApplyTool(hex, true);
            return;
        }

        if (_document.ActiveTool == HexEditorTool.Site)
        {
            _document.CreateSite(hex);
            return;
        }

        if (_document.EditFootprintMode && !string.IsNullOrEmpty(_document.SelectedSiteId))
        {
            var add = Keyboard.Modifiers != ModifierKeys.Control;
            _document.ToggleFootprintHex(_document.SelectedSiteId, hex, add);
            return;
        }

        var site = _document.FindSiteAt(hex);
        if (site != null)
            _document.SelectedSiteId = site.SiteId;
        RefreshUi();
    }

    void MapCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_painting)
            return;
        _painting = false;
        MapCanvas.ReleaseMouseCapture();
        _strokeUndoPushed = false;
    }

    void ApplyTool(HexCoordDto hex, bool firstStroke)
    {
        if (firstStroke && !_strokeUndoPushed)
        {
            _document.PushUndo();
            _strokeUndoPushed = true;
        }
        else if (!_strokeUndoPushed)
        {
            _document.PushUndo();
            _strokeUndoPushed = true;
        }

        switch (_document.ActiveTool)
        {
            case HexEditorTool.Terrain:
                _document.ApplyTerrainBrush(hex, singleStrokeUndo: false);
                break;
            case HexEditorTool.Road:
                _document.ApplyRoadBrush(hex, singleStrokeUndo: false);
                break;
            case HexEditorTool.Erase:
                _document.ApplyEraseBrush(hex, singleStrokeUndo: false);
                break;
        }
    }

    void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.S)
        {
            Save_Click(sender, e);
            e.Handled = true;
        }
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.Z)
        {
            _document.Undo();
            e.Handled = true;
        }
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.Y)
        {
            _document.Redo();
            e.Handled = true;
        }
    }

    void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!ConfirmDiscard())
            e.Cancel = true;
    }

    bool ConfirmDiscard()
    {
        if (!_document.IsDirty)
            return true;
        var result = MessageBox.Show("有未保存修改，是否保存？", "Hex World Editor", MessageBoxButton.YesNoCancel);
        if (result == MessageBoxResult.Cancel)
            return false;
        if (result == MessageBoxResult.Yes)
            Save_Click(this, new RoutedEventArgs());
        return true;
    }
}
