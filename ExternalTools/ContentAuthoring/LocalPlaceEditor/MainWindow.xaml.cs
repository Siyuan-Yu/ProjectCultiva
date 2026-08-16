using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Windows;
using ContentAuthoring.Shared;
using Microsoft.Win32;

namespace LocalPlaceEditor;

public partial class MainWindow : Window
{
    private ContentPackage? _package;
    private DefRef? _set;
    private readonly ObservableCollection<PlaceRow> _rows = new();

    public MainWindow()
    {
        InitializeComponent();
        Title = "XianXia · 场景地点登记（LocalPlace）";
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
        MapCombo.ItemsSource = _package.OfType("mapLayout").Select(d => d.Id).OrderBy(x => x).ToList();
        SetCombo.ItemsSource = _package.OfType("localPlaceSet").Select(d => d.Id).ToList();
        if (SetCombo.Items.Count > 0) SetCombo.SelectedIndex = 0;
        StatusText.Text = $"已加载地点表 {SetCombo.Items.Count} 个";
    }

    private void OpenPackage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "选择 Content/BaseGame 包目录" };
        if (dlg.ShowDialog() == true) LoadRoot(dlg.FolderName);
    }

    private void SetCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_package == null || SetCombo.SelectedItem is not string id) return;
        _set = _package.Find(id);
        _rows.Clear();
        if (_set == null) return;
        NameBox.Text = JsonEdit.GetString(_set.Raw, "name");
        MapCombo.Text = JsonEdit.GetString(_set.Raw, "mapLayoutId");
        StartBox.Text = JsonEdit.GetString(_set.Raw, "startLocationId");
        if (_set.Raw["locations"] is not JsonArray locs) return;
        foreach (var node in locs.OfType<JsonObject>())
            _rows.Add(PlaceRow.FromJson(node));
    }

    private void NewSet_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null)
        {
            MessageBox.Show("请先打开包");
            return;
        }

        var id = "base:places_new";
        ContentPathRules.EnsureTypeDir(_package.Root, "localPlaceSet");
        var path = Path.Combine(
            ContentPathRules.TypeDataDir(_package.Root, "localPlaceSet"),
            "places_new.json");
        if (File.Exists(path))
        {
            MessageBox.Show("已存在 places_new.json，请先改名或另存。");
            return;
        }

        var root = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["definitions"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = id,
                    ["type"] = "localPlaceSet",
                    ["name"] = "新场景地点表",
                    ["mapLayoutId"] = "",
                    ["startLocationId"] = "",
                    ["locations"] = new JsonArray()
                }
            }
        };
        File.WriteAllText(
            path,
            root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) +
            Environment.NewLine,
            new System.Text.UTF8Encoding(false));
        LoadRoot(_package.Root);
        SetCombo.SelectedItem = id;
        StatusText.Text = "已新建 " + path;
    }

    private void AddLoc_Click(object sender, RoutedEventArgs e)
    {
        _rows.Add(new PlaceRow
        {
            Id = "base:loc_new",
            Name = "新地点",
            Kind = "野外",
            Tags = "",
            AdjacentIds = "",
            AllowedActivities = "",
            PresentationX = 0,
            PresentationZ = 0,
            SurveySenseRequired = 0
        });
    }

    private void AddCave_Click(object sender, RoutedEventArgs e)
    {
        _rows.Add(new PlaceRow
        {
            Id = "base:loc_cave_new",
            Name = "隐藏洞府",
            Kind = "机缘",
            Tags = "cave,opportunity,entrance",
            AllowedActivities = "探索,修炼",
            AdjacentIds = "",
            OpportunitySiteId = "base:site_new",
            EnterLocalMapId = "base:map_cave_new",
            EnterSpawnLocationId = "base:loc_cave_chamber",
            SurveySenseRequired = 0,
            PresentationX = 0,
            PresentationZ = 0
        });
    }

    private void DeleteLoc_Click(object sender, RoutedEventArgs e)
    {
        if (LocGrid.SelectedItem is PlaceRow row) _rows.Remove(row);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null || _set == null)
        {
            MessageBox.Show("没有选中地点表");
            return;
        }

        _set.Raw["name"] = NameBox.Text ?? "";
        JsonEdit.SetString(_set.Raw, "mapLayoutId", MapCombo.Text);
        JsonEdit.SetString(_set.Raw, "startLocationId", StartBox.Text);
        var arr = new JsonArray();
        foreach (var row in _rows) arr.Add(row.ToJson());
        _set.Raw["locations"] = arr;
        try
        {
            PackageStore.SaveDefinition(_package, _set);
            StatusText.Text = "已保存: " + Path.GetFileName(_set.FilePath);
            var keep = _set.Id;
            LoadRoot(_package.Root);
            SetCombo.SelectedItem = keep;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

public sealed class PlaceRow : INotifyPropertyChanged
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
    private string _localMapId = "";
    private string _enterMap = "";
    private string _enterSpawn = "";
    private int _senseRequired;

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
    public string LocalMapId { get => _localMapId; set { _localMapId = value; OnPropertyChanged(); } }
    public string EnterLocalMapId { get => _enterMap; set { _enterMap = value; OnPropertyChanged(); } }
    public string EnterSpawnLocationId { get => _enterSpawn; set { _enterSpawn = value; OnPropertyChanged(); } }
    public int SurveySenseRequired { get => _senseRequired; set { _senseRequired = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public static PlaceRow FromJson(JsonObject loc) => new()
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
        QuestOfferIds = JsonEdit.JoinStringArray(loc["questOfferIds"]),
        LocalMapId = JsonEdit.GetString(loc, "localMapId"),
        EnterLocalMapId = JsonEdit.GetString(loc, "enterLocalMapId"),
        EnterSpawnLocationId = JsonEdit.GetString(loc, "enterSpawnLocationId"),
        SurveySenseRequired = JsonEdit.GetInt(loc, "surveySenseRequired")
    };

    public JsonObject ToJson()
    {
        var o = new JsonObject
        {
            ["id"] = Id,
            ["name"] = Name,
            ["kind"] = UiLabels.ToKey(UiLabels.LocationKinds, Kind, "Wild"),
            ["tags"] = JsonEdit.ParseStringList(Tags),
            ["adjacentIds"] = JsonEdit.ParseStringList(AdjacentIds),
            ["presentationX"] = PresentationX,
            ["presentationZ"] = PresentationZ
        };
        var acts = UiLabels.ActivitiesCsvToKeys(AllowedActivities);
        if (!string.IsNullOrWhiteSpace(acts))
            o["allowedActivities"] = JsonEdit.ParseStringList(acts);
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
        if (!string.IsNullOrWhiteSpace(LocalMapId))
            o["localMapId"] = LocalMapId;
        if (!string.IsNullOrWhiteSpace(EnterLocalMapId))
            o["enterLocalMapId"] = EnterLocalMapId;
        if (!string.IsNullOrWhiteSpace(EnterSpawnLocationId))
            o["enterSpawnLocationId"] = EnterSpawnLocationId;
        if (SurveySenseRequired > 0)
            o["surveySenseRequired"] = SurveySenseRequired;
        return o;
    }
}
