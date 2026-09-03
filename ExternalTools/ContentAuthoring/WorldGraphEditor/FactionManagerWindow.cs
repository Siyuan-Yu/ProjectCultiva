using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ContentAuthoring.Shared;
using ContentAuthoring.Shared.HexWorld;

namespace WorldGraphEditor;

/// <summary>
/// 势力管理窗口：直接编辑正式 Content/BaseGame/Data/Factions/factions.json。
/// 列表 = 全部势力（不止 territorySelectable）；新建 / 改名 / 改色 / 删除（引用保护）。
/// 保存经 ContentAuthoring.Shared StrategicFactionAuthoring.SaveStrategicFactions（写盘 roundtrip）。
/// </summary>
public sealed class FactionManagerWindow : Window
{
    readonly HexWorldDefinitionDto _currentWorld;
    readonly string _baseGameRoot;
    readonly string _factionsFilePath;
    readonly ListBox _factionList = new();
    readonly List<StrategicFactionAuthoringDto> _all = new();
    readonly Dictionary<string, string> _referenceCache = new(StringComparer.Ordinal);
    bool _loadingSelection;
    bool _modified;

    // 编辑字段
    readonly TextBox _nameBox = new() { MinWidth = 220 };
    readonly TextBox _idBox = new() { MinWidth = 220 };
    readonly TextBox _colorBox = new() { MinWidth = 120 };
    readonly CheckBox _selectableBox = new() { IsChecked = true, Content = "可拥有领土（territorySelectable）" };
    readonly TextBox _sortBox = new() { MinWidth = 60, Text = "0" };
    readonly TextBlock _colorPreview = new() { Width = 42, Height = 18 };
    readonly TextBlock _referenceNote = new() { Foreground = Brushes.DarkOrange, TextWrapping = TextWrapping.Wrap, MaxWidth = 300 };

    public FactionManagerWindow(HexWorldDefinitionDto currentWorld, string baseGameRoot, string factionsFilePath)
    {
        _currentWorld = currentWorld;
        _baseGameRoot = baseGameRoot;
        _factionsFilePath = factionsFilePath;
        Title = "势力管理 — factions.json";
        Width = 760;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (Brush)FindResource("Editor.Background");

        _all.AddRange(StrategicFactionAuthoring.LoadStrategicFactions(factionsFilePath));
        _all.Sort(StrategicFactionAuthoring.Compare);
        BuildUi();
        ReloadList(selectId: null);
    }

