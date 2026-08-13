using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using ContentAuthoring.Shared;
using Microsoft.Win32;
using IOPath = System.IO.Path;

namespace QuestEditor;

public partial class MainWindow : Window
{
    private ContentPackage? _package;
    private DefRef? _quest;
    private bool _loading;

    static readonly (QuestOfferMode Mode, string Label)[] OfferModes =
    [
        (QuestOfferMode.Auto, "自动接取（条件满足）"),
        (QuestOfferMode.AfterQuest, "前置任务完成后自动接"),
        (QuestOfferMode.AtLocation, "到指定地点可领"),
        (QuestOfferMode.NpcDialogue, "NPC 对话发放（关联事件）"),
        (QuestOfferMode.Custom, "自定义（保留当前 JSON 逻辑）")
    ];

    public MainWindow()
    {
        InitializeComponent();
        Title = "XianXia · 任务编辑器";
        OfferModeBox.ItemsSource = OfferModes.Select(x => x.Label).ToList();
        TryLoadDefault();
    }

    private void TryLoadDefault()
    {
        var root = PackagePaths.FindDefaultBaseGame();
        if (root != null) LoadRoot(root);
        else StatusText.Text = "未找到默认 Content/BaseGame，请点「打开包…」";
    }

    private void LoadRoot(string root)
    {
        _package = PackageStore.Load(root);
        RootText.Text = root;
        QuestList.ItemsSource = _package.OfType("quest").Select(d => d.Id).ToList();
        ConfigureEditors();
        if (QuestList.Items.Count > 0) QuestList.SelectedIndex = 0;
        StatusText.Text = $"任务 {_package.OfType("quest").Count()} 条";
    }

    void ConfigureEditors()
    {
        OfferEditor.Configure(_package, JsonArrayEditorMode.Condition, "接取条件");
        CompleteEditor.Configure(_package, JsonArrayEditorMode.Condition, "完成条件");
        RewardsEditor.Configure(_package, JsonArrayEditorMode.Outcome, "奖励");
        FailCondEditor.Configure(_package, JsonArrayEditorMode.Condition, "失败条件");
        FailResEditor.Configure(_package, JsonArrayEditorMode.Outcome, "失败结果");
    }

    void ReloadPickers(string? currentQuestId = null)
    {
        if (_package == null) return;
        var quests = PackageStore.AllQuestIds(_package)
            .Where(id => !string.Equals(id, currentQuestId, StringComparison.Ordinal))
            .ToList();
        PreviousQuestBox.ItemsSource = quests;
        var locs = PackageStore.AllLocationIds(_package);
        OfferLocationBox.ItemsSource = locs;
        NpcLocationBox.ItemsSource = locs;
    }

