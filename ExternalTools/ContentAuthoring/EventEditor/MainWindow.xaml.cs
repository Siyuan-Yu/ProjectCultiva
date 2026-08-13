using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using ContentAuthoring.Shared;
using Microsoft.Win32;

namespace EventEditor;

public partial class MainWindow : Window
{
    private ContentPackage? _package;
    private DefRef? _event;

    public MainWindow()
    {
        InitializeComponent();
        Title = "XianXia · 事件编辑器";
        TriggerBox.ItemsSource = SchemaFields.EventTriggers;
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
        EventList.ItemsSource = _package.OfType("contentEvent").Select(d => d.Id).ToList();
        LocationBox.ItemsSource = new[] { "" }.Concat(PackageStore.AllLocationIds(_package)).ToList();
        CondEditor.Configure(_package, JsonArrayEditorMode.Condition, "触发条件");
        if (EventList.Items.Count > 0) EventList.SelectedIndex = 0;
        StatusText.Text = $"事件 {_package.OfType("contentEvent").Count()} 条";
    }

    private void OpenPackage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "选择 Content/BaseGame 包目录" };
        if (dlg.ShowDialog() == true) LoadRoot(dlg.FolderName);
    }

    private void EventList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_package == null || EventList.SelectedItem is not string id) return;
        _event = _package.Find(id);
        if (_event == null) return;
        IdBox.Text = JsonEdit.GetString(_event.Raw, "id");
        NameBox.Text = JsonEdit.GetString(_event.Raw, "name");
        BodyBox.Text = JsonEdit.GetString(_event.Raw, "body");
        TriggerBox.SelectedItem = JsonEdit.GetString(_event.Raw, "trigger", "manual");
        LocationBox.SelectedItem = JsonEdit.GetString(_event.Raw, "locationId");
        QuestIdBox.Text = JsonEdit.GetString(_event.Raw, "questId");
        OnceBox.IsChecked = _event.Raw["once"] is null || JsonEdit.GetBool(_event.Raw, "once", true);
        CondEditor.LoadFrom(_event.Raw["conditions"]);
        ChoicesBox.Text = JsonEdit.ConditionsToEditable(_event.Raw["choices"]);
    }

    private void NewEvent_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null) return;
        var id = $"base:event_new_{DateTime.Now:yyyyMMddHHmmss}";
        var raw = new JsonObject
        {
            ["id"] = id,
            ["type"] = "contentEvent",
            ["name"] = "新事件",
            ["body"] = "",
            ["trigger"] = "manual",
            ["once"] = true,
            ["conditions"] = new JsonArray(),
            ["choices"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "choice_a",
                    ["text"] = "选项 A",
                    ["conditions"] = new JsonArray(),
                    ["outcomes"] = new JsonArray()
                }
            }
        };
        _event = PackageStore.AppendDefinition(_package, "events.json", raw);
        LoadRoot(_package.Root);
        EventList.SelectedItem = id;
        StatusText.Text = "已新建并写入: " + System.IO.Path.GetFileName(_event.FilePath);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null || _event == null)
        {
            MessageBox.Show("没有选中事件");
            return;
        }

        if (!JsonEdit.TryParseJsonArray(ChoicesBox.Text, out var choices, out var err))
        {
            MessageBox.Show("choices JSON 数组无效: " + err, "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _event.Raw["id"] = IdBox.Text?.Trim() ?? _event.Id;
        _event.Raw["name"] = NameBox.Text ?? "";
        _event.Raw["body"] = BodyBox.Text ?? "";
        _event.Raw["trigger"] = TriggerBox.SelectedItem as string ?? "manual";
        JsonEdit.SetString(_event.Raw, "locationId", LocationBox.SelectedItem as string ?? LocationBox.Text);
        JsonEdit.SetString(_event.Raw, "questId", QuestIdBox.Text);
        _event.Raw["once"] = OnceBox.IsChecked == true;
        _event.Raw["conditions"] = CondEditor.ToJsonArray();
        _event.Raw["choices"] = choices;

        try
        {
            PackageStore.SaveDefinition(_package, _event);
            var keep = JsonEdit.GetString(_event.Raw, "id");
            LoadRoot(_package.Root);
            EventList.SelectedItem = keep;
            StatusText.Text = "已保存";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
