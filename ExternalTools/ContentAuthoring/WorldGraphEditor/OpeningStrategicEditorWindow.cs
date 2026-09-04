using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using ContentAuthoring.Shared;

namespace WorldGraphEditor;

/// <summary>
/// OpeningScenario 的开局战略 Content Authoring 窗口。
/// 只编辑场景 Raw JSON 的 strategicOpening；不连接 Unity、不触碰 SaveGame 或 Runtime StrategicBoard。
/// </summary>
public sealed class OpeningStrategicEditorWindow : Window
{
    static readonly Brush PrimaryText = new SolidColorBrush(Color.FromRgb(0xF2, 0xF4, 0xF8));
    static readonly Brush SecondaryText = new SolidColorBrush(Color.FromRgb(0xC7, 0xD0, 0xDA));
    static readonly Brush WarningText = new SolidColorBrush(Color.FromRgb(0xFF, 0xBD, 0x70));
    static readonly Brush DividerBrush = new SolidColorBrush(Color.FromRgb(0x46, 0x51, 0x5F));
    readonly ContentPackage _package;
    readonly List<DefRef> _scenarios;
    readonly List<FactionChoice> _factions;
    readonly ComboBox _scenarioBox = new() { MinWidth = 420 };
    readonly StackPanel _body = new();
    DefRef? _selectedScenario;
    OpeningStrategicAuthoringDto? _state;
    bool _loading;
    bool _dirty;

    public OpeningStrategicEditorWindow(string packageRoot)
    {
        Title = "开局战略";
        Width = 860;
        Height = 720;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (Brush)FindResource("Editor.Background");
        Foreground = PrimaryText;
        Resources[typeof(Separator)] = CreateSeparatorStyle();
        Closing += (_, e) => { if (!ConfirmDiscard()) e.Cancel = true; };

        _package = PackageStore.Load(packageRoot);
        _scenarios = _package.OfType("openingScenario")
            .OrderBy(s => s.Name, StringComparer.Ordinal)
            .ThenBy(s => s.Id, StringComparer.Ordinal)
            .ToList();
        _factions = StrategicFactionAuthoring.LoadStrategicFactions(_package)
            .Select(f => new FactionChoice(f))
            .ToList();
        BuildUi();
        PopulateScenarios();
    }

