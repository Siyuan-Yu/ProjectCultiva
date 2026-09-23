using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using ContentAuthoring.Shared;
using Microsoft.Win32;

namespace OpportunityEditor;

public partial class MainWindow : Window
{
    public static IReadOnlyList<string> AllowedOutcomeKinds { get; } =
        new[] { "setFlag", "clearFlag", "addCounter", "setCounter" };

    sealed record Option(string Id, string Name)
    {
        public override string ToString() => string.IsNullOrWhiteSpace(Name) ? Id : $"{Name} — {Id}";
    }

    sealed record DefRow(DefRef Definition)
    {
        public string Display => string.IsNullOrWhiteSpace(Definition.Name)
            ? Definition.Id : $"{Definition.Name}\n{Definition.Id}";
    }

    public sealed class PoolEntryRow { public string DefinitionId { get; set; } = ""; public int Weight { get; set; } = 1; }
    public sealed class ConditionRow
    {
        public string Kind { get; set; } = "";
        public string Id { get; set; } = "";
        public int Amount { get; set; }
        public string Realm { get; set; } = "";
        public string CharacterId { get; set; } = "";
    }
    public sealed class OutcomeRow { public string Kind { get; set; } = "setFlag"; public string Id { get; set; } = ""; public int Amount { get; set; } }

    ContentPackage? _package;
    DefRef? _director;
    DefRef? _opportunity;
    DefRef? _spawnTable;
    bool _loading;
    readonly ObservableCollection<PoolEntryRow> _poolEntries = new();
    readonly ObservableCollection<ConditionRow> _conditions = new();
    readonly ObservableCollection<OutcomeRow> _outcomes = new();

    public MainWindow()
    {
        InitializeComponent();
        PoolGrid.ItemsSource = _poolEntries;
        ConditionsGrid.ItemsSource = _conditions;
        OutcomesGrid.ItemsSource = _outcomes;
        DiscoveryModeBox.SelectedIndex = 0;
        var root = PackagePaths.FindDefaultBaseGame();
        if (root != null) LoadRoot(root);
    }

    void LoadRoot(string root, string? selectId = null)
    {
        _loading = true;
        _package = PackageStore.Load(root);
        _director = null;
        _opportunity = null;
        _spawnTable = null;
        RootText.Text = root;
        RootText.ToolTip = root;
        PoolCharacterPicker.Configure(_package);
        var surfaces = _package.OfType("outdoorSurface")
            .Select(d => new Option(d.Id, d.Name)).OrderBy(x => x.Name).ToList();
        DirectorSurfaceBox.ItemsSource = surfaces;
        OpportunitySurfaceBox.ItemsSource = surfaces;
        RefreshLists(selectId);
        RefreshSpawnTables();
        _loading = false;
        StatusText.Text = $"已加载 {_package.OfType("worldOpportunityDirector").Count()} 个 Director、{_package.OfType("worldOpportunity").Count()} 个 Opportunity。";
    }

    void RefreshLists(string? selectId = null)
    {
        if (_package == null) return;
        var directors = _package.OfType("worldOpportunityDirector").OrderBy(d => d.Name).Select(d => new DefRow(d)).ToList();
        var opportunities = _package.OfType("worldOpportunity").OrderBy(d => d.Name).Select(d => new DefRow(d)).ToList();
        DirectorList.ItemsSource = directors;
        OpportunityList.ItemsSource = opportunities;
        if (!string.IsNullOrWhiteSpace(selectId))
        {
            DirectorList.SelectedItem = directors.FirstOrDefault(x => x.Definition.Id == selectId);
            OpportunityList.SelectedItem = opportunities.FirstOrDefault(x => x.Definition.Id == selectId);
        }
    }

    void RefreshSpawnTables(string? selectedId = null)
    {
        if (_package == null) return;
        var options = _package.OfType("spawnTable").OrderBy(d => d.Name).Select(d => new Option(d.Id, d.Name)).ToList();
        SpawnTableBox.ItemsSource = options;
        SelectOption(SpawnTableBox, selectedId ?? _spawnTable?.Id ?? "");
    }

