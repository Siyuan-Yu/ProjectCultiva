using System.IO;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
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
    bool _refreshingFactionList;
    bool _sitePlacementArmed;
    string? _selectedFactionId;
    TerritoryBrushKind _territoryBrushKind;
    readonly List<StrategicFactionAuthoringDto> _allFactions = new();
    readonly Dictionary<string, StrategicFactionAuthoringDto> _factionById = new(StringComparer.Ordinal);
    string? _baseGameRoot;
    readonly HashSet<string> _sitesPaintedThisStroke = new(StringComparer.Ordinal);

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
        _baseGameRoot = root;
        LoadFactions();
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

    /// <summary>factions.json → 内存目录（全部 + territorySelectable 筛选列表）；失败时保留空表并提示。</summary>
    void LoadFactions()
    {
        _allFactions.Clear();
        _factionById.Clear();
        if (string.IsNullOrEmpty(_baseGameRoot))
        {
            _allFactions.AddRange(StrategicFactionAuthoring.LoadStrategicFactions(DefaultFactionFilePathFallback()));
        }
        else
        {
            _allFactions.AddRange(StrategicFactionAuthoring.LoadStrategicFactions(
                StrategicFactionAuthoring.FactionDefaultFilePath(_baseGameRoot)));
        }

        foreach (var f in _allFactions)
            _factionById[f.Id] = f;
        _allFactions.Sort(StrategicFactionAuthoring.Compare);
        _mapView.SetFactionColors(_allFactions.Select(f => new KeyValuePair<string, string>(f.Id, f.MapColor)));
        if (_territoryBrushKind == TerritoryBrushKind.Faction &&
            (_selectedFactionId == null || !_factionById.ContainsKey(_selectedFactionId)))
        {
            _selectedFactionId = null;
            _territoryBrushKind = TerritoryBrushKind.None;
        }
        RefreshTerritoryPanel();
        UpdateBrushHeader();
    }

    static string DefaultFactionFilePathFallback()
    {
        var root = PackagePaths.FindDefaultBaseGame();
        return root == null ? string.Empty : StrategicFactionAuthoring.FactionDefaultFilePath(root);
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
        _mapView.SetTerritoryOverlay(_selectedFactionId, _territoryEditMode);
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

    void OnTerritoriesMutated()
    {
        _mapView.RebuildTerritoryOverlay();
        UpdateInspector();
        UpdateValidationSummary();
        // 不刷新 faction 列表：列表只来自 factions.json（LoadFactions），与 hexWorld 涂刷无关。
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
            InspectorText.Text = $"坐标 ({hex.Q},{hex.R}) — 无 Cell 数据";
            return;
        }

        var passable = cell.Passable ?? HexTerrainPalette.DefaultPassable(cell.Terrain);
        var site = _document.FindSiteAt(hex);
        var defaultSite = _document.TryResolveDefaultSiteTerritoryAtHex(hex);
        var territory = _document.FindTerritoryAt(hex);
        var standalone = _document.FindStandaloneAt(hex);

        // 领地类型：无 / 独立势力范围 / WorldSite 辖区（本体）/ WorldSite 辖区（默认外围）/ 固化 Region 微调格
        string terrainKind;
        string controllerId = string.Empty;
        if (site != null)
        {
            terrainKind = "WorldSite 辖区（本体）";
            controllerId = site.OwnerFactionId ?? string.Empty;
        }
        else if (territory != null)
        {
            terrainKind = "WorldSite 辖区";
            controllerId = territory.ControlFactionId ?? string.Empty;
        }
        else if (defaultSite != null)
        {
            terrainKind = "WorldSite 辖区（默认外围）";
            var ds = _document.World.Sites.FirstOrDefault(s => s.SiteId == defaultSite.SiteId);
            controllerId = ds?.OwnerFactionId ?? string.Empty;
        }
        else if (standalone != null)
        {
            terrainKind = "独立势力范围";
            controllerId = standalone.ControlFactionId ?? string.Empty;
        }
        else
        {
            terrainKind = "无";
        }

        var controllerDisplay = string.IsNullOrEmpty(controllerId)
            ? "无"
            : $"{FactionDisplayName(controllerId)}\n{controllerId}";
        var regionDisplay = territory != null
            ? $"{territory.RegionId}\n辖区核心：{SiteDisplayName(territory.PrimaryWorldSiteId)}"
            : "无";

        InspectorText.Text =
            $"坐标：({hex.Q},{hex.R})\n" +
            $"地形：{HexTerrainPalette.ResolveLabel(cell.Terrain)} / {cell.Terrain}\n" +
            $"可通行：{(passable ? "是" : "否")}\n" +
            $"道路：{(cell.IsRoad ? "是" : "否")}\n" +
            $"地点：{(site?.DisplayName ?? "-")}\n" +
            $"领地类型：{terrainKind}\n" +
            $"控制势力：{controllerDisplay}\n" +
            $"领地区域：{regionDisplay}";
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

    string FactionDisplayName(string factionId)
    {
        if (string.IsNullOrEmpty(factionId))
            return "无归属";
        return _factionById.TryGetValue(factionId, out var f) ? f.Name : factionId;
    }

    string SiteDisplayName(string siteId)
    {
        if (string.IsNullOrEmpty(siteId))
            return "无";
        var site = _document.World.Sites.FirstOrDefault(s => s.SiteId == siteId);
        return site?.DisplayName ?? siteId;
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
        var ownerDisplay = string.IsNullOrWhiteSpace(site.OwnerFactionId)
            ? "无"
            : $"{FactionDisplayName(site.OwnerFactionId)}\n{site.OwnerFactionId}";
        SiteTerritoryText.Text = $"所属势力：{ownerDisplay}\n领地区域 ID：{site.TerritoryRegionId}\n辖区格数：{territory?.Hexes.Count ?? 0}";
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
        var issues = ValidateCurrentWorld();
        ValidationText.Text = issues.Count == 0
            ? "Validation: OK"
            : string.Join("\n", issues.Take(8).Select(i => $"[{i.Level}] {i.Message}"));
    }

    List<HexWorldValidationIssue> ValidateCurrentWorld() =>
        HexWorldContentValidator.Validate(_document.World, _allFactions);

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
        var errors = ValidateCurrentWorld().Where(i => i.Level == "error").ToList();
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
        var errors = ValidateCurrentWorld().Where(i => i.Level == "error").ToList();
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
        var issues = ValidateCurrentWorld();
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
            _sitesPaintedThisStroke.Clear();
        }
        if (e.ChangedButton == MouseButton.Right && _territoryErasing)
        {
            _territoryErasing = false; _mapView.ReleaseMouseCapture(); _strokeUndoPushed = false; _sitesPaintedThisStroke.Clear();
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
        if (hoverChanged && _territoryEditMode)
            UpdateTerritoryHoverPreview();

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
            if (e.ChangedButton == MouseButton.Right && _territoryErasing) { _territoryErasing = false; _mapView.ReleaseMouseCapture(); _strokeUndoPushed = false; _sitesPaintedThisStroke.Clear(); }
            return;
        }
        _painting = false;
        _mapView.ReleaseMouseCapture();
        _strokeUndoPushed = false;
        _sitesPaintedThisStroke.Clear();
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
        if (_territoryBrushKind == TerritoryBrushKind.Unowned)
        {
            ApplyTerritoryErase(hex);
            return;
        }

        if (_territoryBrushKind != TerritoryBrushKind.Faction || string.IsNullOrEmpty(_selectedFactionId))
        {
            SetTerritoryHint("请先选择势力，或选择「无势力 / 无主地」。");
            return;
        }
        if (!_factionById.ContainsKey(_selectedFactionId))
        {
            SetTerritoryHint($"势力 '{_selectedFactionId}' 未在 factions.json 中定义。");
            return;
        }

        // §18：一次 stroke 内，同一 WorldSite 只执行一次整片 macro（不论拖过它几个 footprint/ring hex）。
        var defaultSite = _document.TryResolveDefaultSiteTerritoryAtHex(hex);
        if (defaultSite != null && _sitesPaintedThisStroke.Contains(defaultSite.SiteId))
            return;

        if (!_strokeUndoPushed) { _document.PushUndo(); _strokeUndoPushed = true; }
        var result = _document.PaintTerritory(hex, _selectedFactionId, singleStrokeUndo: false);
        if (result.Success && result.SiteId != null)
            _sitesPaintedThisStroke.Add(result.SiteId);
        SetTerritoryHint(result.Message);
    }

    void ApplyTerritoryErase(HexCoordDto hex)
    {
        var defaultSite = _document.TryResolveDefaultSiteTerritoryAtHex(hex);
        if (defaultSite != null && _sitesPaintedThisStroke.Contains(defaultSite.SiteId))
            return;
        if (!_strokeUndoPushed) { _document.PushUndo(); _strokeUndoPushed = true; }
        var result = _document.EraseTerritory(hex, singleStrokeUndo: false);
        if (result.Success && result.SiteId != null)
            _sitesPaintedThisStroke.Add(result.SiteId);
        SetTerritoryHint(result.Message);
    }

    void SetTerritoryHint(string message)
    {
        StatusText.Text = message + " · " + _mapView.FormatPerfStatus();
        if (TerritoryHintText != null)
            TerritoryHintText.Text = message;
    }

    void SidebarTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource != SidebarTabs) return;
        _territoryEditMode = SidebarTabs.SelectedIndex == 1;
        _mapView.SetTerritoryOverlay(_selectedFactionId, _territoryEditMode);
        RefreshTerritoryPanel();
        UpdateTerritoryHoverPreview();
    }

    /// <summary>
    /// 势力列表刷新：从正式 factions.json（territorySelectable）读取；不因 SelectionChanged 再刷新。
    /// 任何 selection 变化都不会重建本列表 —— 重复 bug 根因（SelectionChanged→Refresh→重建→又触发 SelectionChanged）。
    /// </summary>
    void RefreshTerritoryPanel()
    {
        if (TerritoryList == null) return;
        var filter = TerritorySearchBox.Text?.Trim() ?? string.Empty;
        _refreshingFactionList = true;
        try
        {
            _territoryItems.Clear();
            // 固定的编辑器工具项，不属于 factions.json，也绝不进入势力管理器。
            _territoryItems.Add(FactionListItem.Unowned());
            foreach (var faction in _allFactions)
            {
                if (!faction.TerritorySelectable)
                    continue;
                if (filter.Length > 0 && !MatchesFactionFilter(faction, filter))
                    continue;
                _territoryItems.Add(FactionListItem.FromDto(faction));
            }

            // 恢复当前选择（仅当仍可见）；不强改 _selectedFactionId 之外的状态。
            var keep = _territoryBrushKind == TerritoryBrushKind.Unowned
                ? _territoryItems.FirstOrDefault(x => x.IsUnowned)
                : _territoryItems.FirstOrDefault(x => x.FactionId == _selectedFactionId);
            TerritoryList.SelectedItem = keep;
        }
        finally
        {
            _refreshingFactionList = false;
        }

        UpdateBrushHeader();
        TerritoryHintText.Text = _allFactions.Count == 0
            ? "未找到 factions.json（Content/BaseGame/Data/Factions/factions.json）。"
            : string.Empty;
    }

    static bool MatchesFactionFilter(StrategicFactionAuthoringDto faction, string filter)
    {
        return faction.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
               faction.Id.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    void UpdateBrushHeader()
    {
        if (TerritoryBrushName == null)
            return;

        if (_territoryBrushKind == TerritoryBrushKind.Unowned)
        {
            TerritoryBrushName.Text = "□ 无势力 / 无主地";
            TerritoryBrushId.Text = "清除势力控制";
            TerritoryBrushSwatch.Fill = Brushes.Transparent;
            TerritoryBrushSwatch.Stroke = new SolidColorBrush(Color.FromRgb(0x77, 0x82, 0x91));
            TerritoryBrushInstruction.Text = "左键 / 左拖：清除势力控制。右键 / 右拖：同样清除。涂到 WorldSite 本体或外围一圈会将整个 Site 辖区设为无主，但保留辖区结构。";
            return;
        }

        if (_territoryBrushKind != TerritoryBrushKind.Faction || string.IsNullOrEmpty(_selectedFactionId))
        {
            TerritoryBrushName.Text = "未选择";
            TerritoryBrushId.Text = "选择下方势力后点击地图涂色";
            TerritoryBrushSwatch.Fill = new SolidColorBrush(Color.FromRgb(0xB3, 0x94, 0x5C));
            TerritoryBrushSwatch.Stroke = Brushes.Transparent;
            TerritoryBrushInstruction.Text = "左键 / 左拖：涂当前势力。右键 / 右拖：清除势力。涂到 WorldSite 本体或外围一圈会自动更新整个 Site 辖区。";
            return;
        }

        var faction = _factionById.TryGetValue(_selectedFactionId, out var f) ? f : null;
        if (faction == null)
        {
            TerritoryBrushName.Text = _selectedFactionId;
            TerritoryBrushId.Text = string.Empty;
            TerritoryBrushSwatch.Fill = new SolidColorBrush(Color.FromRgb(0xB3, 0x94, 0x5C));
            TerritoryBrushSwatch.Stroke = Brushes.Transparent;
            return;
        }

        TerritoryBrushName.Text = faction.Name;
        TerritoryBrushId.Text = faction.Id;
        TerritoryBrushSwatch.Fill = BrushFromHex(faction.MapColor);
        TerritoryBrushSwatch.Stroke = Brushes.Transparent;
        TerritoryBrushInstruction.Text = "左键 / 左拖：涂当前势力。右键 / 右拖：清除势力。涂到 WorldSite 本体或外围一圈会自动更新整个 Site 辖区。";
    }

    static SolidColorBrush BrushFromHex(string hex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
        catch
        {
            var brush = new SolidColorBrush(Color.FromRgb(0xB3, 0x94, 0x5C));
            brush.Freeze();
            return brush;
        }
    }

    void TerritorySearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshTerritoryPanel();

    void TerritoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshingFactionList)
            return;
        var item = TerritoryList.SelectedItem as FactionListItem;
        _territoryBrushKind = item?.IsUnowned == true
            ? TerritoryBrushKind.Unowned
            : item == null ? TerritoryBrushKind.None : TerritoryBrushKind.Faction;
        _selectedFactionId = _territoryBrushKind == TerritoryBrushKind.Faction ? item!.FactionId : null;
        UpdateBrushHeader();
        _mapView.SetTerritoryOverlay(_selectedFactionId, _territoryEditMode);
        UpdateTerritoryHoverPreview();
    }

    void ValidateTerritory_Click(object sender, RoutedEventArgs e) => Validate_Click(sender, e);

    void ManageFactions_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_baseGameRoot))
        {
            MessageBox.Show("未找到 Content/BaseGame，无法管理势力。", "势力管理", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var path = StrategicFactionAuthoring.FactionDefaultFilePath(_baseGameRoot);
        var manager = new FactionManagerWindow(_document.World, _baseGameRoot, path);
        manager.Owner = this;
        var saved = manager.ShowDialog() == true;
        if (saved)
            LoadFactions();
    }

    void EditOpeningStrategic_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_baseGameRoot))
        {
            MessageBox.Show("未找到 Content/BaseGame，无法编辑开局战略。", "开局战略", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        new OpeningStrategicEditorWindow(_baseGameRoot) { Owner = this }.ShowDialog();
    }

    void UpdateTerritoryHoverPreview()
    {
        if (!_territoryEditMode || _territoryBrushKind == TerritoryBrushKind.None || !_mapView.HoverHex.HasValue)
        {
            _mapView.SetBrushPreview(null, default);
            return;
        }

        var hex = _mapView.HoverHex.Value;
        if (hex.Q < 0)
        {
            _mapView.SetBrushPreview(null, default);
            return;
        }

        var color = _territoryBrushKind == TerritoryBrushKind.Unowned
            ? Color.FromRgb(0x9A, 0xA4, 0xB2)
            : _selectedFactionId != null && _factionById.TryGetValue(_selectedFactionId, out var faction)
                ? (Color)ColorConverter.ConvertFromString(faction.MapColor)
                : default;
        if (color == default)
        {
            _mapView.SetBrushPreview(null, default);
            return;
        }
        var site = _document.TryResolveDefaultSiteTerritoryAtHex(hex);
        if (site != null)
        {
            var hexes = _document.ComputeDefaultSiteTerritory(site).ToList();
            _mapView.SetBrushPreview(hexes, color);
        }
        else
        {
            _mapView.SetBrushPreview(new[] { hex }, color);
        }
    }


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

enum TerritoryBrushKind
{
    None,
    Unowned,
    Faction,
}

sealed class FactionListItem
{
    public string FactionId { get; }
    public string Name { get; }
    public string Id => FactionId;
    public Brush Brush { get; }
    public bool IsUnowned { get; }
    public string Display => Name;

    FactionListItem(StrategicFactionAuthoringDto dto)
    {
        FactionId = dto.Id;
        Name = dto.Name;
        Brush = BrushFromHexSafe(dto.MapColor);
    }

    FactionListItem()
    {
        FactionId = string.Empty;
        Name = "无势力 / 无主地";
        Brush = Brushes.Transparent;
        IsUnowned = true;
    }

    public static FactionListItem FromDto(StrategicFactionAuthoringDto dto) => new(dto);
    public static FactionListItem Unowned() => new();

    static SolidColorBrush BrushFromHexSafe(string hex)
    {
        try
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }
        catch
        {
            var brush = new SolidColorBrush(Color.FromRgb(0xB3, 0x94, 0x5C));
            brush.Freeze();
            return brush;
        }
    }
}
