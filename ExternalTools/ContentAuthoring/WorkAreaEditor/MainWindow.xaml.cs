using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using ContentAuthoring.Shared;
using Microsoft.Win32;

namespace WorkAreaEditor;

public partial class MainWindow : Window
{
    ContentPackage? _package;
    DefRef? _workArea;
    readonly List<CheckBox> _waActivityChecks = new();

    public MainWindow()
    {
        InitializeComponent();
        BuildWaActivityChecks();
        TryLoadDefault();
    }

    void BuildWaActivityChecks()
    {
        WaActivitiesPanel.Items.Clear();
        _waActivityChecks.Clear();
        foreach (var o in UiLabels.ScheduleActivities)
        {
            var cb = new CheckBox
            {
                Content = o.Label,
                Tag = o.Key,
                Margin = new Thickness(0, 0, 12, 6)
            };
            _waActivityChecks.Add(cb);
            WaActivitiesPanel.Items.Add(cb);
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
        RefreshLists(keepSelection: false);
        WaLocationBox.ItemsSource = PackageStore.AllLocationIds(_package);
        StatusText.Text = $"工区 {_package.OfType("workArea").Count()}";
    }

    void RefreshLists(bool keepSelection)
    {
        if (_package == null) return;
        var waSel = keepSelection ? WorkAreaList.SelectedItem as string : null;
        WorkAreaList.ItemsSource = _package.OfType("workArea").Select(d => d.Id).OrderBy(x => x).ToList();
        if (waSel != null) WorkAreaList.SelectedItem = waSel;
        else if (WorkAreaList.Items.Count > 0) WorkAreaList.SelectedIndex = 0;
    }

    void OpenPackage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "选择 Content/BaseGame 包目录" };
        if (dlg.ShowDialog() == true) LoadRoot(dlg.FolderName);
    }

    void WorkAreaList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_package == null || WorkAreaList.SelectedItem is not string id) return;
        _workArea = _package.Find(id);
        if (_workArea == null) return;
        WaIdBox.Text = JsonEdit.GetString(_workArea.Raw, "id");
        WaNameBox.Text = JsonEdit.GetString(_workArea.Raw, "name");
        WaLocationBox.Text = JsonEdit.GetString(_workArea.Raw, "locationId");
        WaOffsetXBox.Text = JsonEdit.GetDouble(_workArea.Raw, "offsetX", 0).ToString(CultureInfo.InvariantCulture);
        WaOffsetZBox.Text = JsonEdit.GetDouble(_workArea.Raw, "offsetZ", 0).ToString(CultureInfo.InvariantCulture);
        if (_workArea.Raw["tags"] is JsonArray tags)
            WaTagsBox.Text = string.Join(", ", tags.OfType<JsonValue>().Select(v => v.GetValue<string>()));
        else
            WaTagsBox.Text = "";

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (_workArea.Raw["allowedActivities"] is JsonArray acts)
        {
            foreach (var a in acts)
                if (a is JsonValue v) allowed.Add(v.GetValue<string>());
        }

        foreach (var cb in _waActivityChecks)
            cb.IsChecked = allowed.Contains(cb.Tag as string ?? "");
    }

    void NewWorkArea_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null) return;
        var id = $"base:workarea_new_{DateTime.Now:yyyyMMddHHmmss}";
        var loc = PackageStore.AllLocationIds(_package).FirstOrDefault() ?? "base:loc_ref_road_hub";
        var raw = new JsonObject
        {
            ["id"] = id,
            ["type"] = "workArea",
            ["name"] = "新工区",
            ["locationId"] = loc,
            ["tags"] = new JsonArray(),
            ["allowedActivities"] = new JsonArray { "Labor" },
            ["offsetX"] = 0,
            ["offsetZ"] = 0
        };
        _workArea = PackageStore.AppendDefinition(_package, "WorkAreas/work_areas.json", raw);
        LoadRoot(_package.Root);
        WorkAreaList.SelectedItem = id;
        StatusText.Text = "已新建工区: " + id;
    }

    void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null || _workArea == null)
        {
            MessageBox.Show("没有选中工区");
            return;
        }

        if (!double.TryParse(WaOffsetXBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var ox))
            ox = 0;
        if (!double.TryParse(WaOffsetZBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var oz))
            oz = 0;

        var tags = new JsonArray();
        foreach (var t in (WaTagsBox.Text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            tags.Add(t);
        var acts = new JsonArray();
        foreach (var cb in _waActivityChecks)
            if (cb.IsChecked == true && cb.Tag is string key)
                acts.Add(key);

        var loc = WaLocationBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(loc))
        {
            MessageBox.Show("locationId 必填");
            return;
        }

        _workArea.Raw["id"] = WaIdBox.Text?.Trim() ?? _workArea.Id;
        _workArea.Raw["type"] = "workArea";
        _workArea.Raw["name"] = WaNameBox.Text ?? "";
        _workArea.Raw["locationId"] = loc;
        _workArea.Raw["offsetX"] = ox;
        _workArea.Raw["offsetZ"] = oz;
        _workArea.Raw["tags"] = tags;
        _workArea.Raw["allowedActivities"] = acts;
        PackageStore.SaveDefinition(_package, _workArea);
        var keep = JsonEdit.GetString(_workArea.Raw, "id");
        LoadRoot(_package.Root);
        WorkAreaList.SelectedItem = keep;
        StatusText.Text = "已保存工区 " + keep;
    }
}
