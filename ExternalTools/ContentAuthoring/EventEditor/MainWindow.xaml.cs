using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ContentAuthoring.Shared;
using Microsoft.Win32;

namespace EventEditor;

public partial class MainWindow : Window
{
    ContentPackage? _package;
    GraphLayoutStore? _layoutStore;
    EditorSession? _session;
    EditorSession.Snapshot? _pendingGraphSnapshot;
    GraphSelection? _inspectedSelection;
    bool _loading;
    string? _selectedBrowserId;

    sealed record ValidationRow(string Display, string? StepId, string? ChoiceId);

    public MainWindow()
    {
        InitializeComponent();
        EventTriggerBox.ItemsSource = UiLabels.Labels(UiLabels.EventTriggers);
        WorldObjectKindBox.ItemsSource = UiLabels.Labels(UiLabels.WorldObjectKinds);
        RepeatBox.ItemsSource = new[] { "可重复", "全局一次", "每个目标一次", "每个角色×目标一次" };
        SpeakerBox.ItemsSource = new[] { "旁白", "当前玩家角色", "当前互动对象", "指定人物…" };
        UnavailableBox.ItemsSource = new[] { "显示但禁用", "隐藏" };
        EventConditionEditor.Changed += ArrayEditor_Changed;
        StepOutcomeEditor.Changed += ArrayEditor_Changed;
        ChoiceConditionEditor.Changed += ArrayEditor_Changed;
        ChoiceOutcomeEditor.Changed += ArrayEditor_Changed;
        FlowGraph.MutationStarting += (_, _) => { if (_session != null) _pendingGraphSnapshot = _session.Capture(); };
        FlowGraph.Mutated += (_, _) =>
        {
            if (_session != null && _pendingGraphSnapshot is EditorSession.Snapshot snapshot) _session.MarkGraphMutation(snapshot);
            _pendingGraphSnapshot = null; UpdateGraphChrome(); LoadInspector(FlowGraph.Selection);
        };
        FlowGraph.SelectionChanged += (_, _) =>
        {
            CommitInspector();
            _inspectedSelection = FlowGraph.Selection;
            LoadInspector(_inspectedSelection);
        };
        FlowGraph.FocusTextRequested += (_, _) => { LoadInspector(FlowGraph.Selection); StepTextBox.Focus(); };
        var root = PackagePaths.FindDefaultBaseGame();
        if (root != null) LoadRoot(root);
        else StatusText.Text = "未找到默认内容包，请点“打开包…”";
    }

    static string S(JsonObject obj, string key, string fallback = "") => JsonEdit.GetString(obj, key, fallback);
    JsonArray Steps => _session?.Working["steps"] as JsonArray ?? new JsonArray();

    void LoadRoot(string root, string? selectId = null)
    {
        _loading = true;
        _package = PackageStore.Load(root);
        _layoutStore = new GraphLayoutStore(root);
        _session = null; _inspectedSelection = null;
        RootText.Text = root;
        LayoutPathText.Text = _layoutStore.Path;
        var characters = PackageStore.AllCharacterIds(_package);
        NpcDefBox.ItemsSource = characters;
        SpeakerDefBox.ItemsSource = characters;
        LocationBox.ItemsSource = new[] { "" }.Concat(PackageStore.AllLocationIds(_package)).ToList();
        EventConditionEditor.Configure(_package, JsonArrayEditorMode.Condition, "触发条件");
        StepOutcomeEditor.Configure(_package, JsonArrayEditorMode.Outcome, "步骤结果");
        ChoiceConditionEditor.Configure(_package, JsonArrayEditorMode.Condition, "选项条件");
        ChoiceOutcomeEditor.Configure(_package, JsonArrayEditorMode.Outcome, "选项结果");
        _loading = false;
        RebuildBrowser(selectId);
        UpdateTitle();
        StatusText.Text = $"事件 {_package.OfType("contentEvent").Count()} 条 · 默认按对象浏览";
    }

