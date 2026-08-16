using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Windows;
using ContentAuthoring.Shared;
using Microsoft.Win32;

namespace WorldGraphEditor;

public partial class MainWindow : Window
{
    private ContentPackage? _package;
    private DefRef? _graph;
    private readonly ObservableCollection<NodeRow> _nodes = new();
    private readonly ObservableCollection<RouteRow> _routes = new();

    public MainWindow()
    {
        InitializeComponent();
        Title = "XianXia · 大世界图编辑器（WorldGraph）";
        NodeGrid.ItemsSource = _nodes;
        RouteGrid.ItemsSource = _routes;
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
        GraphCombo.ItemsSource = _package.OfType("worldGraph").Select(d => d.Id).ToList();
        if (GraphCombo.Items.Count > 0) GraphCombo.SelectedIndex = 0;
        StatusText.Text = $"已加载 WorldGraph {GraphCombo.Items.Count} 个";
    }

    private void OpenPackage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "选择 Content/BaseGame 包目录" };
        if (dlg.ShowDialog() == true) LoadRoot(dlg.FolderName);
    }

    private void GraphCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_package == null || GraphCombo.SelectedItem is not string id) return;
        _graph = _package.Find(id);
        _nodes.Clear();
        _routes.Clear();
        if (_graph == null) return;
        NameBox.Text = JsonEdit.GetString(_graph.Raw, "name");
        StartBox.Text = JsonEdit.GetString(_graph.Raw, "startNodeId");
        if (_graph.Raw["nodes"] is JsonArray nodes)
        {
            foreach (var node in nodes.OfType<JsonObject>())
                _nodes.Add(NodeRow.FromJson(node));
        }

        if (_graph.Raw["routes"] is JsonArray routes)
        {
            foreach (var route in routes.OfType<JsonObject>())
                _routes.Add(RouteRow.FromJson(route));
        }
    }

    private void NewGraph_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null)
        {
            MessageBox.Show("请先打开包");
            return;
        }

        var id = "base:graph_new";
        ContentPathRules.EnsureTypeDir(_package.Root, "worldGraph");
        var path = Path.Combine(ContentPathRules.TypeDataDir(_package.Root, "worldGraph"), "graph_new.json");
        var root = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["definitions"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = id,
                    ["type"] = "worldGraph",
                    ["name"] = "新世界图",
                    ["startNodeId"] = "",
                    ["nodes"] = new JsonArray(),
                    ["routes"] = new JsonArray()
                }
            }
        };
        File.WriteAllText(
            path,
            root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine,
            new System.Text.UTF8Encoding(false));
        LoadRoot(_package.Root);
        GraphCombo.SelectedItem = id;
        StatusText.Text = "已新建 " + path;
    }

    private void AddNode_Click(object sender, RoutedEventArgs e)
    {
        _nodes.Add(new NodeRow
        {
            Id = "base:node_new",
            Name = "新节点",
            Kind = "Village",
            WorldX = 0,
            WorldY = 0
        });
    }

    private void DeleteNode_Click(object sender, RoutedEventArgs e)
    {
        if (NodeGrid.SelectedItem is NodeRow row) _nodes.Remove(row);
    }

    private void AddRoute_Click(object sender, RoutedEventArgs e)
    {
        _routes.Add(new RouteRow
        {
            Id = "base:route_new",
            FromNodeId = _nodes.FirstOrDefault()?.Id ?? "",
            ToNodeId = "",
            Kind = "Trail",
            TravelCost = 4,
            State = "Open"
        });
    }

    private void DeleteRoute_Click(object sender, RoutedEventArgs e)
    {
        if (RouteGrid.SelectedItem is RouteRow row) _routes.Remove(row);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null || _graph == null)
        {
            MessageBox.Show("没有选中 worldGraph");
            return;
        }

        _graph.Raw["name"] = NameBox.Text ?? "";
        JsonEdit.SetString(_graph.Raw, "startNodeId", StartBox.Text);
        var nodes = new JsonArray();
        foreach (var row in _nodes) nodes.Add(row.ToJson());
        _graph.Raw["nodes"] = nodes;
        var routes = new JsonArray();
        foreach (var row in _routes) routes.Add(row.ToJson());
        _graph.Raw["routes"] = routes;
        try
        {
            PackageStore.SaveDefinition(_package, _graph);
            StatusText.Text = "已保存: " + Path.GetFileName(_graph.FilePath);
            LoadRoot(_package.Root);
            GraphCombo.SelectedItem = _graph.Id;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

public sealed class NodeRow : INotifyPropertyChanged
{
    private string _id = "";
    private string _name = "";
    private string _kind = "Village";
    private string _localMapId = "";
    private double _x;
    private double _y;
    private string _owner = "";
    private string _state = "";
    private string _tags = "";

    public string Id { get => _id; set => Set(ref _id, value); }
    public string Name { get => _name; set => Set(ref _name, value); }
    public string Kind { get => _kind; set => Set(ref _kind, value); }
    public string LocalMapId { get => _localMapId; set => Set(ref _localMapId, value); }
    public double WorldX { get => _x; set => Set(ref _x, value); }
    public double WorldY { get => _y; set => Set(ref _y, value); }
    public string OwnerId { get => _owner; set => Set(ref _owner, value); }
    public string State { get => _state; set => Set(ref _state, value); }
    public string Tags { get => _tags; set => Set(ref _tags, value); }

    public static NodeRow FromJson(JsonObject o) => new()
    {
        Id = JsonEdit.GetString(o, "id"),
        Name = JsonEdit.GetString(o, "name"),
        Kind = JsonEdit.GetString(o, "kind"),
        LocalMapId = JsonEdit.GetString(o, "localMapId"),
        WorldX = o["worldX"]?.GetValue<double>() ?? 0,
        WorldY = o["worldY"]?.GetValue<double>() ?? 0,
        OwnerId = JsonEdit.GetString(o, "ownerId"),
        State = JsonEdit.GetString(o, "state"),
        Tags = JoinArr(o["tags"] as JsonArray)
    };

    public JsonObject ToJson()
    {
        var o = new JsonObject
        {
            ["id"] = Id,
            ["name"] = Name,
            ["kind"] = Kind,
            ["worldX"] = WorldX,
            ["worldY"] = WorldY
        };
        if (!string.IsNullOrWhiteSpace(LocalMapId)) o["localMapId"] = LocalMapId;
        if (!string.IsNullOrWhiteSpace(OwnerId)) o["ownerId"] = OwnerId;
        if (!string.IsNullOrWhiteSpace(State)) o["state"] = State;
        var tags = SplitCsv(Tags);
        if (tags.Count > 0) o["tags"] = tags;
        return o;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    static string JoinArr(JsonArray? arr)
    {
        if (arr == null) return "";
        return string.Join(",", arr.Select(x => x?.GetValue<string>() ?? "").Where(s => s.Length > 0));
    }

    static JsonArray SplitCsv(string text)
    {
        var arr = new JsonArray();
        foreach (var p in (text ?? "").Split(new[] { ',', '，', ';' }, StringSplitOptions.RemoveEmptyEntries))
            arr.Add(p.Trim());
        return arr;
    }
}

public sealed class RouteRow : INotifyPropertyChanged
{
    private string _id = "";
    private string _from = "";
    private string _to = "";
    private string _kind = "Trail";
    private int _cost = 4;
    private double _danger;
    private string _state = "Open";
    private bool _directed;
    private string _req = "";
    private string _pool = "";

    public string Id { get => _id; set => Set(ref _id, value); }
    public string FromNodeId { get => _from; set => Set(ref _from, value); }
    public string ToNodeId { get => _to; set => Set(ref _to, value); }
    public string Kind { get => _kind; set => Set(ref _kind, value); }
    public int TravelCost { get => _cost; set => Set(ref _cost, value); }
    public double Danger { get => _danger; set => Set(ref _danger, value); }
    public string State { get => _state; set => Set(ref _state, value); }
    public bool Directed { get => _directed; set => Set(ref _directed, value); }
    public string TraversalRequirements { get => _req; set => Set(ref _req, value); }
    public string EncounterPoolId { get => _pool; set => Set(ref _pool, value); }

    public static RouteRow FromJson(JsonObject o)
    {
        var req = "";
        if (o["traversalRequirements"] is JsonArray arr)
        {
            req = string.Join(",", arr.OfType<JsonObject>().Select(c =>
                JsonEdit.GetString(c, "kind") + ":" + JsonEdit.GetString(c, "id")));
        }

        return new RouteRow
        {
            Id = JsonEdit.GetString(o, "id"),
            FromNodeId = JsonEdit.GetString(o, "fromNodeId"),
            ToNodeId = JsonEdit.GetString(o, "toNodeId"),
            Kind = JsonEdit.GetString(o, "kind"),
            TravelCost = o["travelCost"]?.GetValue<int>() ?? 0,
            Danger = o["danger"]?.GetValue<double>() ?? 0,
            State = JsonEdit.GetString(o, "state"),
            Directed = o["directed"]?.GetValue<bool>() ?? false,
            TraversalRequirements = req,
            EncounterPoolId = JsonEdit.GetString(o, "encounterPoolId")
        };
    }

    public JsonObject ToJson()
    {
        var o = new JsonObject
        {
            ["id"] = Id,
            ["fromNodeId"] = FromNodeId,
            ["toNodeId"] = ToNodeId,
            ["kind"] = Kind,
            ["travelCost"] = TravelCost,
            ["danger"] = Danger,
            ["state"] = string.IsNullOrWhiteSpace(State) ? "Open" : State,
            ["directed"] = Directed
        };
        if (!string.IsNullOrWhiteSpace(EncounterPoolId)) o["encounterPoolId"] = EncounterPoolId;
        var reqs = new JsonArray();
        foreach (var part in (TraversalRequirements ?? "").Split(new[] { ',', '，', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var t = part.Trim();
            var idx = t.IndexOf(':');
            if (idx <= 0) continue;
            reqs.Add(new JsonObject
            {
                ["kind"] = t[..idx].Trim(),
                ["id"] = t[(idx + 1)..].Trim()
            });
        }

        if (reqs.Count > 0) o["traversalRequirements"] = reqs;
        return o;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
