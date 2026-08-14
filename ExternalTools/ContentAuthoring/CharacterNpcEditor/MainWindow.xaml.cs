using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using ContentAuthoring.Shared;
using Microsoft.Win32;

namespace CharacterNpcEditor;

public partial class MainWindow : Window
{
    public const string LevelTesterRosterId = "base:roster_level_tester";
    const string LevelTesterRosterRelPath = "Rosters/level_tester_roster.json";

    ContentPackage? _package;
    DefRef? _character;
    DefRef? _scenario;
    ObservableCollection<SpawnRow> _spawns = new();
    readonly List<CapabilityRowUi> _caps = new();
    readonly List<SpiritRootRowUi> _roots = new();
    bool _suppressControllableUi;

    public MainWindow()
    {
        InitializeComponent();
        BuildCapabilityUi();
        BuildSpiritRootUi();
        TryLoadDefault();
    }

    void BuildCapabilityUi()
    {
        CapabilityPanel.Items.Clear();
        _caps.Clear();
        foreach (var o in UiLabels.ScheduleActivities)
        {
            var row = new CapabilityRowUi { Key = o.Key, Label = o.Label };
            var panel = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var cb = new CheckBox { Content = $"可做：{o.Label}", Width = 140, VerticalAlignment = VerticalAlignment.Center };
            var weight = new TextBox { Width = 60, Text = "0", Margin = new Thickness(12, 0, 8, 0) };
            var hint = new TextBlock
            {
                Text = "权重（越大越优先）",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = System.Windows.Media.Brushes.DimGray
            };
            row.EnabledBox = cb;
            row.WeightBox = weight;
            DockPanel.SetDock(cb, Dock.Left);
            panel.Children.Add(cb);
            panel.Children.Add(weight);
            panel.Children.Add(hint);
            CapabilityPanel.Items.Add(panel);
            _caps.Add(row);
        }
    }

    void BuildSpiritRootUi()
    {
        SpiritRootPanel.Items.Clear();
        _roots.Clear();
        foreach (var o in UiLabels.SpiritRoots)
        {
            var row = new SpiritRootRowUi { Key = o.Key, Label = o.Label };
            var panel = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var label = new TextBlock
            {
                Text = $"{o.Label}灵根 ({o.Key})",
                Width = 140,
                VerticalAlignment = VerticalAlignment.Center
            };
            var box = new TextBox { Width = 60, Text = "0", Margin = new Thickness(8, 0, 8, 0) };
            var hint = new TextBlock
            {
                Text = "/ 30（亲和数值，不是标签）",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = System.Windows.Media.Brushes.DimGray
            };
            row.ValueBox = box;
            DockPanel.SetDock(label, Dock.Left);
            panel.Children.Add(label);
            panel.Children.Add(box);
            panel.Children.Add(hint);
            SpiritRootPanel.Items.Add(panel);
            _roots.Add(row);
        }
    }

    void TryLoadDefault()
    {
        var root = PackagePaths.FindDefaultBaseGame();
        if (root != null) LoadRoot(root);
        else StatusText.Text = "未找到默认 Content/BaseGame，请点「打开包…」";
    }

    void LoadRoot(string root)
    {
        _package = PackageStore.Load(root);
        RootText.Text = root;
        ScenarioBox.ItemsSource = _package.OfType("openingScenario").Select(d => d.Id).OrderBy(x => x).ToList();
        var sched = new[] { "" }.Concat(_package.OfType("schedule").Select(d => d.Id).OrderBy(x => x)).ToList();
        SpawnSchedCol.ItemsSource = sched;
        SpawnAiCol.ItemsSource = UiLabels.Labels(UiLabels.AiRoles);
        SpawnFactionCol.ItemsSource = UiLabels.Labels(UiLabels.FactionRoles);
        if (ScenarioBox.Items.Count > 0)
        {
            var prefer = _package.OfType("openingScenario")
                .Select(d => d.Id)
                .FirstOrDefault(id => id.Contains("ch01", StringComparison.OrdinalIgnoreCase));
            ScenarioBox.SelectedItem = prefer ?? ScenarioBox.Items[0];
        }

        RefreshCharList(keepId: CharList.SelectedItem is CharListItem cur ? cur.Id : null);
        StatusText.Text =
            $"人物 {_package.OfType("character").Count()} · 场景 {_package.OfType("openingScenario").Count()}";
    }