    static void SelectOption(ComboBox box, string id) =>
        box.SelectedItem = (box.ItemsSource as IEnumerable<Option>)?.FirstOrDefault(x => x.Id == id);

    void DirectorList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || DirectorList.SelectedItem is not DefRow row) return;
        _loading = true;
        _director = row.Definition;
        _opportunity = null;
        OpportunityList.SelectedItem = null;
        var raw = _director.Raw;
        DirectorIdBox.Text = JsonEdit.GetString(raw, "id");
        DirectorNameBox.Text = JsonEdit.GetString(raw, "name");
        SelectOption(DirectorSurfaceBox, JsonEdit.GetString(raw, "surfaceId"));
        TargetMinBox.Text = JsonEdit.GetInt(raw, "targetActiveMin").ToString(CultureInfo.InvariantCulture);
        TargetMaxBox.Text = JsonEdit.GetInt(raw, "targetActiveMax").ToString(CultureInfo.InvariantCulture);
        EditorTabs.SelectedIndex = 0;
        _loading = false;
    }

    void OpportunityList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || OpportunityList.SelectedItem is not DefRow row) return;
        _loading = true;
        _opportunity = row.Definition;
        _director = null;
        DirectorList.SelectedItem = null;
        var raw = _opportunity.Raw;
        OpportunityIdBox.Text = JsonEdit.GetString(raw, "id");
        OpportunityNameBox.Text = JsonEdit.GetString(raw, "name");
        SelectOption(OpportunitySurfaceBox, JsonEdit.GetString(raw, "surfaceId"));
        WeightBox.Text = JsonEdit.GetInt(raw, "weight", 1).ToString(CultureInfo.InvariantCulture);
        MaxActiveBox.Text = JsonEdit.GetInt(raw, "maxActive", 1).ToString(CultureInfo.InvariantCulture);
        DurationDaysBox.Text = JsonEdit.GetInt(raw, "durationDays", 1).ToString(CultureInfo.InvariantCulture);
        MinDistanceBox.Text = JsonEdit.GetDouble(raw, "minPlayerDistanceWorld").ToString(CultureInfo.InvariantCulture);
        MaxDistanceBox.Text = JsonEdit.GetDouble(raw, "maxPlayerDistanceWorld").ToString(CultureInfo.InvariantCulture);
        AllowInsideSiteBox.IsChecked = JsonEdit.GetBool(raw, "allowInsideWorldSite");
        SelectDiscoveryMode(JsonEdit.GetString(raw, "discoveryMode", "worldVisible"));
        NoticeTitleBox.Text = JsonEdit.GetString(raw, "publicNoticeTitle");
        NoticeTextBox.Text = JsonEdit.GetString(raw, "publicNoticeText");
        RevealExactLocationBox.IsChecked = JsonEdit.GetBool(raw, "publicNoticeRevealExactLocation");
        LoadConditions(raw["conditions"] as JsonArray);
        LoadOutcomes(raw["expireOutcomes"] as JsonArray);
        RefreshSpawnTables(JsonEdit.GetString(raw, "spawnTableId"));
        LoadSelectedPool();
        EditorTabs.SelectedIndex = 1;
        _loading = false;
        UpdateNoticeVisibility();
    }

    void SelectDiscoveryMode(string mode)
    {
        foreach (ComboBoxItem item in DiscoveryModeBox.Items)
            if (string.Equals(item.Tag as string, mode, StringComparison.Ordinal)) { DiscoveryModeBox.SelectedItem = item; return; }
        DiscoveryModeBox.SelectedIndex = 0;
    }

    string DiscoveryMode => (DiscoveryModeBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "worldVisible";

    void LoadConditions(JsonArray? array)
    {
        _conditions.Clear();
        if (array == null) return;
        foreach (var node in array.OfType<JsonObject>())
            _conditions.Add(new ConditionRow
            {
                Kind = JsonEdit.GetString(node, "kind"), Id = JsonEdit.GetString(node, "id"),
                Amount = JsonEdit.GetInt(node, "amount"), Realm = JsonEdit.GetString(node, "realm"),
                CharacterId = JsonEdit.GetString(node, "characterId")
            });
    }

    void LoadOutcomes(JsonArray? array)
    {
        _outcomes.Clear();
        if (array == null) return;
        foreach (var node in array.OfType<JsonObject>())
            _outcomes.Add(new OutcomeRow { Kind = JsonEdit.GetString(node, "kind"), Id = JsonEdit.GetString(node, "id"), Amount = JsonEdit.GetInt(node, "amount") });
    }

    void SpawnTableBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        LoadSelectedPool();
    }

    void LoadSelectedPool()
    {
        _poolEntries.Clear();
        var id = (SpawnTableBox.SelectedItem as Option)?.Id ?? "";
        _spawnTable = _package?.Find(id);
        if (_spawnTable?.Raw["entries"] is not JsonArray entries) return;
        foreach (var node in entries.OfType<JsonObject>())
            _poolEntries.Add(new PoolEntryRow { DefinitionId = JsonEdit.GetString(node, "definitionId"), Weight = Math.Max(1, JsonEdit.GetInt(node, "weight", 1)) });
    }

    void OpenPackage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "选择内容包目录" };
        if (dialog.ShowDialog() == true) LoadRoot(dialog.FolderName);
    }

    void NewDirector_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null) return;
        var id = UniqueId("base:world_opportunity_director_new");
        var raw = new JsonObject
        {
            ["id"] = id, ["type"] = "worldOpportunityDirector", ["name"] = "新 Surface Director",
            ["surfaceId"] = "", ["targetActiveMin"] = 1, ["targetActiveMax"] = 1
        };
        PackageStore.AppendDefinition(_package, "WorldOpportunities/world_opportunity_authoring.json", raw);
        RefreshLists(id);
    }

    void NewOpportunity_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null) return;
        var id = UniqueId("base:world_opportunity_new");
        var raw = new JsonObject
        {
            ["id"] = id, ["type"] = "worldOpportunity", ["name"] = "新世界机会", ["surfaceId"] = "",
            ["weight"] = 1, ["maxActive"] = 1, ["spawnTableId"] = "", ["durationDays"] = 1,
            ["minPlayerDistanceWorld"] = 3.0, ["maxPlayerDistanceWorld"] = 8.0,
            ["allowInsideWorldSite"] = false, ["discoveryMode"] = "worldVisible",
            ["conditions"] = new JsonArray(), ["expireOutcomes"] = new JsonArray()
        };
        PackageStore.AppendDefinition(_package, "WorldOpportunities/world_opportunity_authoring.json", raw);
        RefreshLists(id);
    }

    void NewPool_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null) return;
        var characterId = PoolCharacterPicker.SelectedId;
        if (string.IsNullOrWhiteSpace(characterId))
        {
            MessageBox.Show(this, "请先在下方选择人物；新人物池必须至少包含一名人物。", "新建人物池");
            return;
        }
        var id = UniqueId("base:spawn_world_opportunity_pool_new");
        var raw = new JsonObject
        {
            ["id"] = id,
            ["type"] = "spawnTable",
            ["name"] = "新 Opportunity 人物池",
            ["entries"] = new JsonArray
            {
                new JsonObject { ["definitionId"] = characterId, ["weight"] = 1 }
            }
        };
        _spawnTable = PackageStore.AppendDefinition(_package, "WorldOpportunities/world_opportunity_authoring.json", raw);
        RefreshSpawnTables(id);
        LoadSelectedPool();
        StatusText.Text = "已创建包含所选人物的独立 Opportunity 人物池。";
    }

    string UniqueId(string seed)
    {
        if (_package?.Find(seed) == null) return seed;
        for (var i = 2; ; i++) if (_package.Find(seed + "_" + i) == null) return seed + "_" + i;
    }

    void AddPoolCharacter_Click(object sender, RoutedEventArgs e)
    {
        var id = PoolCharacterPicker.SelectedId;
        if (string.IsNullOrWhiteSpace(id)) { MessageBox.Show(this, "请先选择人物。", "人物池"); return; }
        if (_poolEntries.Any(x => x.DefinitionId == id)) return;
        _poolEntries.Add(new PoolEntryRow { DefinitionId = id, Weight = 1 });
    }

    void RemovePoolCharacter_Click(object sender, RoutedEventArgs e)
    {
        if (PoolGrid.SelectedItem is PoolEntryRow row) _poolEntries.Remove(row);
    }
    void RemoveCondition_Click(object sender, RoutedEventArgs e) { if (ConditionsGrid.SelectedItem is ConditionRow row) _conditions.Remove(row); }
    void RemoveOutcome_Click(object sender, RoutedEventArgs e) { if (OutcomesGrid.SelectedItem is OutcomeRow row) _outcomes.Remove(row); }
    void DiscoveryModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateNoticeVisibility();
    void UpdateNoticeVisibility() { if (NoticePanel != null) NoticePanel.Visibility = DiscoveryMode == "publicNotice" ? Visibility.Visible : Visibility.Collapsed; }

    void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null) return;
        PoolGrid.CommitEdit(); ConditionsGrid.CommitEdit(); OutcomesGrid.CommitEdit();
        try
        {
            if (_director != null) SaveDirector();
            else if (_opportunity != null) SaveOpportunity();
            else { StatusText.Text = "请先选择一条定义。"; return; }
            var selected = _director?.Id ?? _opportunity?.Id;
            LoadRoot(_package.Root, selected);
            StatusText.Text = "已保存。Runtime Content validation 仍需通过后才可进入游戏。";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void SaveDirector()
    {
        if (_package == null || _director == null) return;
        var id = Required(DirectorIdBox.Text, "Director ID");
        var surface = (DirectorSurfaceBox.SelectedItem as Option)?.Id ?? "";
        var min = PositiveOrZero(TargetMinBox.Text, "最少数量");
        var max = PositiveOrZero(TargetMaxBox.Text, "最多数量");
        if (string.IsNullOrWhiteSpace(surface)) throw new InvalidOperationException("请选择 Surface。");
        if (max < min) throw new InvalidOperationException("最多数量不能小于最少数量。");
        if (_package.OfType("worldOpportunityDirector").Any(d => d != _director && JsonEdit.GetString(d.Raw, "surfaceId") == surface))
            throw new InvalidOperationException("同一个 Surface 只能有一个 Director。");
        var raw = _director.Raw;
        raw["id"] = id; raw["type"] = "worldOpportunityDirector"; raw["name"] = Required(DirectorNameBox.Text, "名称");
        raw["surfaceId"] = surface; raw["targetActiveMin"] = min; raw["targetActiveMax"] = max;
        PackageStore.SaveDefinition(_package, _director);
    }

    void SaveOpportunity()
    {
        if (_package == null || _opportunity == null) return;
        var surface = (OpportunitySurfaceBox.SelectedItem as Option)?.Id ?? "";
        var tableId = (SpawnTableBox.SelectedItem as Option)?.Id ?? "";
        if (string.IsNullOrWhiteSpace(surface)) throw new InvalidOperationException("请选择 Surface。");
        if (_spawnTable == null || string.IsNullOrWhiteSpace(tableId)) throw new InvalidOperationException("请选择或新建 NPC 人物池。");
        if (_poolEntries.Count == 0) throw new InvalidOperationException("NPC 人物池至少需要一名人物。");
        var mode = DiscoveryMode;
        if (mode == "publicNotice" && string.IsNullOrWhiteSpace(NoticeTextBox.Text)) throw new InvalidOperationException("公开消息方式必须填写消息文字。");
        var minDistance = Number(MinDistanceBox.Text, "最小距离");
        var maxDistance = Number(MaxDistanceBox.Text, "最大距离");
        if (minDistance < 0 || maxDistance < minDistance) throw new InvalidOperationException("玩家距离范围无效。");
        foreach (var outcome in _outcomes)
            if (!AllowedOutcomeKinds.Contains(outcome.Kind)) throw new InvalidOperationException("到期结果仅允许 setFlag / clearFlag / addCounter / setCounter。");

        var raw = _opportunity.Raw;
        raw["id"] = Required(OpportunityIdBox.Text, "Opportunity ID"); raw["type"] = "worldOpportunity";
        raw["name"] = Required(OpportunityNameBox.Text, "名称"); raw["surfaceId"] = surface;
        raw["weight"] = Positive(WeightBox.Text, "权重"); raw["maxActive"] = Positive(MaxActiveBox.Text, "最大同时存在");
        raw["spawnTableId"] = tableId; raw["durationDays"] = Positive(DurationDaysBox.Text, "生命周期");
        raw["minPlayerDistanceWorld"] = minDistance; raw["maxPlayerDistanceWorld"] = maxDistance;
        raw["allowInsideWorldSite"] = AllowInsideSiteBox.IsChecked == true; raw["discoveryMode"] = mode;
        if (mode == "publicNotice")
        {
            var title = NoticeTitleBox.Text.Trim();
            if (title.Length > 0) raw["publicNoticeTitle"] = title; else raw.Remove("publicNoticeTitle");
            raw["publicNoticeText"] = NoticeTextBox.Text.Trim();
            raw["publicNoticeRevealExactLocation"] = RevealExactLocationBox.IsChecked == true;
        }
        else
        {
            raw.Remove("publicNoticeTitle");
            raw.Remove("publicNoticeText");
            raw.Remove("publicNoticeRevealExactLocation");
        }
        raw["conditions"] = WriteConditions(); raw["expireOutcomes"] = WriteOutcomes();
        SavePool();
        PackageStore.SaveDefinition(_package, _opportunity);
    }

    void SavePool()
    {
        if (_package == null || _spawnTable == null) return;
        var entries = new JsonArray();
        foreach (var row in _poolEntries)
            entries.Add(new JsonObject { ["definitionId"] = row.DefinitionId, ["weight"] = Math.Max(1, row.Weight), ["countMin"] = 1, ["countMax"] = 1 });
        _spawnTable.Raw["entries"] = entries;
        PackageStore.SaveDefinition(_package, _spawnTable);
    }

    JsonArray WriteConditions()
    {
        var result = new JsonArray();
        foreach (var row in _conditions.Where(x => !string.IsNullOrWhiteSpace(x.Kind)))
        {
            var node = new JsonObject { ["kind"] = row.Kind.Trim() };
            if (!string.IsNullOrWhiteSpace(row.Id)) node["id"] = row.Id.Trim();
            if (row.Amount != 0) node["amount"] = row.Amount;
            if (!string.IsNullOrWhiteSpace(row.Realm)) node["realm"] = row.Realm.Trim();
            if (!string.IsNullOrWhiteSpace(row.CharacterId)) node["characterId"] = row.CharacterId.Trim();
            result.Add(node);
        }
        return result;
    }

    JsonArray WriteOutcomes()
    {
        var result = new JsonArray();
        foreach (var row in _outcomes.Where(x => !string.IsNullOrWhiteSpace(x.Kind)))
        {
            var node = new JsonObject { ["kind"] = row.Kind.Trim(), ["id"] = row.Id.Trim() };
            if (row.Amount != 0) node["amount"] = row.Amount;
            result.Add(node);
        }
        return result;
    }

    static string Required(string value, string label) => !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw new InvalidOperationException(label + "不能为空。");
    static int Positive(string value, string label) { var n = PositiveOrZero(value, label); return n > 0 ? n : throw new InvalidOperationException(label + "必须大于 0。"); }
    static int PositiveOrZero(string value, string label) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= 0 ? n : throw new InvalidOperationException(label + "必须是非负整数。");
    static double Number(string value, string label) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : throw new InvalidOperationException(label + "必须是数字。");
}
