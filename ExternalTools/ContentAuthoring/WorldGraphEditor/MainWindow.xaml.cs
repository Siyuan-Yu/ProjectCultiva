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
    readonly HexMapViewHost _mapView = new();
    bool _panning;
    Point _panLast;
    bool _painting;
    bool _strokeUndoPushed;
    HexCoordDto _lastStatusHex = new(-1, -1);

    public MainWindow()
    {
        InitializeComponent();
        Title = "XianXia · WorldGraphEditor — Hex World";
        foreach (var entry in HexTerrainPalette.Legend)
            TerrainList.Items.Add($"{entry.Label} ({entry.Id})");
        TerrainList.SelectedIndex = 2;

        MapHost.Child = _mapView;
        _document.Changed += OnDocumentChanged;
        _document.CellsMutated += OnCellsMutated;
        _document.WorldReplaced += OnWorldReplaced;
        _document.SitesMutated += OnSitesMutated;
        TryLoadDefaultWorld();
    }

    void TryLoadDefaultWorld()
    {
        var root = PackagePaths.FindDefaultBaseGame();
        if (root == null)
        {
            StatusText.Text = "未找到 Content/BaseGame；请用「打开」选择 hexWorld JSON。";
            RefreshChrome();
            return;
        }

        var path = Path.Combine(root, "Data", "Worlds", "ch01_hex_world.json");
        if (File.Exists(path))
        {
            LoadFromPath(path);
            return;
        }

        StatusText.Text = "未找到 ch01_hex_world.json，已显示空白世界。";
        RefreshChrome();
    }

    void LoadFromPath(string path)
    {
        var world = HexWorldContentJson.LoadDefinition(path);
        _document.Load(world, path);
        _viewport.FitWorld(world.Width, world.Height);
        BindMap(fullRebuild: true);
        RefreshChrome();
        UpdateValidationSummary();
    }

    void BindMap(bool fullRebuild)
    {
        _viewport.SetViewportSize(_mapView.ActualWidth > 1 ? _mapView.ActualWidth : MapHost.ActualWidth,
            _mapView.ActualHeight > 1 ? _mapView.ActualHeight : MapHost.ActualHeight);
        _mapView.SetWorld(_document.World, _viewport, fullRebuild);
        _mapView.SetSelection(_document.SelectedHex);
        SyncSiteOverlay();
    }

    void SyncSiteOverlay() =>
        _mapView.SetSiteOverlay(_document.SelectedSiteId, _document.EditFootprintMode);

    void OnSitesMutated()
    {
        SyncSiteOverlay();
        RefreshChrome();
        UpdateInspector();
        UpdateSiteFootprintPanel();
    }

    void OnWorldReplaced()
    {
        BindMap(fullRebuild: true);
        RefreshChrome();
    }

    void OnCellsMutated(IReadOnlyList<(int Q, int R)> hexes)
    {
        _mapView.MarkHexesDirty(hexes);
        RefreshChrome();
        UpdateInspector();
    }

    void OnDocumentChanged()
    {
        RefreshChrome();
        UpdateInspector();
        UpdateSiteFootprintPanel();
        SyncSiteOverlay();
    }

    void RefreshChrome()
    {
        Title = (_document.IsDirty ? "* " : string.Empty) + "XianXia · WorldGraphEditor — Hex World";
        PathText.Text = string.IsNullOrEmpty(_document.FilePath) ? "(未保存)" : _document.FilePath;
        StatusText.Text = _mapView.FormatPerfStatus();
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

        UpdateSiteFootprintPanel();
    }

    void UpdateSiteFootprintPanel()
    {
        var site = _document.GetSelectedSite();
        if (site == null)
        {
            SiteAnchorText.Text = "AnchorHex：—";
            SiteFootprintCountText.Text = "Footprint Count：—";
            SiteFootprintListText.Text = "Footprint Hexes：—";
            FootprintEditStatusText.Text = string.Empty;
            SetAnchorButton.IsEnabled = false;
            return;
        }

        var footprint = HexWorldFootprintRules.ResolveFootprint(site);
        SiteAnchorText.Text = $"AnchorHex：({site.AnchorQ},{site.AnchorR})";
        SiteFootprintCountText.Text = $"Footprint Count：{footprint.Count}";
        SiteFootprintListText.Text = "Footprint Hexes：\n" +
                                      string.Join("\n", footprint.Select(h => $"({h.Q},{h.R})"));
        var validation = HexWorldFootprintRules.ValidateSiteFootprint(site);
        FootprintEditStatusText.Text = validation.Success
            ? (_document.EditFootprintMode
                ? "Footprint 编辑模式：左键加入 · 右键移除"
                : string.Empty)
            : validation.Message;
        if (!string.IsNullOrEmpty(_document.LastFootprintEditMessage))
            FootprintEditStatusText.Text = _document.LastFootprintEditMessage;
        SetAnchorButton.IsEnabled = _document.SelectedHex is { Q: >= 0 };
    }

    void SetAnchor_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_document.SelectedSiteId) || _document.SelectedHex is not { Q: >= 0 } hex)
            return;
        var result = _document.SetSiteAnchor(_document.SelectedSiteId, hex);
        StatusText.Text = result.Message + " · " + _mapView.FormatPerfStatus();
        UpdateSiteFootprintPanel();
        SyncSiteOverlay();
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
        BindMap(fullRebuild: true);
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
        StatusText.Text = "已保存 " + _document.FilePath + " · " + _mapView.FormatPerfStatus();
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
            ? "Validation OK · " + _mapView.FormatPerfStatus()
            : $"Validation: {issues.Count(i => i.Level == "error")} errors, {issues.Count(i => i.Level == "warn")} warnings";
    }

    void Fit_Click(object sender, RoutedEventArgs e)
    {
        _viewport.FitWorld(_document.World.Width, _document.World.Height);
        _mapView.SyncViewport(rebuildGeometry: false);
        RefreshChrome();
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

    void FootprintMode_Changed(object sender, RoutedEventArgs e)
    {
        _document.EditFootprintMode = FootprintModeBox.IsChecked == true;
        SyncSiteOverlay();
        UpdateSiteFootprintPanel();
    }

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
        _mapView.SetSelection(_document.SelectedHex);
        _mapView.SyncViewport(rebuildGeometry: false);
    }

    void MapHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_viewport.ViewHalf <= 1)
            _viewport.FitWorld(_document.World.Width, _document.World.Height);
        _mapView.SyncViewport(rebuildGeometry: false);
        RefreshChrome();
    }

    void MapHost_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var factor = e.Delta > 0 ? 1.0 / 1.12 : 1.12;
        _viewport.ViewHalf = Math.Max(2, _viewport.ViewHalf * factor);
        _mapView.SyncViewport(rebuildGeometry: false);
        RefreshChrome();
        e.Handled = true;
    }

    void MapHost_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle ||
            (e.ChangedButton == MouseButton.Left && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)))
        {
            _panning = true;
            _panLast = e.GetPosition(_mapView);
            _mapView.CaptureMouse();
            e.Handled = true;
        }
    }

    void MapHost_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_panning && (e.ChangedButton == MouseButton.Middle ||
                         (e.ChangedButton == MouseButton.Left && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))))
        {
            _panning = false;
            _mapView.ReleaseMouseCapture();
        }

        if (e.ChangedButton == MouseButton.Left && _painting)
        {
            _painting = false;
            _mapView.ReleaseMouseCapture();
            _strokeUndoPushed = false;
        }
    }

    void MapHost_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(_mapView);
        var hex = _viewport.ScreenToHex(pos.X, pos.Y, _document.World.Width, _document.World.Height);

        if (_panning)
        {
            var delta = pos - _panLast;
            _panLast = pos;
            _viewport.ViewCenterX -= delta.X / _viewport.Scale;
            _viewport.ViewCenterY += delta.Y / _viewport.Scale;
            _mapView.SyncViewport(rebuildGeometry: false);
            StatusText.Text = _mapView.FormatPerfStatus();
            return;
        }

        var hoverChanged = _mapView.SetHover(hex.Q >= 0 ? hex : null);
        if (hoverChanged || hex.Q != _lastStatusHex.Q || hex.R != _lastStatusHex.R)
        {
            _lastStatusHex = hex;
            StatusText.Text = hex.Q >= 0
                ? $"Hex ({hex.Q},{hex.R}) · Tool {_document.ActiveTool} · {_mapView.FormatPerfStatus()}"
                : _mapView.FormatPerfStatus();
        }

        if (_painting && e.LeftButton == MouseButtonState.Pressed && hex.Q >= 0)
            ApplyTool(hex, false);
    }

    void MapHost_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_panning)
            return;
        var pos = e.GetPosition(_mapView);
        var hex = _viewport.ScreenToHex(pos.X, pos.Y, _document.World.Width, _document.World.Height);
        if (hex.Q < 0)
            return;

        _document.SelectedHex = hex;
        _mapView.SetSelection(hex);

        if (_document.ActiveTool is HexEditorTool.Terrain or HexEditorTool.Road or HexEditorTool.Erase)
        {
            _painting = true;
            _strokeUndoPushed = false;
            _mapView.CaptureMouse();
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
            var result = _document.ToggleFootprintHex(_document.SelectedSiteId, hex, add);
            StatusText.Text = result.Message + " · " + _mapView.FormatPerfStatus();
            UpdateSiteFootprintPanel();
            SyncSiteOverlay();
            return;
        }

        var site = _document.FindSiteAt(hex);
        if (site != null)
            _document.SelectedSiteId = site.SiteId;
        UpdateInspector();
        RefreshChrome();
    }

    void MapHost_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_document.EditFootprintMode || string.IsNullOrEmpty(_document.SelectedSiteId))
            return;
        var pos = e.GetPosition(_mapView);
        var hex = _viewport.ScreenToHex(pos.X, pos.Y, _document.World.Width, _document.World.Height);
        if (hex.Q < 0)
            return;

        _document.SelectedHex = hex;
        _mapView.SetSelection(hex);
        var result = _document.ToggleFootprintHex(_document.SelectedSiteId, hex, add: false);
        StatusText.Text = result.Message + " · " + _mapView.FormatPerfStatus();
        UpdateSiteFootprintPanel();
        SyncSiteOverlay();
        e.Handled = true;
    }

    void MapHost_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_painting)
            return;
        _painting = false;
        _mapView.ReleaseMouseCapture();
        _strokeUndoPushed = false;
    }

    void ApplyTool(HexCoordDto hex, bool firstStroke)
    {
        if (!_strokeUndoPushed)
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