    void RefreshCharList(string? keepId)
    {
        if (_package == null) return;
        var filter = FilterBox.SelectedIndex;
        var items = new List<CharListItem>();
        foreach (var d in _package.OfType("character").OrderBy(x => x.Id))
        {
            var controllable = ResolveControllable(d);
            if (filter == 1 && !controllable) continue;
            if (filter == 2 && controllable) continue;
            var name = JsonEdit.GetString(d.Raw, "name");
            var prefix = controllable ? "[可控制] " : "[NPC] ";
            items.Add(new CharListItem(d.Id, prefix + (string.IsNullOrEmpty(name) ? d.Id : name)));
        }

        CharList.ItemsSource = items;
        if (keepId != null)
        {
            var hit = items.FirstOrDefault(i => i.Id == keepId);
            if (hit != null) CharList.SelectedItem = hit;
            else if (items.Count > 0) CharList.SelectedIndex = 0;
        }
        else if (items.Count > 0)
            CharList.SelectedIndex = 0;
    }

    bool ResolveControllable(DefRef character)
    {
        var spawn = FindSpawn(character.Id);
        if (spawn != null)
            return spawn.Controllable;
        return JsonEdit.GetBool(character.Raw, "playerControllable", false);
    }

    SpawnRow? FindSpawn(string definitionId) =>
        _spawns.FirstOrDefault(s => string.Equals(s.DefinitionId, definitionId, StringComparison.Ordinal));

