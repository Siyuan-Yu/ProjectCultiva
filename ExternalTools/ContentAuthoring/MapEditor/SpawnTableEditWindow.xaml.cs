using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Windows;
using ContentAuthoring.Shared;

namespace MapEditor;

public partial class SpawnTableEditWindow : Window
{
    readonly ContentPackage _package;
    readonly ObservableCollection<EntryRow> _rows = new();
    DefRef? _def;
    string _tableId;

    public string? SavedTableId { get; private set; }

    public SpawnTableEditWindow(ContentPackage package, string? preferredTableId)
    {
        InitializeComponent();
        _package = package;
        EntryList.ItemsSource = _rows;
        CharacterBox.ItemsSource = PackageStore.AllCharacterIds(package);

        var tables = PackageStore.AllSpawnTableIds(package);
        TablePick.ItemsSource = tables;
        if (!string.IsNullOrWhiteSpace(preferredTableId) &&
            tables.Contains(preferredTableId, StringComparer.Ordinal))
            TablePick.SelectedItem = preferredTableId;
        else if (tables.Count > 0)
            TablePick.SelectedIndex = 0;
        else
            StartNewTable();
    }

    void TablePick_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (TablePick.SelectedItem is string id)
            LoadTable(id);
    }

    void LoadTable(string id)
    {
        _def = PackageStore.FindSpawnTable(_package, id);
        if (_def == null)
            return;
        _tableId = id;
        IdBox.Text = id;
        NameBox.Text = _def.Raw["name"]?.GetValue<string>() ?? "";
        _rows.Clear();
        if (_def.Raw["entries"] is JsonArray arr)
        {
            foreach (var node in arr)
            {
                if (node is not JsonObject o) continue;
                _rows.Add(new EntryRow
                {
                    DefinitionId = o["definitionId"]?.GetValue<string>() ?? "",
                    Weight = o["weight"]?.GetValue<int>() ?? 1,
                    CountMin = o["countMin"]?.GetValue<int>() ?? 1,
                    CountMax = o["countMax"]?.GetValue<int>() ?? 1
                });
            }
        }
    }

    void StartNewTable()
    {
        _def = null;
        _tableId = "base:spawn_table_new";
        IdBox.Text = _tableId;
        NameBox.Text = "新刷怪表";
        TablePick.SelectedItem = null;
        _rows.Clear();
        _rows.Add(new EntryRow
        {
            DefinitionId = PackageStore.AllCharacterIds(_package).FirstOrDefault() ?? "",
            Weight = 1,
            CountMin = 1,
            CountMax = 1
        });
    }

    void New_Click(object sender, RoutedEventArgs e) => StartNewTable();

    void AddEntry_Click(object sender, RoutedEventArgs e)
    {
        var id = CharacterBox.SelectedItem as string ??
                 PackageStore.AllCharacterIds(_package).FirstOrDefault() ?? "";
        _rows.Add(new EntryRow
        {
            DefinitionId = id,
            Weight = 1,
            CountMin = 1,
            CountMax = 1
        });
    }

    void RemoveEntry_Click(object sender, RoutedEventArgs e)
    {
        if (EntryList.SelectedItem is EntryRow row)
            _rows.Remove(row);
    }

    void ApplyRow_Click(object sender, RoutedEventArgs e)
    {
        if (EntryList.SelectedItem is not EntryRow row)
            return;
        if (int.TryParse(WeightBox.Text, out var w))
            row.Weight = Math.Max(1, w);
        if (int.TryParse(MinBox.Text, out var mn))
            row.CountMin = Math.Max(0, mn);
        if (int.TryParse(MaxBox.Text, out var mx))
            row.CountMax = Math.Max(row.CountMin, mx);
        EntryList.Items.Refresh();
    }

    void Save_Click(object sender, RoutedEventArgs e)
    {
        var id = (IdBox.Text ?? "").Trim();
        if (string.IsNullOrEmpty(id) || !id.Contains(':'))
        {
            MessageBox.Show("表 id 须形如 base:spawn_table_xxx", "刷怪表", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_rows.Count == 0)
        {
            MessageBox.Show("至少一条条目", "刷怪表", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var entries = new JsonArray();
        foreach (var row in _rows)
        {
            if (string.IsNullOrWhiteSpace(row.DefinitionId)) continue;
            entries.Add(new JsonObject
            {
                ["definitionId"] = row.DefinitionId.Trim(),
                ["weight"] = Math.Max(1, row.Weight),
                ["countMin"] = Math.Max(0, row.CountMin),
                ["countMax"] = Math.Max(row.CountMin, row.CountMax)
            });
        }

        if (entries.Count == 0)
        {
            MessageBox.Show("条目需选择角色 definitionId", "刷怪表", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var raw = new JsonObject
        {
            ["id"] = id,
            ["type"] = "spawnTable",
            ["name"] = string.IsNullOrWhiteSpace(NameBox.Text) ? id : NameBox.Text.Trim(),
            ["entries"] = entries
        };

        try
        {
            if (_def != null && string.Equals(_def.Id, id, StringComparison.Ordinal))
            {
                _def.Raw = raw;
                PackageStore.SaveDefinition(_package, _def);
            }
            else
            {
                var existing = PackageStore.FindSpawnTable(_package, id);
                if (existing != null)
                {
                    existing.Raw = raw;
                    PackageStore.SaveDefinition(_package, existing);
                }
                else
                {
                    PackageStore.AppendDefinition(_package, "spawn_tables.json", raw);
                }
            }

            SavedTableId = id;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show("保存失败：\n" + ex.Message, "刷怪表", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    public sealed class EntryRow
    {
        public string DefinitionId { get; set; } = "";
        public int Weight { get; set; } = 1;
        public int CountMin { get; set; } = 1;
        public int CountMax { get; set; } = 1;
    }
}