    void BuildUi()
    {
        var root = new DockPanel { Margin = new Thickness(12) };
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var save = new Button { Content = "保存开局战略", Width = 112, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        save.Click += (_, _) => SaveCurrent();
        var discard = new Button { Content = "恢复未保存修改", Width = 128, Margin = new Thickness(0, 0, 8, 0) };
        discard.Click += (_, _) => { if (_selectedScenario != null) LoadScenario(_selectedScenario); };
        var close = new Button { Content = "关闭", Width = 68 };
        close.Click += (_, _) => Close();
        footer.Children.Add(save); footer.Children.Add(discard); footer.Children.Add(close);
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);

        var top = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        top.Children.Add(new TextBlock { Text = "开局场景", Foreground = SecondaryText, Margin = new Thickness(0, 0, 0, 3) });
        _scenarioBox.SelectionChanged += ScenarioChanged;
        top.Children.Add(_scenarioBox);
        top.Children.Add(new TextBlock
        {
            Text = "这里配置新游戏开始时的战略关系。游戏运行后的战争、联盟、附庸变化由存档保存。",
            Foreground = SecondaryText, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0),
        });
        DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _body };
        root.Children.Add(scroll);
        Content = root;
    }

    void PopulateScenarios()
    {
        _loading = true;
        foreach (var scenario in _scenarios)
            _scenarioBox.Items.Add(new ScenarioChoice(scenario));
        if (_scenarioBox.Items.Count > 0)
            _scenarioBox.SelectedIndex = 0;
        _loading = false;
        if (_scenarioBox.SelectedItem is ScenarioChoice choice)
            LoadScenario(choice.Definition);
    }

    void ScenarioChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _scenarioBox.SelectedItem is not ScenarioChoice next)
            return;
        if (_selectedScenario != null && _dirty && !ConfirmDiscard())
        {
            _loading = true;
            _scenarioBox.SelectedItem = _scenarioBox.Items.Cast<ScenarioChoice>().FirstOrDefault(x => x.Definition == _selectedScenario);
            _loading = false;
            return;
        }
        LoadScenario(next.Definition);
    }

    void LoadScenario(DefRef scenario)
    {
        _selectedScenario = scenario;
        _state = OpeningStrategicAuthoring.FromScenarioRaw(scenario.Raw);
        _dirty = false;
        RebuildBody();
    }

    void RebuildBody()
    {
        _body.Children.Clear();
        if (_selectedScenario == null)
            return;
        _body.Children.Add(new TextBlock { Text = "开局战略 · " + DisplayName(_selectedScenario), FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 3) });
        _body.Children.Add(new TextBlock { Text = _selectedScenario.Id, Foreground = SecondaryText, FontSize = 11, Margin = new Thickness(0, 0, 0, 12) });
        if (_state == null)
        {
            _body.Children.Add(new TextBlock { Text = "该场景尚未配置战略初始状态。", Foreground = WarningText, Margin = new Thickness(0, 0, 0, 8) });
            var create = new Button { Content = "创建战略初始状态", Width = 150 };
            create.Click += (_, _) => { _state = new OpeningStrategicAuthoringDto(); _dirty = true; RebuildBody(); };
            _body.Children.Add(create);
            return;
        }

        _body.Children.Add(Label("玩家势力"));
        _body.Children.Add(CreateFactionBox(_state.PlayerFactionId, id => _state.PlayerFactionId = id));
        AddSeparator();
        BuildPairs("附庸关系", "附庸势力", "宗主势力", _state.Vassalages,
            () => _state.Vassalages.Add(new OpeningVassalageAuthoringDto()),
            v => v.VassalFactionId, (v, id) => v.VassalFactionId = id,
            v => v.OverlordFactionId, (v, id) => v.OverlordFactionId = id,
            "→");
        AddSeparator();
        BuildPairs("开局联盟", "势力 A", "势力 B", _state.Alliances,
            () => _state.Alliances.Add(new OpeningAllianceAuthoringDto()),
            v => v.FactionAId, (v, id) => v.FactionAId = id,
            v => v.FactionBId, (v, id) => v.FactionBId = id,
            "↔");
        AddSeparator();
        BuildPairs("开局战争", "宣战方", "目标方", _state.InitialWars,
            () => _state.InitialWars.Add(new OpeningWarAuthoringDto()),
            v => v.DeclarerFactionId, (v, id) => v.DeclarerFactionId = id,
            v => v.TargetFactionId, (v, id) => v.TargetFactionId = id,
            "→");
    }

    void BuildPairs<T>(string title, string leftLabel, string rightLabel, List<T> items, Action add, Func<T, string> getLeft, Action<T, string> setLeft, Func<T, string> getRight, Action<T, string> setRight, string arrow)
    {
        _body.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
        var labels = new Grid { Margin = new Thickness(0, 0, 0, 3) };
        labels.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) }); labels.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) }); labels.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        labels.Children.Add(new TextBlock { Text = leftLabel, Foreground = SecondaryText, FontSize = 11 });
        var right = new TextBlock { Text = rightLabel, Foreground = SecondaryText, FontSize = 11 }; Grid.SetColumn(right, 2); labels.Children.Add(right); _body.Children.Add(labels);
        foreach (var item in items.ToList())
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            row.Children.Add(CreateFactionBox(getLeft(item), id => setLeft(item, id)));
            var mark = new TextBlock { Text = arrow, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(mark, 1); row.Children.Add(mark);
            var box = CreateFactionBox(getRight(item), id => setRight(item, id)); Grid.SetColumn(box, 2); row.Children.Add(box);
            var remove = new Button { Content = "删除", Padding = new Thickness(4, 2, 4, 2), Margin = new Thickness(6, 0, 0, 0) };
            remove.Click += (_, _) => { items.Remove(item); MarkDirty(); RebuildBody(); }; Grid.SetColumn(remove, 3); row.Children.Add(remove); _body.Children.Add(row);
        }
        var addButton = new Button { Content = "+ 添加" + title, Width = 112, Margin = new Thickness(0, 6, 0, 0) };
        addButton.Click += (_, _) => { add(); MarkDirty(); RebuildBody(); }; _body.Children.Add(addButton);
    }

    ComboBox CreateFactionBox(string selectedId, Action<string> set)
    {
        var box = new ComboBox { ItemsSource = _factions, ItemTemplate = CreateFactionTemplate() };
        box.SelectedItem = _factions.FirstOrDefault(f => string.Equals(f.Id, selectedId, StringComparison.Ordinal));
        box.SelectionChanged += (_, _) => { if (!_loading && box.SelectedItem is FactionChoice choice) { set(choice.Id); MarkDirty(); } };
        return box;
    }

    static DataTemplate CreateFactionTemplate()
    {
        var panel = new FrameworkElementFactory(typeof(StackPanel));
        panel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        var swatch = new FrameworkElementFactory(typeof(Border));
        swatch.SetValue(Border.WidthProperty, 12d); swatch.SetValue(Border.HeightProperty, 12d);
        swatch.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
        swatch.SetValue(Border.MarginProperty, new Thickness(0, 0, 6, 0));
        swatch.SetBinding(Border.BackgroundProperty, new Binding(nameof(FactionChoice.Brush)));
        panel.AppendChild(swatch);
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetValue(TextBlock.ForegroundProperty, PrimaryText);
        text.SetBinding(TextBlock.TextProperty, new Binding(nameof(FactionChoice.Display)));
        panel.AppendChild(text);
        return new DataTemplate { VisualTree = panel };
    }

    bool SaveCurrent()
    {
        if (_selectedScenario == null || _state == null)
            return false;
        var errors = OpeningStrategicAuthoring.Validate(_state, _factions.Select(f => f.Id));
        if (errors.Count > 0)
        {
            MessageBox.Show(this, "不能保存开局战略：\n\n" + string.Join("\n", errors), "开局战略 — 校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        OpeningStrategicAuthoring.ApplyToScenarioRaw(_selectedScenario.Raw, _state);
        PackageStore.SaveDefinition(_package, _selectedScenario);
        _dirty = false;
        MessageBox.Show(this, "已保存当前场景的开局战略。", "开局战略", MessageBoxButton.OK, MessageBoxImage.Information);
        return true;
    }

    bool ConfirmDiscard()
    {
        if (!_dirty) return true;
        var answer = MessageBox.Show(this, "当前开局战略有未保存修改。\n\n是：保存；否：放弃；取消：留在当前场景。", "开局战略", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return answer switch { MessageBoxResult.Yes => SaveCurrent(), MessageBoxResult.No => true, _ => false };
    }

    void MarkDirty() => _dirty = true;
    void AddSeparator() => _body.Children.Add(new Separator { Margin = new Thickness(0, 14, 0, 12) });
    static TextBlock Label(string text) => new() { Text = text, Foreground = SecondaryText, Margin = new Thickness(0, 0, 0, 3) };
    static Style CreateSeparatorStyle()
    {
        var style = new Style(typeof(Separator));
        style.Setters.Add(new Setter(HeightProperty, 1d));
        style.Setters.Add(new Setter(BackgroundProperty, DividerBrush));
        style.Setters.Add(new Setter(ForegroundProperty, DividerBrush));
        return style;
    }
    static string DisplayName(DefRef scenario) => string.IsNullOrWhiteSpace(scenario.Name) ? scenario.Id : scenario.Name;
}

sealed class ScenarioChoice
{
    public DefRef Definition { get; }
    public ScenarioChoice(DefRef definition) => Definition = definition;
    public override string ToString() => string.IsNullOrWhiteSpace(Definition.Name) ? Definition.Id : Definition.Name + "  ·  " + Definition.Id;
}

sealed class FactionChoice
{
    public string Id { get; }
    public string Display { get; }
    public Brush Brush { get; }
    public FactionChoice(StrategicFactionAuthoringDto dto)
    {
        Id = dto.Id;
        Display = dto.Name + "  ·  " + dto.Id;
        try { Brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dto.MapColor)); }
        catch { Brush = Brushes.Gray; }
        if (Brush.CanFreeze) Brush.Freeze();
    }
    public override string ToString() => Display;
}