    void OpenPackage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "选择 Content/BaseGame 包目录" };
        if (dlg.ShowDialog() == true) LoadRoot(dlg.FolderName);
    }

    void FilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _package == null) return;
        var keep = CharList.SelectedItem is CharListItem c ? c.Id : null;
        RefreshCharList(keep);
    }

    void CharList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_package == null || CharList.SelectedItem is not CharListItem item) return;
        _character = _package.Find(item.Id);
        if (_character == null) return;
        IdBox.Text = JsonEdit.GetString(_character.Raw, "id");
        NameBox.Text = JsonEdit.GetString(_character.Raw, "name");
        RealmBox.Text = JsonEdit.GetString(_character.Raw, "initialRealmPlaceholder");
        HometownBox.Text = JsonEdit.GetString(_character.Raw, "hometown");
        ReputationBox.Text = JsonEdit.GetInt(_character.Raw, "reputation", 0).ToString(CultureInfo.InvariantCulture);
        TagsBox.Text = JsonEdit.JoinStringArray(_character.Raw["tags"]);
        PersonalityBox.Text = JsonEdit.JoinStringArray(_character.Raw["personalityTags"]);
        BackgroundBox.Text = JsonEdit.JoinStringArray(_character.Raw["backgroundTags"]);
        TalentBox.Text = JsonEdit.JoinStringArray(_character.Raw["talentTags"]);
        GoalsBox.Text = LinesFromArray(_character.Raw["goals"]);
        DesiresBox.Text = LinesFromArray(_character.Raw["desires"]);
        PreferredWaBox.Text = LinesFromArray(_character.Raw["preferredWorkAreaIds"]);
        HomeWaBox.Text = JsonEdit.GetString(_character.Raw, "homeWorkAreaId");

        ReadAttr("MaxHp", AttrHpBox);
        ReadAttr("Stamina", AttrStaminaBox);
        ReadAttr("Attack", AttrAtkBox);
        ReadAttr("Defense", AttrDefBox);
        ReadAttr("Speed", AttrSpdBox);
        ReadAttr("SpiritSense", AttrSenseBox);
        ReadAttr("Comprehension", AttrCompBox);
        ReadAttr("SpiritPower", AttrSpiritPowerBox);
        ReadAttr("Cultivation", AttrCultBox);
        ReadAttr("MindState", AttrMindBox);
        LoadSpiritRoots();
        LoadCapabilities();
        _suppressControllableUi = true;
        ControllableBox.IsChecked = ResolveControllable(_character);
        _suppressControllableUi = false;
        HighlightSpawnForCharacter(item.Id);
        RefreshTalkEventsHint(item.Id);
    }

    void RefreshTalkEventsHint(string characterId)
    {
        if (TalkEventsHint == null)
            return;
        if (_package == null || string.IsNullOrEmpty(characterId))
        {
            TalkEventsHint.Text = "关联 onTalk 事件：（未选人物）";
            return;
        }

        var hits = PackageStore.EventsTalkingToNpc(_package, characterId);
        TalkEventsHint.Text = hits.Count == 0
            ? "关联 onTalk 事件：无（在事件编辑器建 trigger=onTalk + npcDefinitionId=本 id；选项可 startQuest）"
            : "关联 onTalk 事件：" + string.Join("、", hits);
    }

    static string LinesFromArray(JsonNode? node)
    {
        if (node is not JsonArray arr) return "";
        return string.Join(Environment.NewLine, arr.OfType<JsonValue>().Select(v => v.GetValue<string>()));
    }

    void ControllableBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressControllableUi || _character == null) return;
        ApplyControllable(JsonEdit.GetString(_character.Raw, "id"), ControllableBox.IsChecked == true, refreshList: true);
    }

    void ApplyControllable(string definitionId, bool on, bool refreshList)
    {
        if (_character != null &&
            string.Equals(JsonEdit.GetString(_character.Raw, "id"), definitionId, StringComparison.Ordinal))
            _character.Raw["playerControllable"] = on;

        var spawn = FindSpawn(definitionId);
        if (spawn != null)
            spawn.Controllable = on;

        if (refreshList)
            RefreshCharList(definitionId);
    }

    void ReadAttr(string key, TextBox box)
    {
        box.Text = "0";
        if (_character?.Raw["baseAttributes"] is JsonObject attrs &&
            attrs[key] is JsonValue v && v.TryGetValue<int>(out var n))
            box.Text = n.ToString(CultureInfo.InvariantCulture);
    }

    void LoadSpiritRoots()
    {
        var map = _character?.Raw["spiritRoots"] as JsonObject;
        foreach (var row in _roots)
        {
            var v = 0;
            if (map != null && map[row.Key] is JsonValue jv)
            {
                if (jv.TryGetValue<int>(out var n)) v = n;
                else if (jv.TryGetValue<double>(out var d)) v = (int)d;
            }

            row.ValueBox!.Text = v.ToString(CultureInfo.InvariantCulture);
        }
    }

    void LoadCapabilities()
    {
        var caps = _character?.Raw["activityCapabilities"] as JsonObject;
        var pri = _character?.Raw["activityPriorities"] as JsonObject;
        foreach (var row in _caps)
        {
            var enabled = true;
            if (caps != null && caps[row.Key] is JsonValue cv && cv.TryGetValue<bool>(out var b))
                enabled = b;
            else if (caps != null)
                enabled = false;
            row.EnabledBox!.IsChecked = enabled;
            var w = 0;
            if (pri != null && pri[row.Key] is JsonValue pv)
            {
                if (pv.TryGetValue<int>(out var n)) w = n;
                else if (pv.TryGetValue<double>(out var d)) w = (int)d;
            }

            row.WeightBox!.Text = w.ToString(CultureInfo.InvariantCulture);
        }
    }

    void NewCharacter_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null) return;
        var id = $"base:character_npc_{DateTime.Now:yyyyMMddHHmmss}";
        var roots = new JsonObject();
        foreach (var o in UiLabels.SpiritRoots)
            roots[o.Key] = 0;
        var raw = new JsonObject
        {
            ["id"] = id,
            ["type"] = "character",
            ["name"] = "新NPC",
            ["playerControllable"] = false,
            ["baseAttributes"] = new JsonObject
            {
                ["MaxHp"] = 100,
                ["Stamina"] = 50,
                ["Attack"] = 8,
                ["Defense"] = 5,
                ["Speed"] = 8,
                ["SpiritSense"] = 5,
                ["Comprehension"] = 5,
                ["SpiritPower"] = 0,
                ["Cultivation"] = 0,
                ["MindState"] = 50
            },
            ["spiritRoots"] = roots,
            ["tags"] = new JsonArray { "npc", "mortal" },
            ["preferredWorkAreaIds"] = new JsonArray(),
            ["homeWorkAreaId"] = "base:workarea_houses",
            ["personalityTags"] = new JsonArray(),
            ["backgroundTags"] = new JsonArray(),
            ["talentTags"] = new JsonArray(),
            ["goals"] = new JsonArray(),
            ["desires"] = new JsonArray(),
            ["hometown"] = "",
            ["reputation"] = 0,
            ["initialRealmPlaceholder"] = "凡人",
            ["activityCapabilities"] = DefaultMortalCapabilities(),
            ["activityPriorities"] = DefaultMortalPriorities()
        };
        _character = PackageStore.AppendDefinition(_package, "Characters/ch01_reference_characters.json", raw);
        LoadRoot(_package.Root);
        RefreshCharList(id);
        StatusText.Text = "已新建人物 " + id;
    }

    static JsonObject DefaultMortalCapabilities() => new()
    {
        ["Labor"] = true,
        ["Rest"] = true,
        ["Eat"] = true,
        ["Cultivate"] = false,
        ["Explore"] = true,
        ["Patrol"] = false,
        ["Inspect"] = false,
        ["Idle"] = true
    };

    static JsonObject DefaultMortalPriorities() => new()
    {
        ["Labor"] = 8,
        ["Eat"] = 6,
        ["Rest"] = 5,
        ["Explore"] = 2,
        ["Cultivate"] = 0,
        ["Patrol"] = 0,
        ["Inspect"] = 0,
        ["Idle"] = 1
    };

    void SaveCharacter_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null || _character == null)
        {
            MessageBox.Show("没有选中人物");
            return;
        }

        _character.Raw["id"] = IdBox.Text?.Trim() ?? _character.Id;
        _character.Raw["type"] = "character";
        _character.Raw["name"] = NameBox.Text ?? "";
        _character.Raw["playerControllable"] = ControllableBox.IsChecked == true;
        JsonEdit.SetString(_character.Raw, "initialRealmPlaceholder", RealmBox.Text);
        JsonEdit.SetString(_character.Raw, "hometown", HometownBox.Text);
        _character.Raw["reputation"] = ParseInt(ReputationBox.Text, 0);
        _character.Raw.Remove("spiritRootPlaceholder");
        _character.Raw["tags"] = JsonEdit.ParseStringList(TagsBox.Text);
        _character.Raw["personalityTags"] = JsonEdit.ParseStringList(PersonalityBox.Text);
        _character.Raw["backgroundTags"] = JsonEdit.ParseStringList(BackgroundBox.Text);
        _character.Raw["talentTags"] = JsonEdit.ParseStringList(TalentBox.Text);
        _character.Raw["goals"] = LinesToArray(GoalsBox.Text);
        _character.Raw["desires"] = LinesToArray(DesiresBox.Text);
        _character.Raw["preferredWorkAreaIds"] = LinesToArray(PreferredWaBox.Text);
        _character.Raw["homeWorkAreaId"] = HomeWaBox.Text?.Trim() ?? "";
        _character.Raw["baseAttributes"] = new JsonObject
        {
            ["MaxHp"] = ParseInt(AttrHpBox.Text, 100),
            ["Stamina"] = ParseInt(AttrStaminaBox.Text, 50),
            ["Attack"] = ParseInt(AttrAtkBox.Text, 8),
            ["Defense"] = ParseInt(AttrDefBox.Text, 5),
            ["Speed"] = ParseInt(AttrSpdBox.Text, 8),
            ["SpiritSense"] = ParseInt(AttrSenseBox.Text, 5),
            ["Comprehension"] = ParseInt(AttrCompBox.Text, 5),
            ["SpiritPower"] = ParseInt(AttrSpiritPowerBox.Text, 0),
            ["Cultivation"] = ParseInt(AttrCultBox.Text, 0),
            ["MindState"] = ParseInt(AttrMindBox.Text, 50)
        };
        var roots = new JsonObject();
        foreach (var row in _roots)
            roots[row.Key] = Math.Clamp(ParseInt(row.ValueBox?.Text, 0), 0, 30);
        _character.Raw["spiritRoots"] = roots;

        var caps = new JsonObject();
        var pri = new JsonObject();
        foreach (var row in _caps)
        {
            caps[row.Key] = row.EnabledBox?.IsChecked == true;
            pri[row.Key] = ParseInt(row.WeightBox?.Text, 0);
        }

        _character.Raw["activityCapabilities"] = caps;
        _character.Raw["activityPriorities"] = pri;

        var keep = JsonEdit.GetString(_character.Raw, "id");
        ApplyControllable(keep, ControllableBox.IsChecked == true, refreshList: false);
        PackageStore.SaveDefinition(_package, _character);
        if (_scenario != null && FindSpawn(keep) != null)
        {
            SpawnGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            SpawnGrid.CommitEdit(DataGridEditingUnit.Row, true);
            ApplySpawnsToScenario();
            PackageStore.SaveDefinition(_package, _scenario);
        }

        LoadRoot(_package.Root);
        RefreshCharList(keep);
        StatusText.Text = "已保存人物 " + keep;
    }

    static JsonArray LinesToArray(string? text)
    {
        var arr = new JsonArray();
        foreach (var line in (text ?? "").Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            arr.Add(line);
        return arr;
    }

    static int ParseInt(string? text, int fallback) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;

    void ScenarioBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_package == null || ScenarioBox.SelectedItem is not string id) return;
        _scenario = _package.Find(id);
        _spawns = new ObservableCollection<SpawnRow>();
        if (_scenario?.Raw["spawns"] is JsonArray arr)
        {
            foreach (var node in arr)
            {
                if (node is not JsonObject o) continue;
                var row = SpawnRow.FromJson(o);
                row.ControllableChanged += OnSpawnControllableChanged;
                _spawns.Add(row);
            }
        }

        SpawnGrid.ItemsSource = _spawns;
        SpawnHint.Text = $"场景 {_scenario?.Id} 出场 {_spawns.Count} 条 · 可控制 {_spawns.Count(s => s.Controllable)}";
        var keep = CharList.SelectedItem is CharListItem c ? c.Id : null;
        RefreshCharList(keep);
    }

    void OnSpawnControllableChanged(SpawnRow row)
    {
        if (_package == null) return;
        var def = _package.Find(row.DefinitionId);
        if (def != null)
            def.Raw["playerControllable"] = row.Controllable;

        if (_character != null &&
            string.Equals(JsonEdit.GetString(_character.Raw, "id"), row.DefinitionId, StringComparison.Ordinal))
        {
            _suppressControllableUi = true;
            ControllableBox.IsChecked = row.Controllable;
            _suppressControllableUi = false;
        }

        RefreshCharList(row.DefinitionId);
        SpawnHint.Text = $"场景 {_scenario?.Id} 出场 {_spawns.Count} 条 · 可控制 {_spawns.Count(s => s.Controllable)}（未保存出场前仅内存）";
    }

    void SpawnGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SpawnGrid.SelectedItem is not SpawnRow row || string.IsNullOrEmpty(row.DefinitionId)) return;
        if (CharList.ItemsSource is IEnumerable<CharListItem> items)
        {
            var hit = items.FirstOrDefault(i => i.Id == row.DefinitionId);
            if (hit != null) CharList.SelectedItem = hit;
        }
    }

    void HighlightSpawnForCharacter(string definitionId)
    {
        for (var i = 0; i < _spawns.Count; i++)
        {
            if (!string.Equals(_spawns[i].DefinitionId, definitionId, StringComparison.Ordinal))
                continue;
            SpawnGrid.SelectedIndex = i;
            SpawnGrid.ScrollIntoView(_spawns[i]);
            return;
        }
    }

    void AddToScenario_Click(object sender, RoutedEventArgs e)
    {
        if (_character == null)
        {
            MessageBox.Show("先选人物");
            return;
        }

        if (_scenario == null)
        {
            MessageBox.Show("先选开局场景");
            return;
        }

        var id = JsonEdit.GetString(_character.Raw, "id");
        if (_spawns.Any(s => string.Equals(s.DefinitionId, id, StringComparison.Ordinal)))
        {
            MessageBox.Show("该人物已在场景出场列表中");
            return;
        }

        var controllable = ControllableBox.IsChecked == true ||
                           JsonEdit.GetBool(_character.Raw, "playerControllable", false);
        var row = SpawnRow.FromJson(new JsonObject
        {
            ["definitionId"] = id,
            ["displayName"] = JsonEdit.GetString(_character.Raw, "name"),
            ["entityKind"] = controllable ? "character" : "npc",
            ["scheduleId"] = "base:schedule_mortal_day",
            ["aiRole"] = "Mortal",
            ["factionRole"] = "LaborDisciple",
            ["assignOpeningFaction"] = true,
            ["bindSchedule"] = true,
            ["bindDailyTask"] = true
        });
        row.ControllableChanged += OnSpawnControllableChanged;
        _spawns.Add(row);
        SpawnGrid.ItemsSource = null;
        SpawnGrid.ItemsSource = _spawns;
        RefreshCharList(id);
        StatusText.Text = "已加入出场列表（记得点「保存场景出场」）";
    }

    void SaveSpawn_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null || _scenario == null)
        {
            MessageBox.Show("没有选中场景");
            return;
        }

        if (!TryPickSavePath("保存场景出场（另存为）", _scenario.FilePath, out var targetPath))
            return;

        try
        {
            SpawnGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            SpawnGrid.CommitEdit(DataGridEditingUnit.Row, true);

            foreach (var row in _spawns)
            {
                var def = _package.Find(row.DefinitionId);
                if (def == null) continue;
                def.Raw["playerControllable"] = row.Controllable;
                PackageStore.SaveDefinition(_package, def);
            }

            ApplySpawnsToScenario();
            WriteDefinitionToPath(_package, _scenario, targetPath);
            ExportLevelTesterRoster(quiet: true, targetPath: null);
            var keep = CharList.SelectedItem is CharListItem c ? c.Id : null;
            RefreshCharList(keep);
            StatusText.Text = "已保存场景出场 → " + targetPath;
        }
        catch (Exception ex)
        {
            MessageBox.Show("保存场景出场失败：\n" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void ExportRoster_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null)
        {
            MessageBox.Show("没有打开包");
            return;
        }

        if (_spawns.Count == 0)
        {
            MessageBox.Show("当前场景出场为空：先选开局场景，把人加入出场，再导出。");
            return;
        }

        var defaultPath = _package.Find(LevelTesterRosterId)?.FilePath
                          ?? System.IO.Path.Combine(_package.Root, "Data", LevelTesterRosterRelPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        if (!TryPickSavePath("导出 Level Tester 名册（另存为）", defaultPath, out var targetPath))
            return;

        SpawnGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        SpawnGrid.CommitEdit(DataGridEditingUnit.Row, true);
        try
        {
            ExportLevelTesterRoster(quiet: false, targetPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show("导出名册失败：\n" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>把出场表写回当前场景 Raw，并用深拷贝避免「节点已有父级」崩溃。</summary>
    void ApplySpawnsToScenario()
    {
        if (_scenario == null) return;
        var arr = new JsonArray();
        foreach (var row in _spawns)
        {
            var clone = row.SnapshotJson();
            arr.Add(clone);
            row.AttachRaw(clone);
        }

        _scenario.Raw["spawns"] = arr;
    }

    static bool TryPickSavePath(string title, string defaultPath, out string path)
    {
        path = "";
        var dir = System.IO.Path.GetDirectoryName(defaultPath);
        var dlg = new SaveFileDialog
        {
            Title = title,
            Filter = "JSON (*.json)|*.json|所有文件|*.*",
            DefaultExt = ".json",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = System.IO.Path.GetFileName(defaultPath)
        };
        if (!string.IsNullOrWhiteSpace(dir) && System.IO.Directory.Exists(dir))
            dlg.InitialDirectory = dir;

        if (dlg.ShowDialog() != true)
            return false;
        path = dlg.FileName;
        return true;
    }

    static void WriteDefinitionToPath(ContentPackage package, DefRef def, string targetPath)
    {
        var same = string.Equals(
            System.IO.Path.GetFullPath(targetPath),
            System.IO.Path.GetFullPath(def.FilePath),
            StringComparison.OrdinalIgnoreCase);
        if (same)
        {
            PackageStore.SaveDefinition(package, def);
            return;
        }

        var src = package.Files.FirstOrDefault(f =>
                      string.Equals(f.Path, def.FilePath, StringComparison.OrdinalIgnoreCase))
                  ?? throw new InvalidOperationException("找不到源文件: " + def.FilePath);
        if (def.Index >= 0 && def.Index < src.Definitions.Count)
            src.Definitions[def.Index] = def.Raw;

        var defs = src.Definitions
            .Select(d => (JsonObject)JsonNode.Parse(d.ToJsonString())!)
            .ToList();
        PackageStore.SaveFile(new ContentFile
        {
            Path = targetPath,
            SchemaVersion = src.SchemaVersion,
            Definitions = defs
        });
    }

    void ExportLevelTesterRoster(bool quiet, string? targetPath)
    {
        if (_package == null || _spawns.Count == 0) return;

        var entries = new JsonArray();
        foreach (var row in _spawns)
            entries.Add(row.SnapshotJson());

        var existing = _package.Find(LevelTesterRosterId);
        if (existing != null)
        {
            existing.Raw["id"] = LevelTesterRosterId;
            existing.Raw["type"] = "characterRoster";
            existing.Raw["name"] = "Level Tester 名册";
            existing.Raw["entries"] = entries;
            var path = targetPath ?? existing.FilePath;
            WriteDefinitionToPath(_package, existing, path);
            if (!quiet)
                StatusText.Text = $"已导出名册 {LevelTesterRosterId} → {path}（{_spawns.Count} 人）";
            return;
        }

        var rosterDef = new JsonObject
        {
            ["id"] = LevelTesterRosterId,
            ["type"] = "characterRoster",
            ["name"] = "Level Tester 名册",
            ["entries"] = entries
        };

        var defaultPath = System.IO.Path.Combine(
            _package.Root, "Data", LevelTesterRosterRelPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        var pathNew = targetPath ?? defaultPath;
        if (string.Equals(
                System.IO.Path.GetFullPath(pathNew),
                System.IO.Path.GetFullPath(defaultPath),
                StringComparison.OrdinalIgnoreCase))
        {
            PackageStore.AppendDefinition(_package, LevelTesterRosterRelPath, rosterDef);
            _package = PackageStore.Load(_package.Root);
        }
        else
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(pathNew)!);
            PackageStore.SaveFile(new ContentFile
            {
                Path = pathNew,
                SchemaVersion = 1,
                Definitions = new List<JsonObject> { rosterDef }
            });
        }

        if (!quiet)
            StatusText.Text = $"已导出名册 {LevelTesterRosterId} → {pathNew}（{_spawns.Count} 人）";
    }

    sealed class CharListItem
    {
        public CharListItem(string id, string label)
        {
            Id = id;
            Label = label;
        }

        public string Id { get; }
        public string Label { get; }
    }

    sealed class CapabilityRowUi
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public CheckBox? EnabledBox { get; set; }
        public TextBox? WeightBox { get; set; }
    }

    sealed class SpiritRootRowUi
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public TextBox? ValueBox { get; set; }
    }

    public sealed class SpawnRow : INotifyPropertyChanged
    {
        JsonObject _raw;
        string _definitionId = "";
        string _displayName = "";
        bool _controllable;
        string _scheduleId = "";
        string _aiRole = "Mortal";
        string _factionRole = "";
        bool _bindSchedule = true;

        public SpawnRow(JsonObject raw) => _raw = raw;

        public event Action<SpawnRow>? ControllableChanged;

        public string DefinitionId { get => _definitionId; set { _definitionId = value; OnPropertyChanged(); } }
        public string DisplayName { get => _displayName; set { _displayName = value; OnPropertyChanged(); } }

        public bool Controllable
        {
            get => _controllable;
            set
            {
                if (_controllable == value) return;
                _controllable = value;
                OnPropertyChanged();
                ControllableChanged?.Invoke(this);
            }
        }

        public string ScheduleId { get => _scheduleId; set { _scheduleId = value; OnPropertyChanged(); } }

        public string AiRoleLabel
        {
            get => UiLabels.ToLabel(UiLabels.AiRoles, _aiRole, "凡人倾向");
            set
            {
                _aiRole = UiLabels.ToKey(UiLabels.AiRoles, value, "Mortal");
                OnPropertyChanged();
            }
        }

        public string FactionRoleLabel
        {
            get => UiLabels.ToLabel(UiLabels.FactionRoles, _factionRole, "劳役弟子");
            set
            {
                _factionRole = UiLabels.ToKey(UiLabels.FactionRoles, value, "LaborDisciple");
                OnPropertyChanged();
            }
        }

        public bool BindSchedule { get => _bindSchedule; set { _bindSchedule = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string? n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public static SpawnRow FromJson(JsonObject o)
        {
            var kind = JsonEdit.GetString(o, "entityKind", "npc");
            return new SpawnRow(o)
            {
                DefinitionId = JsonEdit.GetString(o, "definitionId"),
                DisplayName = JsonEdit.GetString(o, "displayName"),
                _controllable = string.Equals(kind, "character", StringComparison.OrdinalIgnoreCase),
                ScheduleId = JsonEdit.GetString(o, "scheduleId"),
                _aiRole = JsonEdit.GetString(o, "aiRole", "Mortal"),
                _factionRole = JsonEdit.GetString(o, "factionRole"),
                BindSchedule = o["bindSchedule"] is null || JsonEdit.GetBool(o, "bindSchedule", true)
            };
        }

        public void AttachRaw(JsonObject raw) => _raw = raw;

        /// <summary>把 UI 字段写回 _raw，并返回深拷贝（可安全加入新 JsonArray）。</summary>
        public JsonObject SnapshotJson()
        {
            ApplyToRaw();
            return (JsonObject)_raw.DeepClone()!;
        }

        public JsonObject ToJson()
        {
            ApplyToRaw();
            return _raw;
        }

        void ApplyToRaw()
        {
            _raw["definitionId"] = DefinitionId;
            _raw["entityKind"] = Controllable ? "character" : "npc";
            _raw["bindSchedule"] = BindSchedule;
            _raw.Remove("jobId");
            JsonEdit.SetString(_raw, "displayName", DisplayName);
            JsonEdit.SetString(_raw, "scheduleId", ScheduleId);
            JsonEdit.SetString(_raw, "aiRole", _aiRole);
            if (!string.IsNullOrWhiteSpace(_factionRole))
            {
                _raw["factionRole"] = _factionRole;
                if (_raw["assignOpeningFaction"] is null)
                    _raw["assignOpeningFaction"] = true;
            }
        }
    }
}
