using System.IO;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    readonly ObservableCollection<FactionListItem> _territoryItems = new();
    bool _territoryEditMode, _territoryErasing;
    bool _sitePlacementArmed;
    string? _selectedFactionId;

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
        _document.TerritoriesMutated += OnTerritoriesMutated;
        TerritoryList.ItemsSource = _territoryItems;
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
        _mapView.SetTerritoryOverlay(null, _territoryEditMode);
    }

    void SyncSiteOverlay() =>
        _mapView.SetSiteOverlay(_document.SelectedSiteId, _document.EditFootprintMode);

    void OnSitesMutated()
    {
        SyncSiteOverlay();
        RefreshChrome();
        UpdateInspector();
        UpdateSiteFootprintPanel();
        RefreshTerritoryPanel();
    }

    void OnTerritoriesMutated()
    {
        _mapView.RebuildTerritoryOverlay();
        RefreshTerritoryPanel();
        UpdateInspector();
        UpdateValidationSummary();
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
        var territory = _document.FindTerritoryAt(hex);
        InspectorText.Text =
            $"Coord: ({hex.Q},{hex.R})\n" +
            $"Terrain: {HexTerrainPalette.ResolveLabel(cell.Terrain)} / {cell.Terrain}\n" +
            $"Passable: {passable}\n" +
            $"Road: {cell.IsRoad}\n" +
            $"Site: {(site?.DisplayName ?? "-")}\n" +
            $"TerritoryRegion: {(territory?.RegionId ?? "-")}\n" +
            $"Controller: {(territory?.ControlFactionId ?? "-")}\n" +
            $"PrimaryWorldSite: {(territory?.PrimaryWorldSiteId ?? "-")}";
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
            SitePresenceText.Text = "PresenceHex：—";
            SiteFootprintCountText.Text = "Footprint Count：—";
            SiteFootprintListText.Text = "Footprint Hexes：—";
            FootprintEditStatusText.Text = string.Empty;
            SetAnchorButton.IsEnabled = false;
            SetPresenceButton.Visibility = Visibility.Collapsed;
            return;
        }

        var footprint = HexWorldFootprintRules.ResolveFootprint(site);
        HexWorldPresenceRules.SyncPresenceToAnchor(site);
        SiteAnchorText.Text = $"AnchorHex：({site.AnchorQ},{site.AnchorR})";
        SitePresenceText.Text =
            $"PresenceHex = AnchorHex (compatibility)：({site.AnchorQ},{site.AnchorR})";
        SiteFootprintCountText.Text = $"Footprint Count：{footprint.Count}";
        SiteFootprintListText.Text = "Footprint Hexes：\n" +
                                      string.Join("\n", footprint.Select(h => $"({h.Q},{h.R})"));
        var territory = _document.GetTerritoryForSite(site.SiteId);
        SiteTerritoryText.Text = $"OwnerFactionId：{site.OwnerFactionId}\nTerritoryRegionId：{site.TerritoryRegionId}\nTerritory Hex Count：{territory?.Hexes.Count ?? 0}";
        var validation = HexWorldFootprintRules.ValidateSiteFootprint(site);
        FootprintEditStatusText.Text = validation.Success
            ? (_document.EditFootprintMode
                ? "Footprint 编辑模式：左键加入 · 右键移除"
                : string.Empty)
            : validation.Message;
        if (!string.IsNullOrEmpty(_document.LastFootprintEditMessage))
            FootprintEditStatusText.Text = _document.LastFootprintEditMessage;
        SetAnchorButton.IsEnabled = _document.SelectedHex is { Q: >= 0 };
        SetPresenceButton.Visibility = Visibility.Collapsed;
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

    void SetPresence_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_document.SelectedSiteId) || _document.SelectedHex is not { Q: >= 0 } hex)
            return;
        var result = _document.SetSitePresence(_document.SelectedSiteId, hex);
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
        var errors = HexWorldContentValidator.Validate(_document.World).Where(i => i.Level == "error").ToList();
        if (errors.Count > 0) { MessageBox.Show(string.Join(Environment.NewLine, errors.Take(12).Select(i => i.Message)), "Territory / HexWorld 校验失败，已禁止保存", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
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
        var errors = HexWorldContentValidator.Validate(_document.World).Where(i => i.Level == "error").ToList();
        if (errors.Count > 0) { MessageBox.Show(string.Join(Environment.NewLine, errors.Take(12).Select(i => i.Message)), "Territory / HexWorld 校验失败，已禁止保存", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
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
        if (sender is ToggleButton clicked)
        {
            ToolSelect.IsChecked = ReferenceEquals(clicked, ToolSelect); ToolTerrain.IsChecked = ReferenceEquals(clicked, ToolTerrain);
            ToolRoad.IsChecked = ReferenceEquals(clicked, ToolRoad); ToolSite.IsChecked = ReferenceEquals(clicked, ToolSite); ToolErase.IsChecked = ReferenceEquals(clicked, ToolErase);
        }
        if (ToolSelect.IsChecked == true) _document.ActiveTool = HexEditorTool.Select;
        else if (ToolTerrain.IsChecked == true) _document.ActiveTool = HexEditorTool.Terrain;
        else if (ToolRoad.IsChecked == true) _document.ActiveTool = HexEditorTool.Road;
        else if (ToolSite.IsChecked == true) _document.ActiveTool = HexEditorTool.Site;
        else if (ToolErase.IsChecked == true) _document.ActiveTool = HexEditorTool.Erase;
        SiteToolPanel.Visibility = _document.ActiveTool == HexEditorTool.Site ? Visibility.Visible : Visibility.Collapsed;
        _sitePlacementArmed = false;
    }

    void ArmNewSite_Click(object sender, RoutedEventArgs e) { _sitePlacementArmed = true; StatusText.Text = "新建 WorldSite：请点击地图上的一个 Hex 作为 Anchor，Esc 取消。"; }

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
        var site = _document.GetSelectedSite();
        if (site == null) return;
        var regionNote = string.IsNullOrEmpty(site.TerritoryRegionId) ? string.Empty : "\n该 WorldSite 对应的 TerritoryRegion 也将删除。";
        if (MessageBox.Show($"确定删除 WorldSite？\n名称：{site.DisplayName}\nSiteId：{site.SiteId}\nFootprint：{site.Footprint.Count} Hex{regionNote}", "删除 WorldSite", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        _document.DeleteSite(_document.SelectedSiteId);
        _document.EditFootprintMode = false; FootprintModeBox.IsChecked = false;
    }

    void SiteFields_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_document.SelectedSiteId))
            return;
        var site = _document.World.Sites.FirstOrDefault(s => s.SiteId == _document.SelectedSiteId);
        if (site == null)
            return;
        _document.RenameSelectedSite(_document.SelectedSiteId, SiteIdBox.Text.Trim(), SiteNameBox.Text.Trim(), SiteTypeBox.Text.Trim(), SiteLocalMapBox.Text.Trim());
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
        if (e.ChangedButton == MouseButton.Right && _territoryErasing)
        {
            _territoryErasing = false; _mapView.ReleaseMouseCapture(); _strokeUndoPushed = false;
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
        {
            if (_territoryEditMode) ApplyTerritoryPaint(hex);
            else ApplyTool(hex, false);
        }
        if (_territoryErasing && e.RightButton == MouseButtonState.Pressed && hex.Q >= 0)
            ApplyTerritoryErase(hex);
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

        if (_territoryEditMode)
        {
            _painting = true; _strokeUndoPushed = false; _mapView.CaptureMouse(); ApplyTerritoryPaint(hex); return;
        }

        if (_document.ActiveTool is HexEditorTool.Terrain or HexEditorTool.Road or HexEditorTool.Erase)
        {
            _painting = true;
            _strokeUndoPushed = false;
            _mapView.CaptureMouse();
            ApplyTool(hex, true);
            return;
        }

        if (_document.ActiveTool == HexEditorTool.Site && _sitePlacementArmed)
        {
            var newSite = _document.CreateSite(hex); _sitePlacementArmed = false;
            StatusText.Text = $"已创建 WorldSite「{newSite.DisplayName}」，Anchor=({hex.Q},{hex.R})。";
            UpdateInspector();
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
        if (_territoryEditMode)
        {
            var territoryHex = _viewport.ScreenToHex(e.GetPosition(_mapView).X, e.GetPosition(_mapView).Y, _document.World.Width, _document.World.Height);
            if (territoryHex.Q >= 0) { _territoryErasing = true; _strokeUndoPushed = false; _mapView.CaptureMouse(); ApplyTerritoryErase(territoryHex); }
            e.Handled = true; return;
        }
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
        {
            if (e.ChangedButton == MouseButton.Right && _territoryErasing) { _territoryErasing = false; _mapView.ReleaseMouseCapture(); _strokeUndoPushed = false; }
            return;
        }
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

    void ApplyTerritoryPaint(HexCoordDto hex)
    {
        if (string.IsNullOrEmpty(_selectedFactionId)) { StatusText.Text = "请先选择势力。"; return; }
        if (!_strokeUndoPushed) { _document.PushUndo(); _strokeUndoPushed = true; }
        var site = _document.TryResolveDefaultSiteTerritoryAtHex(hex);
        if (site == null) { StatusText.Text = "该荒野 Hex 当前没有正式 standalone claim Content authority，未修改。"; return; }
        var result = _document.AssignFactionToSiteTerritory(site.SiteId, _selectedFactionId, false);
        if (!result.Success) StatusText.Text = result.Message; else StatusText.Text = result.Message;
    }

    void ApplyTerritoryErase(HexCoordDto hex)
    {
        if (!_strokeUndoPushed) { _document.PushUndo(); _strokeUndoPushed = true; }
        var site = _document.TryResolveDefaultSiteTerritoryAtHex(hex);
        if (site == null) { StatusText.Text = "该荒野 Hex 当前没有正式 standalone claim Content authority，未修改。"; return; }
        var result = _document.AssignFactionToSiteTerritory(site.SiteId, string.Empty, false);
        StatusText.Text = result.Message;
    }

    void SidebarTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource != SidebarTabs) return;
        _territoryEditMode = SidebarTabs.SelectedIndex == 1;
        _mapView.SetTerritoryOverlay(null, _territoryEditMode);
        RefreshTerritoryPanel();
    }

    void RefreshTerritoryPanel()
    {
        if (TerritoryList == null) return;
        var filter = TerritorySearchBox.Text?.Trim() ?? string.Empty;
        _territoryItems.Clear();
        var factions = _document.World.Sites.Select(s => s.OwnerFactionId)
            .Concat(_document.World.TerritoryRegions.Select(r => r.ControlFactionId))
            .Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal);
        foreach (var factionId in factions)
        {
            if (!string.IsNullOrEmpty(filter) && !factionId.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
            _territoryItems.Add(new FactionListItem(factionId));
        }
        TerritoryList.SelectedItem = _territoryItems.FirstOrDefault(x => x.FactionId == _selectedFactionId);
        TerritoryCurrentText.Text = string.IsNullOrEmpty(_selectedFactionId) ? "请选择一个势力。" : $"当前笔刷：{_selectedFactionId}\n左键/拖涂势力范围；右键/拖清除。\nWorldSite 本体或外围一圈会自动处理整块辖区。";
    }

    void TerritorySearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshTerritoryPanel();
    void TerritoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var item = TerritoryList.SelectedItem as FactionListItem; _selectedFactionId = item?.FactionId;
        _mapView.SetTerritoryOverlay(null, _territoryEditMode); RefreshTerritoryPanel();
    }
    void ValidateTerritory_Click(object sender, RoutedEventArgs e) => Validate_Click(sender, e);

    void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _sitePlacementArmed) { _sitePlacementArmed = false; StatusText.Text = "已取消新建 WorldSite。"; e.Handled = true; return; }
        if (e.Key == Key.Escape && _document.EditFootprintMode) { _document.EditFootprintMode = false; FootprintModeBox.IsChecked = false; StatusText.Text = "已完成 Footprint 编辑。"; e.Handled = true; return; }
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

sealed class FactionListItem
{
    public string FactionId { get; }
    public string Display => $"■ {FactionId}";
    public FactionListItem(string factionId) => FactionId = factionId;
}