    void BuildUi()
    {
        var root = new DockPanel { Margin = new Thickness(10) };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        var saveBtn = new Button { Content = "保存修改", Width = 96, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        saveBtn.Click += (_, _) => SaveAndClose();
        var cancelBtn = new Button { Content = "关闭", Width = 72 };
        cancelBtn.Click += (_, _) =>
        {
            if (_modified)
            {
                var answer = MessageBox.Show(this, "有未保存的势力修改，确定放弃并关闭？", "势力管理",
                    MessageBoxButton.OKCancel, MessageBoxImage.Question);
                if (answer != MessageBoxResult.OK)
                    return;
            }

            DialogResult = false;
        };
        buttons.Children.Add(saveBtn);
        buttons.Children.Add(cancelBtn);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);

        var left = new DockPanel { Width = 230, Margin = new Thickness(0, 0, 12, 0) };
        DockPanel.SetDock(left, Dock.Left);
        root.Children.Add(left);

        var addBtn = new Button { Content = "+ 新建势力", Height = 26, Margin = new Thickness(0, 0, 6, 0) };
        addBtn.Click += (_, _) => AddNewFaction();
        var delBtn = new Button { Content = "删除势力", Height = 26 };
        delBtn.Click += (_, _) => DeleteSelected();
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        toolbar.Children.Add(addBtn);
        toolbar.Children.Add(delBtn);
        DockPanel.SetDock(toolbar, Dock.Top);
        left.Children.Add(toolbar);

        _factionList.SelectionChanged += (_, _) => OnSelectionChanged();
        left.Children.Add(_factionList);

        var right = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        root.Children.Add(right);

        right.Children.Add(new TextBlock
        {
            Text = "势力身份（factions.json）",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8),
        });
        right.Children.Add(FieldLabel("名称"));
        right.Children.Add(_nameBox);
        right.Children.Add(FieldLabel("ID（新建未保存前可改；已被 Content 引用则只读）"));
        right.Children.Add(_idBox);
        right.Children.Add(FieldLabel("地图颜色 #RRGGBB"));
        var colorRow = new StackPanel { Orientation = Orientation.Horizontal };
        colorRow.Children.Add(_colorBox);
        var previewBorder = new Border
        {
            Child = _colorPreview,
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Color.FromRgb(0xB3, 0x94, 0x5C)),
        };
        colorRow.Children.Add(previewBorder);
        right.Children.Add(colorRow);
        _colorBox.TextChanged += (_, _) => RefreshColorPreview();
        right.Children.Add(FieldLabel(""));
        right.Children.Add(_selectableBox);
        right.Children.Add(FieldLabel("排序（sortOrder，小在前）"));
        right.Children.Add(_sortBox);
        right.Children.Add(_referenceNote);

        _nameBox.TextChanged += (_, _) => { _modified = true; _referenceNote.Text = "有未保存修改，请点「保存修改」写回 factions.json。"; };
        _idBox.TextChanged += (_, _) => { _modified = true; _referenceNote.Text = "有未保存修改，请点「保存修改」写回 factions.json。"; };
        _colorBox.TextChanged += (_, _) => { _modified = true; _referenceNote.Text = "有未保存修改，请点「保存修改」写回 factions.json。"; };
        _selectableBox.Checked += (_, _) => { _modified = true; };
        _selectableBox.Unchecked += (_, _) => { _modified = true; };
        _sortBox.TextChanged += (_, _) => { _modified = true; };

        Content = root;
    }

    static TextBlock FieldLabel(string text) => new()
    {
        Text = text,
        Foreground = Brushes.Gray,
        Margin = new Thickness(0, 8, 0, 3),
        FontSize = 11,
        TextWrapping = TextWrapping.Wrap,
    };

    void ReloadList(string? selectId)
    {
        _factionList.ItemsSource = null;
        _factionList.Items.Clear();
        foreach (var f in _all)
        {
            _factionList.Items.Add(new FactionManagerListItem(f));
        }

        if (selectId != null)
        {
            foreach (var item in _factionList.Items)
            {
                if (item is FactionManagerListItem fi && fi.FactionId == selectId)
                {
                    _factionList.SelectedItem = item;
                    break;
                }
            }
        }

        _referenceNote.Text = string.Empty;
    }

    void OnSelectionChanged()
    {
        if (_loadingSelection)
            return;
        if (_factionList.SelectedItem is not FactionManagerListItem item)
        {
            ClearFields();
            return;
        }

        _loadingSelection = true;
        _nameBox.Text = item.Name;
        _idBox.Text = item.FactionId;
        _colorBox.Text = item.MapColor;
        _selectableBox.IsChecked = item.TerritorySelectable;
        _sortBox.Text = item.SortOrder.ToString();
        RefreshColorPreview();

        var referenced = IsReferenced(item.FactionId);
        _idBox.IsEnabled = !referenced;
        _referenceNote.Text = referenced
            ? $"⚠ 「{item.Name}」仍被 Content 引用，ID 只读；如需改名请先解除引用。"
            : string.Empty;
        _loadingSelection = false;
    }

    void ClearFields()
    {
        _loadingSelection = true;
        _nameBox.Text = string.Empty;
        _idBox.Text = string.Empty;
        _colorBox.Text = string.Empty;
        _selectableBox.IsChecked = true;
        _sortBox.Text = "0";
        _referenceNote.Text = string.Empty;
        _loadingSelection = false;
    }

    void AddNewFaction()
    {
        var baseId = "base:faction_new";
        var id = baseId;
        var n = 2;
        while (_all.Any(f => string.Equals(f.Id, id, StringComparison.Ordinal)))
            id = baseId + "_" + n++;
        var nextSort = _all.Count == 0 ? 10 : _all.Max(f => f.SortOrder) + 10;
        var fresh = new StrategicFactionAuthoringDto
        {
            Id = id,
            Name = "新势力",
            MapColor = "#B3945C",
            TerritorySelectable = true,
            SortOrder = nextSort,
        };
        _all.Add(fresh);
        _all.Sort(StrategicFactionAuthoring.Compare);
        _modified = true;
        ReloadList(selectId: fresh.Id);
        OnSelectionChanged();
        _nameBox.Focus();
        _nameBox.SelectAll();
    }

    void DeleteSelected()
    {
        if (_factionList.SelectedItem is not FactionManagerListItem item)
            return;
        if (IsReferenced(item.FactionId))
        {
            var hits = CollectReferences(item.FactionId);
            var detail = hits.Count == 0
                ? "（引用来自当前打开的 hexWorld 之外）"
                : string.Join(Environment.NewLine, hits.Take(10).Select(h => "- " + h));
            MessageBox.Show(
                this,
                $"无法删除「{item.Name}」\n\n仍被以下 Content 引用：\n{detail}",
                "势力管理 — 删除被阻止",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (MessageBox.Show(this, $"确定删除势力「{item.Name}」（{item.FactionId}）？\n删除后需要点「保存修改」写盘。", "势力管理",
                MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            return;
        _all.RemoveAll(f => string.Equals(f.Id, item.FactionId, StringComparison.Ordinal));
        _referenceCache.Remove(item.FactionId);
        _modified = true;
        ReloadList(selectId: null);
        _referenceNote.Text = "已从列表移除；请点「保存修改」写回 factions.json。";
    }

    void SaveAndClose()
    {
        if (_factionList.SelectedItem is FactionManagerListItem selected)
        {
            var idx = _all.FindIndex(f => string.Equals(f.Id, selected.FactionId, StringComparison.Ordinal));
            if (idx >= 0)
            {
                var current = _all[idx];
                var idEdited = _idBox.Text.Trim();
                if (!string.Equals(current.Id, idEdited, StringComparison.Ordinal) && IsReferenced(current.Id))
                {
                    MessageBox.Show(this, $"「{current.Name}」仍被 Content 引用，ID 不能修改。", "势力管理",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _all[idx] = current.With(
                    id: idEdited,
                    name: _nameBox.Text.Trim(),
                    mapColor: _colorBox.Text.Trim(),
                    territorySelectable: _selectableBox.IsChecked == true,
                    sortOrder: int.TryParse(_sortBox.Text.Trim(), out var sort) ? sort : current.SortOrder);
            }
        }

        var errors = StrategicFactionAuthoring.Validate(_all);
        if (errors.Count > 0)
        {
            MessageBox.Show(this,
                "无法保存：\n" + string.Join(Environment.NewLine, errors),
                "势力管理 — 校验失败",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        StrategicFactionAuthoring.SaveStrategicFactions(_factionsFilePath, _all);
        DialogResult = true;
    }

    /// <summary>已引用缓存：当前 hexWorld 内存 + 全包 hexWorld/Armies/Scenarios/Rosters。</summary>
    bool IsReferenced(string factionId)
    {
        if (_referenceCache.TryGetValue(factionId, out var cached))
            return !string.IsNullOrEmpty(cached);
        var hits = CollectReferences(factionId);
        _referenceCache[factionId] = hits.Count == 0 ? string.Empty : string.Join("|", hits);
        return hits.Count > 0;
    }

    List<string> CollectReferences(string factionId)
    {
        var hits = new List<string>();
        if (_currentWorld != null)
        {
            foreach (var site in _currentWorld.Sites)
            {
                if (string.Equals(site.OwnerFactionId, factionId, StringComparison.Ordinal))
                    hits.Add($"{_currentWorld.Id ?? "hexWorld"} / {site.SiteId} (ownerFactionId)");
            }

            foreach (var region in _currentWorld.TerritoryRegions)
            {
                if (string.Equals(region.ControlFactionId, factionId, StringComparison.Ordinal))
                    hits.Add($"{_currentWorld.Id ?? "hexWorld"} / {region.RegionId} (controlFactionId)");
            }

            foreach (var control in _currentWorld.StandaloneTerritoryHexes)
            {
                if (string.Equals(control.ControlFactionId, factionId, StringComparison.Ordinal))
                    hits.Add($"{_currentWorld.Id ?? "hexWorld"} / hex ({control.Q},{control.R}) (standalone)");
            }
        }

        var dataDir = Path.Combine(_baseGameRoot, "Data");
        foreach (var hit in FactionReferenceScanner.ScanPackage(dataDir, factionId))
            hits.Add($"{hit.FilePath} :: {hit.Key} = {hit.Value}");

        return hits;
    }

    void RefreshColorPreview()
    {
        try
        {
            _colorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_colorBox.Text.Trim()));
        }
        catch
        {
            _colorPreview.Background = new SolidColorBrush(Color.FromRgb(0xB3, 0x94, 0x5C));
        }
    }
}

sealed class FactionManagerListItem
{
    public string FactionId { get; }
    public string Name { get; }
    public string MapColor { get; }
    public bool TerritorySelectable { get; }
    public int SortOrder { get; }
    public string SortLabel { get; }

    public FactionManagerListItem(StrategicFactionAuthoringDto dto)
    {
        FactionId = dto.Id;
        Name = dto.Name;
        MapColor = dto.MapColor;
        TerritorySelectable = dto.TerritorySelectable;
        SortOrder = dto.SortOrder;
        SortLabel = dto.SortOrder.ToString();
    }

    public override string ToString() => $"{Name}  ({FactionId})  · sort {SortOrder}{(TerritorySelectable ? string.Empty : " · 无领土")}";
}
