using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace ContentAuthoring.Shared;

public partial class JsonArrayEditor : UserControl
{
    readonly List<RowState> _rows = new();
    bool _rebuildingRows;

    ContentPackage? _package;
    JsonArrayEditorMode _mode = JsonArrayEditorMode.Condition;

    public JsonArrayEditor()
    {
        InitializeComponent();
    }

    public void Configure(ContentPackage? package, JsonArrayEditorMode mode, string title)
    {
        _package = package;
        _mode = mode;
        TitleText.Text = title;
        RebuildRows();
    }

    public void LoadFrom(JsonNode? node)
    {
        _rows.Clear();
        if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item is JsonObject obj)
                    _rows.Add(RowState.FromJson(_mode, obj));
            }
        }

        RebuildRows();
    }

    public JsonArray ToJsonArray()
    {
        var arr = new JsonArray();
        foreach (var row in _rows)
            arr.Add(row.ToJson());
        return arr;
    }

    void RebuildRows()
    {
        _rebuildingRows = true;
        try
        {
            RowsPanel.Children.Clear();
            for (var i = 0; i < _rows.Count; i++)
            {
                var index = i;
                var row = _rows[i];
                var border = new Border
                {
                    BorderBrush = System.Windows.Media.Brushes.LightGray,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(6),
                    Margin = new Thickness(0, 0, 0, 6)
                };
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var kindCombo = new ComboBox { Margin = new Thickness(0, 0, 6, 0) };
                var kinds = _mode == JsonArrayEditorMode.Condition
                    ? ContentFieldCatalog.ConditionKinds
                    : ContentFieldCatalog.OutcomeKinds;
                foreach (var (kind, label) in kinds)
                    kindCombo.Items.Add(new ComboBoxItem { Content = label, Tag = kind });
                SelectKind(kindCombo, row.Kind);
                kindCombo.SelectionChanged += (_, _) =>
                {
                    if (_rebuildingRows) return;
                    if (kindCombo.SelectedItem is not ComboBoxItem item || item.Tag is not string kind) return;
                    if (string.Equals(row.Kind, kind, StringComparison.Ordinal)) return;
                    row.Kind = kind;
                    RebuildRows();
                };
                Grid.SetColumn(kindCombo, 0);
                grid.Children.Add(kindCombo);

                var fieldsPanel = new StackPanel { Orientation = Orientation.Vertical };
                foreach (var spec in GetFieldSpecs(row.Kind))
                {
                    var rowGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    var label = new TextBlock
                    {
                        Text = spec.Label,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 6, 0)
                    };
                    Grid.SetColumn(label, 0);
                    rowGrid.Children.Add(label);

                    var editor = CreateFieldEditor(spec, row);
                    Grid.SetColumn(editor, 1);
                    rowGrid.Children.Add(editor);
                    fieldsPanel.Children.Add(rowGrid);
                }

                Grid.SetColumn(fieldsPanel, 1);
                grid.Children.Add(fieldsPanel);

                var remove = new Button { Content = "删除", Width = 56, Margin = new Thickness(6, 0, 0, 0) };
                remove.Click += (_, _) =>
                {
                    _rows.RemoveAt(index);
                    RebuildRows();
                };
                Grid.SetColumn(remove, 2);
                grid.Children.Add(remove);

                border.Child = grid;
                RowsPanel.Children.Add(border);
            }
        }
        finally
        {
            _rebuildingRows = false;
        }
    }

    static void SelectKind(ComboBox combo, string kind)
    {
        foreach (ComboBoxItem item in combo.Items)
        {
            if (string.Equals(item.Tag as string, kind, StringComparison.Ordinal))
            {
                combo.SelectedItem = item;
                return;
            }
        }

        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    IReadOnlyList<FieldSpec> GetFieldSpecs(string kind) =>
        _mode == JsonArrayEditorMode.Condition
            ? ContentFieldCatalog.FieldsForCondition(kind)
            : ContentFieldCatalog.FieldsForOutcome(kind);

    FrameworkElement CreateFieldEditor(FieldSpec spec, RowState row)
    {
        IEnumerable<string> OptionsFor(FieldEditorKind kind) => kind switch
        {
            FieldEditorKind.Location => _package == null ? [] : PackageStore.AllLocationIds(_package),
            FieldEditorKind.Quest => _package == null ? [] : PackageStore.AllQuestIds(_package),
            FieldEditorKind.Character => _package == null ? [] : PackageStore.AllCharacterIds(_package),
            FieldEditorKind.Resource => _package == null ? [] : PackageStore.AllResourceIds(_package),
            FieldEditorKind.Site => _package == null ? [] : PackageStore.AllSiteIds(_package),
            FieldEditorKind.Manual => _package == null ? [] : PackageStore.AllManualIds(_package),
            FieldEditorKind.Realm => ContentFieldCatalog.RealmOptions,
            _ => []
        };

        if (spec.Editor == FieldEditorKind.Number)
        {
            var box = new TextBox { Text = row.Get(spec.Key) };
            box.TextChanged += (_, _) => row.Set(spec.Key, box.Text);
            return box;
        }

        if (spec.Editor != FieldEditorKind.Text)
        {
            var combo = new ComboBox { IsEditable = true };
            foreach (var opt in OptionsFor(spec.Editor))
                combo.Items.Add(opt);
            combo.Text = row.Get(spec.Key);
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is string s)
                    row.Set(spec.Key, s);
            };
            combo.LostFocus += (_, _) => row.Set(spec.Key, combo.Text);
            return combo;
        }

        var text = new TextBox { Text = row.Get(spec.Key) };
        text.TextChanged += (_, _) => row.Set(spec.Key, text.Text);
        return text;
    }

    void Add_Click(object sender, RoutedEventArgs e)
    {
        var defaultKind = _mode == JsonArrayEditorMode.Condition
            ? ContentFieldCatalog.ConditionKinds[0].Kind
            : ContentFieldCatalog.OutcomeKinds[0].Kind;
        _rows.Add(new RowState(defaultKind));
        RebuildRows();
    }

    sealed class RowState
    {
        readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public string Kind { get; set; }

        public RowState(string kind) => Kind = kind;

        public static RowState FromJson(JsonArrayEditorMode mode, JsonObject obj)
        {
            var kind = obj["kind"]?.GetValue<string>() ?? "storyFlag";
            var row = new RowState(kind);
            foreach (var prop in obj)
            {
                if (prop.Key == "kind") continue;
                row._values[prop.Key] = ReadJsonScalar(prop.Value);
            }

            if (mode == JsonArrayEditorMode.Condition &&
                string.Equals(kind, "realmAtLeast", StringComparison.OrdinalIgnoreCase) &&
                !row._values.ContainsKey("realm"))
                row._values["realm"] = obj["realm"]?.GetValue<string>() ?? "炼气";

            if (string.Equals(kind, "relationDelta", StringComparison.OrdinalIgnoreCase))
            {
                if (obj["toDefinitionIds"] is JsonArray ids && ids.Count > 0)
                {
                    var parts = new List<string>(ids.Count);
                    foreach (var node in ids)
                    {
                        if (node is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s))
                            parts.Add(s.Trim());
                    }

                    if (parts.Count > 0)
                        row._values["toDefinitionIds"] = string.Join(", ", parts);
                }
                else if (!row._values.ContainsKey("toDefinitionIds"))
                {
                    var single = obj["toDefinitionId"]?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(single))
                        row._values["toDefinitionIds"] = single.Trim();
                }
            }

            return row;
        }

        public string Get(string key) => _values.TryGetValue(key, out var v) ? v : "";

        public void Set(string key, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                _values.Remove(key);
            else
                _values[key] = value.Trim();
        }

        public JsonObject ToJson()
        {
            var obj = new JsonObject { ["kind"] = Kind };
            foreach (var kv in _values)
            {
                if (string.Equals(Kind, "relationDelta", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(kv.Key, "toDefinitionIds", StringComparison.Ordinal))
                {
                    if (string.IsNullOrWhiteSpace(kv.Value))
                        continue;
                    var arr = new JsonArray();
                    foreach (var part in kv.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        arr.Add(part);
                    if (arr.Count > 0)
                        obj["toDefinitionIds"] = arr;
                    continue;
                }

                if (kv.Key == "amount" && int.TryParse(kv.Value, out var n))
                    obj[kv.Key] = n;
                else if (kv.Key == "realm")
                    obj["realm"] = kv.Value;
                else
                    obj[kv.Key] = kv.Value;
            }

            return obj;
        }

        static string ReadJsonScalar(JsonNode? node)
        {
            if (node is JsonValue v)
            {
                if (v.TryGetValue<int>(out var n)) return n.ToString();
                if (v.TryGetValue<double>(out var d)) return d.ToString();
                if (v.TryGetValue<string>(out var s)) return s;
                if (v.TryGetValue<bool>(out var b)) return b.ToString();
            }

            return node?.ToJsonString().Trim('"') ?? "";
        }
    }
}