    private void OpenPackage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "选择 Content/BaseGame 包目录" };
        if (dlg.ShowDialog() == true) LoadRoot(dlg.FolderName);
    }

    private void QuestList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_package == null || QuestList.SelectedItem is not string id) return;
        _quest = _package.Find(id);
        if (_quest == null) return;

        _loading = true;
        try
        {
            ReloadPickers(id);
            IdBox.Text = JsonEdit.GetString(_quest.Raw, "id");
            NameBox.Text = JsonEdit.GetString(_quest.Raw, "name");
            DescBox.Text = JsonEdit.GetString(_quest.Raw, "description");
            AutoOfferBox.IsChecked = JsonEdit.GetBool(_quest.Raw, "autoOffer", true);
            AbandonableBox.IsChecked = JsonEdit.GetBool(_quest.Raw, "abandonable", false);

            OfferEditor.LoadFrom(_quest.Raw["offerConditions"]);
            CompleteEditor.LoadFrom(_quest.Raw["completeConditions"]);
            RewardsEditor.LoadFrom(_quest.Raw["rewards"]);
            FailCondEditor.LoadFrom(_quest.Raw["failConditions"]);
            FailResEditor.LoadFrom(_quest.Raw["failResults"]);

            var mode = QuestOfferService.DetectMode(_package, _quest.Raw);
            SelectOfferMode(mode);
            if (mode == QuestOfferMode.AfterQuest &&
                QuestOfferService.TryGetSingleQuestCompletedOffer(_quest.Raw, out var prev))
                PreviousQuestBox.Text = prev;

            var offerLocs = PackageStore.LocationsOfferingQuest(_package, id);
            if (offerLocs.Count > 0)
            {
                OfferLocationBox.Text = offerLocs[0];
                NpcLocationBox.Text = offerLocs[0];
            }

            var linkedEvents = PackageStore.EventsStartingQuest(_package, id);
            LinkedEventText.Text = linkedEvents.Count > 0
                ? "关联事件: " + string.Join(", ", linkedEvents)
                : "（尚未创建关联事件）";
            LinksText.Text = QuestOfferService.DescribeLinks(_package, _quest.Raw) +
                             "\n保存文件: " + System.IO.Path.GetFileName(_quest.FilePath);
            UpdateOfferPanels();
        }
        finally
        {
            _loading = false;
        }
    }

    void SelectOfferMode(QuestOfferMode mode)
    {
        var label = OfferModes.First(x => x.Mode == mode).Label;
        OfferModeBox.SelectedItem = label;
    }

    QuestOfferMode CurrentOfferMode()
    {
        var label = OfferModeBox.SelectedItem as string ?? OfferModes[0].Label;
        return OfferModes.First(x => x.Label == label).Mode;
    }

    void UpdateOfferPanels()
    {
        if (_loading) return;
        var mode = CurrentOfferMode();
        OfferAfterQuestPanel.Visibility = mode == QuestOfferMode.AfterQuest ? Visibility.Visible : Visibility.Collapsed;
        OfferAtLocationPanel.Visibility = mode == QuestOfferMode.AtLocation ? Visibility.Visible : Visibility.Collapsed;
        OfferNpcPanel.Visibility = mode == QuestOfferMode.NpcDialogue ? Visibility.Visible : Visibility.Collapsed;

        var showOfferEditor = mode is QuestOfferMode.Auto or QuestOfferMode.Custom;
        OfferEditor.IsEnabled = showOfferEditor;
        AutoOfferBox.IsEnabled = mode != QuestOfferMode.AtLocation && mode != QuestOfferMode.NpcDialogue;
        if (mode == QuestOfferMode.AfterQuest || mode == QuestOfferMode.AtLocation || mode == QuestOfferMode.NpcDialogue)
            AutoOfferBox.IsChecked = mode == QuestOfferMode.AfterQuest;
    }

    private void OfferModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateOfferPanels();

    private void EnsureNpcEvent_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null || _quest == null)
        {
            MessageBox.Show("请先选择任务");
            return;
        }

        var loc = NpcLocationBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(loc))
        {
            MessageBox.Show("请填写对话地点 locationId");
            return;
        }

        try
        {
            var ev = QuestOfferService.EnsureNpcQuestEvent(_package, _quest.Raw, loc);
            LoadRoot(_package.Root);
            QuestList.SelectedItem = JsonEdit.GetString(_quest.Raw, "id");
            LinkedEventText.Text = "关联事件: " + ev.Id;
            StatusText.Text = "已创建/更新事件 " + ev.Id + "（台词请在 EventEditor 修改）";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "创建事件失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void NewQuest_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null)
        {
            MessageBox.Show("请先打开 Content/BaseGame 包");
            return;
        }

        var questsDir = ContentPathRules.TypeDataDir(_package.Root, "quest");
        if (string.IsNullOrEmpty(questsDir))
        {
            MessageBox.Show("找不到 Content/BaseGame/Data/Quests，请确认工程路径。");
            return;
        }

        Directory.CreateDirectory(questsDir);
        var defaultId = $"base:quest_{DateTime.Now:MMddHHmm}";
        if (!EditorPrompts.TryPromptText("新建任务", "任务 Id（例如 base:quest_ch01_intro）", defaultId, out var questId))
            return;
        questId = questId.Trim();
        if (string.IsNullOrWhiteSpace(questId))
        {
            MessageBox.Show("Id 不能为空");
            return;
        }

        if (_package.Find(questId) != null)
        {
            var openExisting = MessageBox.Show(
                "已存在 " + questId + "，是否打开现有任务？\n（选「否」则取消新建）",
                "新建任务",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (openExisting == MessageBoxResult.Yes)
            {
                QuestList.SelectedItem = questId;
                return;
            }

            return;
        }

        var dlg = new SaveFileDialog
        {
            Title = "新建任务保存到…",
            Filter = "任务 JSON|*.json",
            InitialDirectory = questsDir,
            FileName = ContentPathRules.SuggestQuestFileName(questId)
        };
        if (dlg.ShowDialog() != true) return;

        if (!EditorPrompts.TryPromptText("新建任务", "显示名称", "新任务", out var name) ||
            string.IsNullOrWhiteSpace(name))
            name = "新任务";

        var raw = CreateEmptyQuestRaw(questId, name.Trim());
        try
        {
            if (File.Exists(dlg.FileName))
            {
                _quest = PackageStore.AppendDefinition(_package, IOPath.GetFileName(dlg.FileName), raw);
            }
            else
            {
                PackageStore.SaveStandaloneDefinition(dlg.FileName, raw);
                _quest = PackageStore.RegisterStandaloneDefinition(_package, dlg.FileName, raw);
            }

            LoadRoot(_package.Root);
            QuestList.SelectedItem = questId;
            StatusText.Text = "已新建 → " + dlg.FileName;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "新建失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    static JsonObject CreateEmptyQuestRaw(string id, string name) => new()
    {
        ["id"] = id,
        ["type"] = "quest",
        ["name"] = name,
        ["description"] = "",
        ["autoOffer"] = true,
        ["offerConditions"] = new JsonArray(),
        ["completeConditions"] = new JsonArray(),
        ["failConditions"] = new JsonArray(),
        ["rewards"] = new JsonArray(),
        ["failResults"] = new JsonArray()
    };

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null || _quest == null)
        {
            MessageBox.Show("没有选中任务");
            return;
        }

        if (!TryApplyEditorToQuest(out var err))
        {
            MessageBox.Show(err, "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            PackageStore.SaveDefinition(_package, _quest);
            var keep = JsonEdit.GetString(_quest.Raw, "id");
            LoadRoot(_package.Root);
            QuestList.SelectedItem = keep;
            StatusText.Text = "已保存 → " + IOPath.GetFileName(_quest.FilePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null || _quest == null)
        {
            MessageBox.Show("没有选中任务");
            return;
        }

        if (!TryApplyEditorToQuest(out var err))
        {
            MessageBox.Show(err, "另存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ContentPathRules.EnsureTypeDir(_package.Root, "quest");
        var questsDir = ContentPathRules.TypeDataDir(_package.Root, "quest");
        if (!string.IsNullOrEmpty(questsDir))
            Directory.CreateDirectory(questsDir);

        var currentId = JsonEdit.GetString(_quest.Raw, "id");
        var dlg = new SaveFileDialog
        {
            Title = "另存为任务 JSON",
            Filter = "任务 JSON|*.json",
            InitialDirectory = questsDir ?? IOPath.GetDirectoryName(_quest.FilePath) ?? "",
            FileName = ContentPathRules.SuggestQuestFileName(currentId)
        };
        if (dlg.ShowDialog() != true) return;

        if (!EditorPrompts.TryPromptText("另存为", "任务 Id（可改成新 id）", currentId, out var newId) ||
            string.IsNullOrWhiteSpace(newId))
            return;
        newId = newId.Trim();

        if (!string.Equals(newId, currentId, StringComparison.Ordinal) && _package.Find(newId) != null)
        {
            MessageBox.Show("Id 已存在: " + newId, "另存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var clone = JsonNode.Parse(_quest.Raw.ToJsonString()) as JsonObject
                    ?? throw new InvalidOperationException("无法克隆任务");
        clone["id"] = newId;

        try
        {
            PackageStore.SaveStandaloneDefinition(dlg.FileName, clone);
            _quest = PackageStore.RegisterStandaloneDefinition(_package, dlg.FileName, clone);
            LoadRoot(_package.Root);
            QuestList.SelectedItem = newId;
            StatusText.Text = "已另存为 → " + dlg.FileName;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "另存失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteQuest_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null || _quest == null)
        {
            MessageBox.Show("没有选中任务");
            return;
        }

        var id = _quest.Id;
        var name = string.IsNullOrWhiteSpace(_quest.Name) ? id : _quest.Name;
        var locs = PackageStore.LocationsOfferingQuest(_package, id);
        var events = PackageStore.EventsStartingQuest(_package, id);
        var extra = "";
        if (locs.Count > 0)
            extra += "\n· 将从地点 questOfferIds 移除引用：" + string.Join(", ", locs);
        if (events.Count > 0)
            extra += "\n· 仍有关联事件（不会自动删除，请到 EventEditor 自行处理）：" + string.Join(", ", events);

        var confirm = MessageBox.Show(
            "确定删除任务？\n\n" + name + "\n" + id +
            "\n文件：" + _quest.FilePath +
            extra +
            "\n\n此操作不可撤销。",
            "删除任务",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            PackageStore.DeleteDefinition(_package, _quest);
            _quest = null;
            LoadRoot(_package.Root);
            StatusText.Text = "已删除任务 " + id;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "删除失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    bool TryApplyEditorToQuest(out string err)
    {
        err = "";
        if (_package == null || _quest == null)
        {
            err = "没有选中任务";
            return false;
        }

        _quest.Raw["id"] = IdBox.Text?.Trim() ?? _quest.Id;
        _quest.Raw["name"] = NameBox.Text ?? "";
        _quest.Raw["description"] = DescBox.Text ?? "";
        _quest.Raw["abandonable"] = AbandonableBox.IsChecked == true;

        var mode = CurrentOfferMode();
        try
        {
            switch (mode)
            {
                case QuestOfferMode.AfterQuest:
                    var prev = PreviousQuestBox.Text?.Trim() ?? "";
                    if (string.IsNullOrEmpty(prev))
                    {
                        err = "请选择前置任务";
                        return false;
                    }

                    QuestOfferService.ApplyAfterQuest(_quest.Raw, prev);
                    break;
                case QuestOfferMode.AtLocation:
                    var loc = OfferLocationBox.Text?.Trim() ?? "";
                    if (string.IsNullOrEmpty(loc))
                    {
                        err = "请填写发放地点";
                        return false;
                    }

                    QuestOfferService.ApplyAtLocation(_package, _quest.Raw, loc);
                    break;
                case QuestOfferMode.NpcDialogue:
                    _quest.Raw["autoOffer"] = false;
                    _quest.Raw["offerConditions"] = new JsonArray();
                    break;
                case QuestOfferMode.Auto:
                    QuestOfferService.ApplyAutoOffer(_quest.Raw, OfferEditor.ToJsonArray());
                    break;
                default:
                    _quest.Raw["autoOffer"] = AutoOfferBox.IsChecked == true;
                    _quest.Raw["offerConditions"] = OfferEditor.ToJsonArray();
                    break;
            }
        }
        catch (Exception ex)
        {
            err = ex.Message;
            return false;
        }

        _quest.Raw["completeConditions"] = CompleteEditor.ToJsonArray();
        _quest.Raw["rewards"] = RewardsEditor.ToJsonArray();
        _quest.Raw["failConditions"] = FailCondEditor.ToJsonArray();
        _quest.Raw["failResults"] = FailResEditor.ToJsonArray();
        return true;
    }
}