    void RebuildBrowser(string? selectId = null)
    {
        if (_package == null || BrowserTree == null) return;
        _loading = true;
        BrowserTree.Items.Clear();
        var events = _package.OfType("contentEvent").Where(MatchesSearch).ToList();
        if (ByEventRadio.IsChecked == true)
        {
            foreach (var ev in events.OrderBy(DisplayName, StringComparer.CurrentCulture)) BrowserTree.Items.Add(EventItem(ev));
        }
        else
        {
            var people = GroupItem("人物");
            foreach (var group in events.Where(e => S(e.Raw, "trigger") == "onTalk").GroupBy(e => S(e.Raw, "npcDefinitionId")).OrderBy(g => DisplayDefinition(g.Key)))
            {
                var owner = GroupItem(DisplayDefinition(group.Key));
                foreach (var ev in group.OrderByDescending(e => JsonEdit.GetInt(e.Raw, "priority")).ThenBy(DisplayName, StringComparer.CurrentCulture)) owner.Items.Add(EventItem(ev));
                people.Items.Add(owner);
            }
            if (people.Items.Count > 0) { people.IsExpanded = true; BrowserTree.Items.Add(people); }
            var objects = GroupItem("世界物体");
            foreach (var group in events.Where(e => S(e.Raw, "trigger") == "onInspect").GroupBy(e => S(e.Raw, "worldObjectKind") + "\u001f" + S(e.Raw, "worldObjectId")).OrderBy(g => g.Key))
            {
                var parts = group.Key.Split('\u001f'); var label = parts.Length > 1 && parts[1].Length > 0 ? DisplayDefinition(parts[1]) : UiLabels.ToLabel(UiLabels.WorldObjectKinds, parts[0], parts[0]);
                var owner = GroupItem(label);
                foreach (var ev in group.OrderBy(DisplayName, StringComparer.CurrentCulture)) owner.Items.Add(EventItem(ev));
                objects.Items.Add(owner);
            }
            if (objects.Items.Count > 0) { objects.IsExpanded = true; BrowserTree.Items.Add(objects); }
            var other = GroupItem("其他事件");
            foreach (var ev in events.Where(e => S(e.Raw, "trigger") is not ("onTalk" or "onInspect")).OrderBy(DisplayName, StringComparer.CurrentCulture)) other.Items.Add(EventItem(ev));
            if (other.Items.Count > 0) { other.IsExpanded = true; BrowserTree.Items.Add(other); }
        }
        _loading = false;
        if (selectId != null) SelectBrowserItem(selectId);
        else if (_session == null) SelectFirstEvent(BrowserTree.Items);
    }

    bool MatchesSearch(DefRef ev)
    {
        var q = SearchBox?.Text?.Trim() ?? "";
        if (q.Length == 0) return true;
        var haystack = string.Join("\n", ev.Name, ev.Id, S(ev.Raw, "topicText"), S(ev.Raw, "npcDefinitionId"), S(ev.Raw, "worldObjectKind"), S(ev.Raw, "worldObjectId"), S(ev.Raw, "trigger"));
        return haystack.Contains(q, StringComparison.CurrentCultureIgnoreCase);
    }

    string DisplayName(DefRef ev)
    {
        var normal = S(ev.Raw, "trigger") == "onTalk" && JsonEdit.GetInt(ev.Raw, "priority") == 0 && !JsonEdit.GetBool(ev.Raw, "once", true);
        var title = S(ev.Raw, "topicText", ev.Name);
        if (string.IsNullOrWhiteSpace(title)) title = ev.Name;
        return normal ? "★ 普通 / 保底对话 · " + title : title;
    }

    string DisplayDefinition(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return "未指定对象";
        var def = _package?.Find(id);
        return def == null || string.IsNullOrWhiteSpace(def.Name) ? id : def.Name + "  ·  " + id;
    }

