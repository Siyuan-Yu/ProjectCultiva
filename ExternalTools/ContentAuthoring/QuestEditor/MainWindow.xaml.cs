using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using ContentAuthoring.Shared;
using Microsoft.Win32;

namespace QuestEditor;

public partial class MainWindow : Window
{
    private ContentPackage? _package;
    private DefRef? _quest;

    public MainWindow()
    {
        InitializeComponent();
        Title = "XianXia · 任务编辑器";
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
        if (QuestList.Items.Count > 0) QuestList.SelectedIndex = 0;
        StatusText.Text = $"任务 {_package.OfType("quest").Count()} 条";
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
        IdBox.Text = JsonEdit.GetString(_quest.Raw, "id");
        NameBox.Text = JsonEdit.GetString(_quest.Raw, "name");
        DescBox.Text = JsonEdit.GetString(_quest.Raw, "description");
        AutoOfferBox.IsChecked = JsonEdit.GetBool(_quest.Raw, "autoOffer", true);
        OfferBox.Text = JsonEdit.ConditionsToEditable(_quest.Raw["offerConditions"]);
        CompleteBox.Text = JsonEdit.ConditionsToEditable(_quest.Raw["completeConditions"]);
        RewardsBox.Text = JsonEdit.ConditionsToEditable(_quest.Raw["rewards"]);
        FailCondBox.Text = JsonEdit.ConditionsToEditable(_quest.Raw["failConditions"]);
        FailResBox.Text = JsonEdit.ConditionsToEditable(_quest.Raw["failResults"]);
    }

    private void NewQuest_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null) return;
        var id = $"base:quest_new_{DateTime.Now:yyyyMMddHHmmss}";
        var raw = new JsonObject
        {
            ["id"] = id,
            ["type"] = "quest",
            ["name"] = "新任务",
            ["description"] = "",
            ["autoOffer"] = true,
            ["offerConditions"] = new JsonArray(),
            ["completeConditions"] = new JsonArray(),
            ["failConditions"] = new JsonArray(),
            ["rewards"] = new JsonArray(),
            ["failResults"] = new JsonArray()
        };
        _quest = PackageStore.AppendDefinition(_package, "quests.json", raw);
        LoadRoot(_package.Root);
        QuestList.SelectedItem = id;
        StatusText.Text = "已新建并写入: " + System.IO.Path.GetFileName(_quest.FilePath);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null || _quest == null)
        {
            MessageBox.Show("没有选中任务");
            return;
        }

        if (!JsonEdit.TryParseJsonArray(OfferBox.Text, out var offer, out var err) ||
            !JsonEdit.TryParseJsonArray(CompleteBox.Text, out var complete, out err) ||
            !JsonEdit.TryParseJsonArray(RewardsBox.Text, out var rewards, out err) ||
            !JsonEdit.TryParseJsonArray(FailCondBox.Text, out var failC, out err) ||
            !JsonEdit.TryParseJsonArray(FailResBox.Text, out var failR, out err))
        {
            MessageBox.Show("JSON 数组无效: " + err, "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _quest.Raw["id"] = IdBox.Text?.Trim() ?? _quest.Id;
        _quest.Raw["name"] = NameBox.Text ?? "";
        _quest.Raw["description"] = DescBox.Text ?? "";
        _quest.Raw["autoOffer"] = AutoOfferBox.IsChecked == true;
        _quest.Raw["offerConditions"] = offer;
        _quest.Raw["completeConditions"] = complete;
        _quest.Raw["rewards"] = rewards;
        _quest.Raw["failConditions"] = failC;
        _quest.Raw["failResults"] = failR;

        try
        {
            PackageStore.SaveDefinition(_package, _quest);
            var keep = JsonEdit.GetString(_quest.Raw, "id");
            LoadRoot(_package.Root);
            QuestList.SelectedItem = keep;
            StatusText.Text = "已保存";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
