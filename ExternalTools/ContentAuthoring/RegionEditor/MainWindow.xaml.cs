using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Windows;
using ContentAuthoring.Shared;
using Microsoft.Win32;

namespace RegionEditor;

public partial class MainWindow : Window
{
    private ContentPackage? _package;
    private DefRef? _region;
    private readonly ObservableCollection<LocationRow> _rows = new();

    public MainWindow()
    {
        InitializeComponent();
        Title = "XianXia · 区域／地点编辑器";
        KindColumn.ItemsSource = UiLabels.Labels(UiLabels.LocationKinds);
        LocGrid.ItemsSource = _rows;
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
        RegionCombo.ItemsSource = _package.OfType("worldRegion").Select(d => d.Id).ToList();
        if (RegionCombo.Items.Count > 0) RegionCombo.SelectedIndex = 0;
        StatusText.Text = $"已加载区域 {RegionCombo.Items.Count} 个";
    }

    private void OpenPackage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "选择 Content/BaseGame 包目录" };
        if (dlg.ShowDialog() == true) LoadRoot(dlg.FolderName);
    }

    private void RegionCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_package == null || RegionCombo.SelectedItem is not string id) return;
        _region = _package.Find(id);
        _rows.Clear();
        if (_region?.Raw["locations"] is not JsonArray locs) return;
        foreach (var node in locs.OfType<JsonObject>())
            _rows.Add(LocationRow.FromJson(node));
        NameBox.Text = JsonEdit.GetString(_region!.Raw, "name");
        StartBox.Text = JsonEdit.GetString(_region.Raw, "startLocationId");
    }

    private void AddLoc_Click(object sender, RoutedEventArgs e)
    {
        _rows.Add(new LocationRow
        {
            Id = "base:loc_new",
            Name = "新地点",
            Kind = "野外",
            AdjacentIds = "",
            Tags = "",
            AllowedActivities = "",
            PresentationX = 0,
            PresentationZ = 0
        });
    }

    private void DeleteLoc_Click(object sender, RoutedEventArgs e)
    {
        if (LocGrid.SelectedItem is LocationRow row) _rows.Remove(row);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null || _region == null)
        {
            MessageBox.Show("没有选中区域");
            return;
        }

        _region.Raw["name"] = NameBox.Text ?? "";
        JsonEdit.SetString(_region.Raw, "startLocationId", StartBox.Text);
        var arr = new JsonArray();
        foreach (var row in _rows) arr.Add(row.ToJson());
        _region.Raw["locations"] = arr;
        try
        {
            PackageStore.SaveDefinition(_package, _region);
            StatusText.Text = "已保存: " + System.IO.Path.GetFileName(_region.FilePath);
            LoadRoot(_package.Root);
            RegionCombo.SelectedItem = _region.Id;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

public sealed class LocationRow : INotifyPropertyChanged
{
    private string _id = "";
    private string _name = "";
    private string _kind = "野外";
    private string _tags = "";
    private string _allowed = "";
    private string _adjacent = "";
    private double _x;
    private double _z;
    private string _resourceId = "";
    private int _resourceAmount;
    private string _npc = "";
    private string _site = "";
    private string _quests = "";

    public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    public string Kind { get => _kind; set { _kind = value; OnPropertyChanged(); } }
    public string Tags { get => _tags; set { _tags = value; OnPropertyChanged(); } }
    public string AllowedActivities { get => _allowed; set { _allowed = value; OnPropertyChanged(); } }
    public string AdjacentIds { get => _adjacent; set { _adjacent = value; OnPropertyChanged(); } }
    public double PresentationX { get => _x; set { _x = value; OnPropertyChanged(); } }
    public double PresentationZ { get => _z; set { _z = value; OnPropertyChanged(); } }
    public string ResourceOnExploreId { get => _resourceId; set { _resourceId = value; OnPropertyChanged(); } }
    public int ResourceOnExploreAmount { get => _resourceAmount; set { _resourceAmount = value; OnPropertyChanged(); } }
    public string ResidentNpcDefinitionId { get => _npc; set { _npc = value; OnPropertyChanged(); } }
    public string OpportunitySiteId { get => _site; set { _site = value; OnPropertyChanged(); } }
    public string QuestOfferIds { get => _quests; set { _quests = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public static LocationRow FromJson(JsonObject loc) => new()
    {
        Id = JsonEdit.GetString(loc, "id"),
        Name = JsonEdit.GetString(loc, "name"),
        Kind = UiLabels.ToLabel(UiLabels.LocationKinds, JsonEdit.GetString(loc, "kind", "Wild"), "野外"),
        Tags = JsonEdit.JoinStringArray(loc["tags"]),
        AllowedActivities = UiLabels.ActivitiesCsvToDisplay(JsonEdit.JoinStringArray(loc["allowedActivities"])),
        AdjacentIds = JsonEdit.JoinStringArray(loc["adjacentIds"]),
        PresentationX = JsonEdit.GetDouble(loc, "presentationX"),
        PresentationZ = JsonEdit.GetDouble(loc, "presentationZ"),
        ResourceOnExploreId = JsonEdit.GetString(loc, "resourceOnExploreId"),
        ResourceOnExploreAmount = JsonEdit.GetInt(loc, "resourceOnExploreAmount"),
        ResidentNpcDefinitionId = JsonEdit.GetString(loc, "residentNpcDefinitionId"),
        OpportunitySiteId = JsonEdit.GetString(loc, "opportunitySiteId"),
        QuestOfferIds = JsonEdit.JoinStringArray(loc["questOfferIds"])
    };

    public JsonObject ToJson()
    {
        var o = new JsonObject
        {
            ["id"] = Id,
            ["name"] = Name,
            ["kind"] = UiLabels.ToKey(UiLabels.LocationKinds, Kind, "Wild"),
            ["tags"] = JsonEdit.ParseStringList(Tags),
            ["allowedActivities"] = JsonEdit.ParseStringList(UiLabels.ActivitiesCsvToKeys(AllowedActivities)),
            ["adjacentIds"] = JsonEdit.ParseStringList(AdjacentIds),
            ["presentationX"] = PresentationX,
            ["presentationZ"] = PresentationZ
        };
        if (!string.IsNullOrWhiteSpace(ResourceOnExploreId))
        {
            o["resourceOnExploreId"] = ResourceOnExploreId;
            o["resourceOnExploreAmount"] = ResourceOnExploreAmount;
        }
        if (!string.IsNullOrWhiteSpace(ResidentNpcDefinitionId))
            o["residentNpcDefinitionId"] = ResidentNpcDefinitionId;
        if (!string.IsNullOrWhiteSpace(OpportunitySiteId))
            o["opportunitySiteId"] = OpportunitySiteId;
        if (!string.IsNullOrWhiteSpace(QuestOfferIds))
            o["questOfferIds"] = JsonEdit.ParseStringList(QuestOfferIds);
        return o;
    }
}