    TreeViewItem EventItem(DefRef ev)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = DisplayName(ev), FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = ev.Id, Foreground = System.Windows.Media.Brushes.Gray, FontSize = 10, TextTrimming = TextTrimming.CharacterEllipsis });
        return new TreeViewItem { Header = panel, Tag = ev, IsExpanded = true };
    }
    static TreeViewItem GroupItem(string header) => new() { Header = header, FontWeight = FontWeights.SemiBold };

    void SelectFirstEvent(ItemCollection items)
    {
        foreach (var obj in items)
        {
            if (obj is not TreeViewItem item) continue;
            if (item.Tag is DefRef) { item.IsSelected = true; return; }
            SelectFirstEvent(item.Items);
            if (_session != null) return;
        }
    }
    bool SelectBrowserItem(string id)
    {
        bool Find(ItemCollection items)
        {
            foreach (var obj in items)
            {
                if (obj is not TreeViewItem item) continue;
                if (item.Tag is DefRef def && def.Id == id) { item.IsSelected = true; item.BringIntoView(); return true; }
                if (Find(item.Items)) { item.IsExpanded = true; return true; }
            }
            return false;
        }
        return Find(BrowserTree.Items);
    }

    void BrowserTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_loading || e.NewValue is not TreeViewItem { Tag: DefRef def }) return;
        if (_session?.Source == def) return;
        var targetId = def.Id;
        if (!TryLeaveCurrent()) { RebuildBrowser(_selectedBrowserId); return; }
        LoadDocument(_package?.Find(targetId) ?? def);
    }

    void LoadDocument(DefRef def)
    {
        if (_layoutStore == null) return;
        var working = (JsonObject)def.Raw.DeepClone();
        var ids = (working["steps"] as JsonArray ?? new JsonArray()).OfType<JsonObject>().Select(s => S(s, "id"));
        SetSession(new EditorSession(def, working, _layoutStore.Get(def.Id, ids)));
        _selectedBrowserId = def.Id;
        StatusText.Text = "已打开：" + DisplayName(def);
    }

    void SetSession(EditorSession session)
    {
        if (_session != null) _session.Changed -= Session_Changed;
        _session = session; _session.Changed += Session_Changed;
        _inspectedSelection = null;
        ReloadGraph();
        ShowEventInspector();
        UpdateTitle();
    }

    void ReloadGraph()
    {
        if (_session == null) return;
        var hasSteps = _session.Working.ContainsKey("steps");
        LegacyBanner.Visibility = hasSteps ? Visibility.Collapsed : Visibility.Visible;
        FlowGraph.Visibility = hasSteps ? Visibility.Visible : Visibility.Collapsed;
        EmptyGraphHint.Visibility = hasSteps && Steps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (hasSteps) FlowGraph.Load(_session.Working, _session.Layout, SpeakerLabel);
        UpdateGraphChrome();
    }

    string SpeakerLabel(string speaker) => speaker switch
    {
        "" => "旁白",
        "@actor" => "当前玩家角色",
        "@target" => "当前互动对象" + (_session == null ? "" : BindingSuffix(_session.Working)),
        _ => DisplayDefinition(speaker)
    };
    string BindingSuffix(JsonObject raw)
    {
        var id = S(raw, "npcDefinitionId");
        if (id.Length == 0) return "";
        var name = _package?.Find(id)?.Name;
        return string.IsNullOrWhiteSpace(name) ? "" : "（" + name + "）";
    }

    void UpdateGraphChrome()
    {
        if (_session == null) { GraphTitleText.Text = "请选择事件"; GraphSummaryText.Text = ""; return; }
        var raw = _session.Working;
        GraphTitleText.Text = S(raw, "name", "未命名事件");
        var trigger = S(raw, "trigger", "manual");
        var binding = trigger switch
        {
            "onTalk" => "与【" + DisplayDefinition(S(raw, "npcDefinitionId")) + "】交谈",
            "onInspect" => "调查【" + (S(raw, "worldObjectId").Length > 0 ? DisplayDefinition(S(raw, "worldObjectId")) : UiLabels.ToLabel(UiLabels.WorldObjectKinds, S(raw, "worldObjectKind"), "世界物体")) + "】",
            _ => UiLabels.ToLabel(UiLabels.EventTriggers, trigger, trigger)
        };
        GraphSummaryText.Text = $"{binding} · Priority {JsonEdit.GetInt(raw, "priority")} · {RepeatLabel(raw)} · 条件 {(raw["conditions"] as JsonArray)?.Count ?? 0}";
    }
    static string RepeatLabel(JsonObject raw) => !JsonEdit.GetBool(raw, "once", true) ? "可重复" : S(raw, "onceScope", "global") switch { "perTarget" => "每目标一次", "perActorTarget" => "每角色×目标一次", _ => "全局一次" };

    void ShowEventInspector()
    {
        CommitInspector(); _inspectedSelection = null;
        InspectorHint.Visibility = _session == null ? Visibility.Visible : Visibility.Collapsed;
        EventInspector.Visibility = _session == null ? Visibility.Collapsed : Visibility.Visible;
        StepInspector.Visibility = ChoiceInspector.Visibility = Visibility.Collapsed;
        InspectorTitle.Text = "Event 设置";
        if (_session == null) return;
        _loading = true;
        var raw = _session.Working;
        EventNameBox.Text = S(raw, "name"); EventTopicBox.Text = S(raw, "topicText"); EventIdBox.Text = S(raw, "id");
        EventTriggerBox.SelectedItem = UiLabels.ToLabel(UiLabels.EventTriggers, S(raw, "trigger", "manual"), "手动");
        NpcDefBox.Text = S(raw, "npcDefinitionId");
        WorldObjectKindBox.SelectedItem = UiLabels.ToLabel(UiLabels.WorldObjectKinds, S(raw, "worldObjectKind", "controlCore"), "控制核心");
        RefreshWorldObjectIds(S(raw, "worldObjectId"));
        PriorityBox.Text = JsonEdit.GetInt(raw, "priority").ToString();
        RepeatBox.SelectedIndex = !JsonEdit.GetBool(raw, "once", true) ? 0 : S(raw, "onceScope", "global") switch { "perTarget" => 2, "perActorTarget" => 3, _ => 1 };
        EventConditionEditor.LoadFrom(raw["conditions"]); LocationBox.Text = S(raw, "locationId"); QuestIdBox.Text = S(raw, "questId");
        _loading = false; UpdateBindingUi();
    }

    void LoadInspector(GraphSelection? selection)
    {
        if (_session == null || selection == null) { ShowEventInspector(); return; }
        var step = FindStep(selection.StepId); if (step == null) { ShowEventInspector(); return; }
        _loading = true;
        InspectorHint.Visibility = EventInspector.Visibility = Visibility.Collapsed;
        if (selection.ChoiceId == null)
        {
            StepInspector.Visibility = Visibility.Visible; ChoiceInspector.Visibility = Visibility.Collapsed; InspectorTitle.Text = "Step";
            var speaker = S(step, "speakerRef"); SpeakerBox.SelectedIndex = speaker switch { "" => 0, "@actor" => 1, "@target" => 2, _ => 3 };
            SpeakerDefBox.Text = SpeakerBox.SelectedIndex == 3 ? speaker : ""; SpeakerDefBox.IsEnabled = SpeakerBox.SelectedIndex == 3;
            StepTextBox.Text = S(step, "text"); StepOutcomeEditor.LoadFrom(step["outcomes"]); StepIdText.Text = S(step, "id");
        }
        else
        {
            StepInspector.Visibility = Visibility.Collapsed; ChoiceInspector.Visibility = Visibility.Visible; InspectorTitle.Text = "Choice";
            var choice = FindChoice(step, selection.ChoiceId); if (choice != null)
            {
                ChoiceTextBox.Text = S(choice, "text"); ChoiceConditionEditor.LoadFrom(choice["conditions"]); ChoiceOutcomeEditor.LoadFrom(choice["outcomes"]);
                UnavailableBox.SelectedIndex = S(choice, "unavailableMode", "disabled") == "hidden" ? 1 : 0; RequirementBox.Text = S(choice, "requirementText"); ChoiceIdText.Text = S(choice, "id");
            }
        }
        _loading = false;
    }

    void CommitInspector()
    {
        if (_loading || _session == null) return;
        var candidate = (JsonObject)_session.Working.DeepClone();
        if (EventInspector.Visibility == Visibility.Visible)
        {
            if (!int.TryParse(PriorityBox.Text, out var priority)) priority = 0;
            candidate["name"] = EventNameBox.Text; candidate["topicText"] = EventTopicBox.Text; candidate["priority"] = priority;
            var trigger = UiLabels.ToKey(UiLabels.EventTriggers, EventTriggerBox.SelectedItem as string ?? EventTriggerBox.Text, "manual"); candidate["trigger"] = trigger;
            JsonEdit.SetString(candidate, "locationId", LocationBox.Text); JsonEdit.SetString(candidate, "questId", QuestIdBox.Text);
            if (trigger == "onTalk") { JsonEdit.SetString(candidate, "npcDefinitionId", NpcDefBox.Text); candidate.Remove("worldObjectKind"); candidate.Remove("worldObjectId"); }
            else if (trigger == "onInspect") { candidate.Remove("npcDefinitionId"); JsonEdit.SetString(candidate, "worldObjectKind", UiLabels.ToKey(UiLabels.WorldObjectKinds, WorldObjectKindBox.SelectedItem as string ?? WorldObjectKindBox.Text, "controlCore")); JsonEdit.SetString(candidate, "worldObjectId", WorldObjectIdBox.Text); }
            else { candidate.Remove("npcDefinitionId"); candidate.Remove("worldObjectKind"); candidate.Remove("worldObjectId"); }
            candidate["once"] = RepeatBox.SelectedIndex != 0; candidate["onceScope"] = RepeatBox.SelectedIndex switch { 2 => "perTarget", 3 => "perActorTarget", _ => "global" };
            candidate["conditions"] = EventConditionEditor.ToJsonArray();
        }
        else if (_inspectedSelection != null)
        {
            var step = FindStep(candidate, _inspectedSelection.StepId);
            if (step != null && _inspectedSelection.ChoiceId == null && StepInspector.Visibility == Visibility.Visible)
            {
                step["speakerRef"] = SpeakerBox.SelectedIndex switch { 1 => "@actor", 2 => "@target", 3 => SpeakerDefBox.Text.Trim(), _ => "" };
                step["text"] = StepTextBox.Text; step["outcomes"] = StepOutcomeEditor.ToJsonArray();
            }
            else if (step != null && _inspectedSelection.ChoiceId != null && ChoiceInspector.Visibility == Visibility.Visible)
            {
                var choice = FindChoice(step, _inspectedSelection.ChoiceId);
                if (choice != null) { choice["text"] = ChoiceTextBox.Text; choice["conditions"] = ChoiceConditionEditor.ToJsonArray(); choice["outcomes"] = ChoiceOutcomeEditor.ToJsonArray(); choice["unavailableMode"] = UnavailableBox.SelectedIndex == 1 ? "hidden" : "disabled"; choice["requirementText"] = RequirementBox.Text; }
            }
        }
        if (candidate.ToJsonString() != _session.Working.ToJsonString())
        {
            _session.Mutate((raw, _) => ReplaceObject(raw, candidate));
            var selection = _inspectedSelection; FlowGraph.Load(_session.Working, _session.Layout, SpeakerLabel); if (selection != null) FlowGraph.Select(selection.StepId, selection.ChoiceId, false);
            UpdateGraphChrome();
        }
    }

    static void ReplaceObject(JsonObject target, JsonObject source) { target.Clear(); foreach (var pair in source) target[pair.Key] = pair.Value?.DeepClone(); }
    JsonObject? FindStep(string id) => FindStep(_session?.Working, id);
    static JsonObject? FindStep(JsonObject? raw, string id) => (raw?["steps"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(s => S(s, "id") == id);
    static JsonObject? FindChoice(JsonObject step, string id) => (step["choices"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(c => S(c, "id") == id);

    bool TryLeaveCurrent()
    {
        CommitInspector();
        if (_session?.IsDirty != true) return true;
        var answer = MessageBox.Show("当前事件有未保存修改。", "保存修改？", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Cancel) return false;
        if (answer == MessageBoxResult.Yes) return SaveDocument();
        return true;
    }

    bool SaveDocument()
    {
        if (_session == null || _package == null || _layoutStore == null) return false;
        CommitInspector();
        var issues = EventAuthoringValidator.Validate(_session.Working, _package);
        ShowValidation(issues);
        if (issues.Count > 0) { StatusText.Text = $"保存被阻止：{issues.Count} 个问题"; return false; }
        try
        {
            var id = S(_session.Working, "id");
            if (_package.Definitions.Any(d => d != _session.Source && d.Id == id)) throw new InvalidOperationException("事件标识已存在：" + id);
            DefRef saved;
            if (_session.Source == null) saved = PackageStore.AppendDefinition(_package, "Events/content_events.json", (JsonObject)_session.Working.DeepClone());
            else
            {
                var old = _session.Source.Raw; _session.Source.Raw = (JsonObject)_session.Working.DeepClone();
                try { PackageStore.SaveDefinition(_package, _session.Source); }
                catch { _session.Source.Raw = old; throw; }
                saved = _session.Source;
            }
            _session.Layout.EventId = id;
            _layoutStore.Save(_session.Layout, _package.OfType("contentEvent").Select(e => e.Id).Append(id));
            _session.AcceptSaved(saved);
            var root = _package.Root; LoadRoot(root, id);
            StatusText.Text = "已验证并安全保存：" + id;
            return true;
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning); return false; }
    }

    void ShowValidation(List<string> issues)
    {
        var rows = new List<ValidationRow>(); var errorSteps = new HashSet<string>(StringComparer.Ordinal);
        foreach (var issue in issues)
        {
            string? stepId = null, choiceId = null;
            foreach (var step in Steps.OfType<JsonObject>())
            {
                var id = S(step, "id"); if (issue.Contains(id, StringComparison.Ordinal)) stepId = id;
                if (step["choices"] is JsonArray choices) foreach (var choice in choices.OfType<JsonObject>()) { var cid = S(choice, "id"); if (issue.Contains(cid, StringComparison.Ordinal)) { stepId = id; choiceId = cid; } }
            }
            if (stepId != null) errorSteps.Add(stepId);
            rows.Add(new ValidationRow((stepId == null ? "● Event\n  " : "● Step：" + StepLabel(stepId) + "\n  ") + issue, stepId, choiceId));
        }
        ErrorList.ItemsSource = rows; ErrorExpander.Header = issues.Count == 0 ? "验证通过" : $"{issues.Count} 个问题"; ErrorExpander.IsExpanded = issues.Count > 0; FlowGraph.SetErrors(errorSteps);
    }
    string StepLabel(string id) { var text = S(FindStep(id) ?? new JsonObject(), "text"); return text.Length > 18 ? text[..18] + "…" : text.Length == 0 ? id : text; }

    void NewEvent_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null || !TryLeaveCurrent()) return;
        var dialog = new NewEventDialog(_package) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        var idBase = "base:event_new_" + DateTime.Now.ToString("yyyyMMddHHmmss"); var id = idBase; var suffix = 2;
        while (_package.Find(id) != null) id = idBase + "_" + suffix++;
        var trigger = dialog.EventKind is "npcNormal" or "npcSpecial" ? "onTalk" : dialog.EventKind == "object" ? "onInspect" : "manual";
        var speaker = trigger == "onTalk" ? "@target" : "";
        var raw = new JsonObject { ["id"] = id, ["type"] = "contentEvent", ["name"] = dialog.EventName, ["trigger"] = trigger, ["priority"] = 0, ["topicText"] = dialog.EventName, ["once"] = dialog.EventKind != "npcNormal", ["onceScope"] = "global", ["conditions"] = new JsonArray(), ["entryStepId"] = "step_001", ["steps"] = new JsonArray(new JsonObject { ["id"] = "step_001", ["speakerRef"] = speaker, ["text"] = "", ["outcomes"] = new JsonArray(), ["choices"] = new JsonArray() }) };
        if (trigger == "onTalk") raw["npcDefinitionId"] = dialog.NpcDefinitionId;
        if (trigger == "onInspect") { raw["worldObjectKind"] = dialog.WorldObjectKind; JsonEdit.SetString(raw, "worldObjectId", dialog.WorldObjectId); }
        var layout = new EventGraphLayout { EventId = id }; layout.GetOrCreate("step_001").X = 120; layout.GetOrCreate("step_001").Y = 120;
        var session = new EditorSession(null, raw, layout); SetSession(session);
        _selectedBrowserId = null; FlowGraph.Select("step_001"); StepTextBox.Focus(); StatusText.Text = "新事件草稿尚未写入磁盘；首次点击保存才会创建文件内容。";
    }

    void ConvertLegacy_Click(object sender, RoutedEventArgs e)
    {
        if (_session == null || _session.Working.ContainsKey("steps")) return;
        _session.Mutate((raw, layout) =>
        {
            var choices = raw["choices"]?.DeepClone() as JsonArray ?? new JsonArray();
            foreach (var choice in choices.OfType<JsonObject>()) { if (!choice.ContainsKey("conditions")) choice["conditions"] = new JsonArray(); if (!choice.ContainsKey("outcomes")) choice["outcomes"] = new JsonArray(); choice["nextStepId"] = ""; }
            var step = new JsonObject { ["id"] = "step_001", ["speakerRef"] = S(raw, "trigger") == "onTalk" ? "@target" : "", ["text"] = S(raw, "body"), ["outcomes"] = new JsonArray(), ["choices"] = choices };
            raw.Remove("body"); raw.Remove("choices"); raw["entryStepId"] = "step_001"; raw["steps"] = new JsonArray(step);
            layout.EventId = S(raw, "id"); var node = layout.GetOrCreate("step_001"); node.X = 120; node.Y = 120;
        });
        ReloadGraph(); FlowGraph.Select("step_001"); StatusText.Text = "已转换为 Steps working copy；保存前仍可放弃。";
    }

    void Session_Changed(object? sender, EventArgs e) { UpdateTitle(); UpdateGraphChrome(); UndoButton.IsEnabled = _session?.CanUndo == true; RedoButton.IsEnabled = _session?.CanRedo == true; }
    void ArrayEditor_Changed(object? sender, EventArgs e) { if (!_loading) Dispatcher.BeginInvoke(CommitInspector); }
    void UpdateTitle() { var dirty = _session?.IsDirty == true ? " *" : ""; Title = "XianXia · Event Editor" + dirty; SaveButton.IsEnabled = _session != null; UndoButton.IsEnabled = _session?.CanUndo == true; RedoButton.IsEnabled = _session?.CanRedo == true; }

    void OpenPackage_Click(object sender, RoutedEventArgs e) { if (!TryLeaveCurrent()) return; var dialog = new OpenFolderDialog { Title = "选择内容包目录" }; if (dialog.ShowDialog() == true) LoadRoot(dialog.FolderName); }
    void Save_Click(object sender, RoutedEventArgs e) => SaveDocument();
    void Undo_Click(object sender, RoutedEventArgs e) { CommitInspector(); _session?.Undo(); ReloadGraph(); ShowEventInspector(); }
    void Redo_Click(object sender, RoutedEventArgs e) { CommitInspector(); _session?.Redo(); ReloadGraph(); ShowEventInspector(); }
    void AutoLayout_Click(object sender, RoutedEventArgs e) { CommitInspector(); FlowGraph.AutoLayout(); }
    void ZoomFit_Click(object sender, RoutedEventArgs e) => FlowGraph.ZoomToFit();
    void ShowEventInspector_Click(object sender, RoutedEventArgs e) => ShowEventInspector();
    void AddEntryStep_Click(object sender, RoutedEventArgs e) => FlowGraph.AddStep();
    void SetEntry_Click(object sender, RoutedEventArgs e) { if (_inspectedSelection != null) FlowGraph.SetEntry(_inspectedSelection.StepId); }
    void AddChoice_Click(object sender, RoutedEventArgs e) { if (_inspectedSelection != null) FlowGraph.AddChoice(_inspectedSelection.StepId); }
    void DeleteChoice_Click(object sender, RoutedEventArgs e) { if (_inspectedSelection?.ChoiceId is string choiceId) FlowGraph.DeleteChoice(_inspectedSelection.StepId, choiceId); }
    void SearchBox_TextChanged(object sender, TextChangedEventArgs e) { if (!_loading) RebuildBrowser(_selectedBrowserId); }
    void BrowserMode_Changed(object sender, RoutedEventArgs e) { if (!_loading && BrowserTree != null) RebuildBrowser(_selectedBrowserId); }
    void Inspector_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) { if (e.NewFocus is DependencyObject next && IsDescendantOf(next, (DependencyObject)sender)) return; CommitInspector(); }
    static bool IsDescendantOf(DependencyObject child, DependencyObject parent) { for (var current = child; current != null; current = System.Windows.Media.VisualTreeHelper.GetParent(current)) if (ReferenceEquals(current, parent)) return true; return false; }
    void EventTriggerBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (!_loading) UpdateBindingUi(); }
    void WorldObjectKindBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (!_loading) RefreshWorldObjectIds(WorldObjectIdBox.Text); }
    void SpeakerBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (SpeakerDefBox != null) SpeakerDefBox.IsEnabled = SpeakerBox.SelectedIndex == 3; }
    void UpdateBindingUi() { var trigger = UiLabels.ToKey(UiLabels.EventTriggers, EventTriggerBox.SelectedItem as string ?? EventTriggerBox.Text, "manual"); NpcBindingPanel.Visibility = trigger == "onTalk" ? Visibility.Visible : Visibility.Collapsed; ObjectBindingPanel.Visibility = trigger == "onInspect" ? Visibility.Visible : Visibility.Collapsed; }
    void RefreshWorldObjectIds(string keep) { var kind = UiLabels.ToKey(UiLabels.WorldObjectKinds, WorldObjectKindBox.SelectedItem as string ?? WorldObjectKindBox.Text, "controlCore"); WorldObjectIdBox.ItemsSource = _package == null ? new[] { "" } : new[] { "" }.Concat(PackageStore.WorldObjectIds(_package, kind)).ToList(); WorldObjectIdBox.Text = keep ?? ""; }
    void ErrorList_MouseDoubleClick(object sender, MouseButtonEventArgs e) { if (ErrorList.SelectedItem is ValidationRow row && row.StepId != null) FlowGraph.Select(row.StepId, row.ChoiceId); }
    void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { SaveDocument(); e.Handled = true; }
        else if (e.Key == Key.Z && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { Undo_Click(sender, e); e.Handled = true; }
        else if (e.Key == Key.Y && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { Redo_Click(sender, e); e.Handled = true; }
    }
    void Window_Closing(object? sender, CancelEventArgs e) { if (!TryLeaveCurrent()) e.Cancel = true; }
}
