using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Windows;
using ContentAuthoring.Shared;
using Microsoft.Win32;

namespace ManualArtEditor;

public partial class MainWindow : Window
{
    private ContentPackage? _package;
    private DefRef? _current;
    private string _kind = "cultivation";
    private readonly ObservableCollection<TierRow> _tiers = new();
    private readonly ObservableCollection<BreakRow> _breaks = new();

    public MainWindow()
    {
        InitializeComponent();
        TierGrid.ItemsSource = _tiers;
        BreakGrid.ItemsSource = _breaks;
        TryLoadDefault();
        ApplyKindVisibility();
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
        RefreshIdList();
        StatusText.Text = $"已加载 · 功法 {_package.OfType("cultivation").Count()} · 斗技 {_package.OfType("combatArt").Count()}";
    }

    private void OpenPackage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "选择 Content/BaseGame 包目录" };
        if (dlg.ShowDialog() == true) LoadRoot(dlg.FolderName);
    }

    private void KindTabs_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (KindTabs == null) return;
        _kind = KindTabs.SelectedIndex == 1 ? "combatArt" : "cultivation";
        ApplyKindVisibility();
        RefreshIdList();
    }

    private void ApplyKindVisibility()
    {
        var manual = _kind == "cultivation";
        RealmLabel.Visibility = RealmBox.Visibility = manual ? Visibility.Visible : Visibility.Collapsed;
        SpeedLabel.Visibility = SpeedBox.Visibility = manual ? Visibility.Visible : Visibility.Collapsed;
        BreakLabel.Visibility = BreakBox.Visibility = manual ? Visibility.Visible : Visibility.Collapsed;
        DmgLabel.Visibility = DmgBox.Visibility = manual ? Visibility.Collapsed : Visibility.Visible;
        HitLabel.Visibility = HitBox.Visibility = manual ? Visibility.Collapsed : Visibility.Visible;
        BonusLabel.Visibility = BonusBox.Visibility = manual ? Visibility.Collapsed : Visibility.Visible;
        CdLabel.Visibility = CdBox.Visibility = manual ? Visibility.Collapsed : Visibility.Visible;
    }

    private void RefreshIdList()
    {
        if (_package == null) return;
        var ids = _package.OfType(_kind).Select(d => d.Id).OrderBy(x => x).ToList();
        IdList.ItemsSource = ids;
        if (ids.Count > 0) IdList.SelectedIndex = 0;
        else ClearForm();
    }

    private void IdList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_package == null || IdList.SelectedItem is not string id) return;
        _current = _package.Find(id);
        if (_current == null) return;
        LoadDef(_current);
    }

    private void ClearForm()
    {
        _current = null;
        IdBox.Text = NameBox.Text = GradeBox.Text = RealmBox.Text = SummaryBox.Text = "";
        SpeedBox.Text = BreakBox.Text = DmgBox.Text = HitBox.Text = BonusBox.Text = CdBox.Text = "";
        _tiers.Clear();
        _breaks.Clear();
    }

    private void LoadDef(DefRef def)
    {
        var raw = def.Raw;
        IdBox.Text = def.Id;
        NameBox.Text = JsonEdit.GetString(raw, "name");
        GradeBox.Text = JsonEdit.GetString(raw, "grade");
        RealmBox.Text = JsonEdit.GetString(raw, "requiredRealm");
        SummaryBox.Text = JsonEdit.GetString(raw, "effectSummary");
        SpeedBox.Text = GetNumberText(raw, "cultivationSpeed");
        BreakBox.Text = GetNumberText(raw, "breakthroughProgress");
        DmgBox.Text = GetNumberText(raw, "damageAttackMult");
        HitBox.Text = GetNumberText(raw, "hitCount");
        BonusBox.Text = GetNumberText(raw, "attackBonusPercent");
        CdBox.Text = GetNumberText(raw, "cooldownSeconds");

        _tiers.Clear();
        _breaks.Clear();
        if (raw["mastery"] is JsonObject mastery)
        {
            if (mastery["tiers"] is JsonArray tiers)
            {
                foreach (var node in tiers.OfType<JsonObject>())
                    _tiers.Add(TierRow.FromJson(node));
            }

            if (mastery["breakthroughs"] is JsonArray breaks)
            {
                foreach (var node in breaks.OfType<JsonObject>())
                    _breaks.Add(BreakRow.FromJson(node));
            }
        }
    }

    private static string GetNumberText(JsonObject raw, string key)
    {
        if (!raw.TryGetPropertyValue(key, out var n) || n == null) return "";
        return n.ToJsonString().Trim('"');
    }

    private void AddTier_Click(object sender, RoutedEventArgs e)
    {
        _tiers.Add(new TierRow { Tier = "entry", CultivationSpeed = "8", DamageAttackMult = "2.0" });
    }

    private void DeleteTier_Click(object sender, RoutedEventArgs e)
    {
        if (TierGrid.SelectedItem is TierRow row) _tiers.Remove(row);
    }

    private void AddBreak_Click(object sender, RoutedEventArgs e)
    {
        _breaks.Add(new BreakRow
        {
            From = "entry",
            To = "minor",
            ProgressRequired = "100",
            CostsText = "base:resource_spirit_herb×10, base:resource_rough_wood×10"
        });
    }

    private void DeleteBreak_Click(object sender, RoutedEventArgs e)
    {
        if (BreakGrid.SelectedItem is BreakRow row) _breaks.Remove(row);
    }

    private void NewManual_Click(object sender, RoutedEventArgs e) => CreateNew("cultivation");
    private void NewArt_Click(object sender, RoutedEventArgs e) => CreateNew("combatArt");

    private void CreateNew(string type)
    {
        if (_package == null)
        {
            MessageBox.Show("请先打开包");
            return;
        }

        var slug = type == "cultivation" ? "cultivation_new" : "art_new";
        var id = "base:" + slug;
        ContentPathRules.EnsureTypeDir(_package.Root, type);
        var fileName = slug + ".json";
        var path = Path.Combine(ContentPathRules.TypeDataDir(_package.Root, type), fileName);
        if (File.Exists(path))
        {
            MessageBox.Show("已存在 " + fileName + "，请先改名。");
            return;
        }

        JsonObject def;
        if (type == "cultivation")
        {
            def = new JsonObject
            {
                ["id"] = id,
                ["type"] = "cultivation",
                ["name"] = "新功法",
                ["requiredRealm"] = "炼气",
                ["grade"] = "黄阶下级",
                ["effectSummary"] = "",
                ["cultivationSpeed"] = 8,
                ["breakthroughProgress"] = 100,
                ["grantedModifiers"] = new JsonArray(),
                ["mastery"] = DefaultMastery(cultivation: true),
                ["tags"] = new JsonArray { "manual" }
            };
        }
        else
        {
            def = new JsonObject
            {
                ["id"] = id,
                ["type"] = "combatArt",
                ["name"] = "新斗技",
                ["grade"] = "黄阶下级",
                ["effectSummary"] = "",
                ["damageAttackMult"] = 2.0,
                ["hitCount"] = 1,
                ["cooldownSeconds"] = 4,
                ["mastery"] = DefaultMastery(cultivation: false),
                ["tags"] = new JsonArray { "art", "active" }
            };
        }

        var root = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["definitions"] = new JsonArray { def }
        };
        File.WriteAllText(
            path,
            root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) +
            Environment.NewLine,
            new System.Text.UTF8Encoding(false));
        LoadRoot(_package.Root);
        KindTabs.SelectedIndex = type == "combatArt" ? 1 : 0;
        IdList.SelectedItem = id;
        StatusText.Text = "已新建 " + path;
    }

    private static JsonObject DefaultMastery(bool cultivation)
    {
        var tiers = new JsonArray();
        if (cultivation)
        {
            tiers.Add(TierObj("novice", 6, null, null));
            tiers.Add(TierObj("entry", 8, null, null));
            tiers.Add(TierObj("minor", 10, null, null));
        }
        else
        {
            tiers.Add(TierObj("novice", null, 1.8, null));
            tiers.Add(TierObj("entry", null, 2.0, null));
            tiers.Add(TierObj("minor", null, 2.2, null));
        }

        return new JsonObject
        {
            ["tiers"] = tiers,
            ["breakthroughs"] = new JsonArray
            {
                new JsonObject
                {
                    ["from"] = "entry",
                    ["to"] = "minor",
                    ["progressRequired"] = 100,
                    ["costs"] = new JsonArray
                    {
                        new JsonObject { ["itemId"] = "base:resource_spirit_herb", ["count"] = 10 },
                        new JsonObject { ["itemId"] = "base:resource_rough_wood", ["count"] = 10 }
                    }
                }
            }
        };
    }

    private static JsonObject TierObj(string tier, int? speed, double? dmg, double? bonus)
    {
        var o = new JsonObject { ["tier"] = tier };
        if (speed.HasValue) o["cultivationSpeed"] = speed.Value;
        if (dmg.HasValue) o["damageAttackMult"] = dmg.Value;
        if (bonus.HasValue) o["attackBonusPercent"] = bonus.Value;
        return o;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null || _current == null)
        {
            MessageBox.Show("请先选择一条定义");
            return;
        }

        ApplyFormToRaw(_current.Raw);
        try
        {
            PackageStore.SaveDefinition(_package, _current);
            StatusText.Text = "已保存 " + _current.Id + " → " + _current.FilePath;
        }
        catch (Exception ex)
        {
            MessageBox.Show("保存失败：" + ex.Message);
        }
    }

    private void ApplyFormToRaw(JsonObject raw)
    {
        raw["name"] = NameBox.Text?.Trim() ?? "";
        raw["grade"] = GradeBox.Text?.Trim() ?? "";
        raw["effectSummary"] = SummaryBox.Text?.Trim() ?? "";
        if (_kind == "cultivation")
        {
            raw["type"] = "cultivation";
            raw["requiredRealm"] = RealmBox.Text?.Trim() ?? "";
            SetNumber(raw, "cultivationSpeed", SpeedBox.Text, asInt: true);
            SetNumber(raw, "breakthroughProgress", BreakBox.Text, asInt: true);
            raw.Remove("damageAttackMult");
            raw.Remove("hitCount");
            raw.Remove("attackBonusPercent");
            raw.Remove("cooldownSeconds");
            raw.Remove("damageFlat");
        }
        else
        {
            raw["type"] = "combatArt";
            SetNumber(raw, "damageAttackMult", DmgBox.Text, asInt: false);
            SetNumber(raw, "hitCount", HitBox.Text, asInt: true);
            SetNumber(raw, "attackBonusPercent", BonusBox.Text, asInt: false);
            SetNumber(raw, "cooldownSeconds", CdBox.Text, asInt: false);
            raw.Remove("requiredRealm");
            raw.Remove("cultivationSpeed");
            raw.Remove("breakthroughProgress");
            raw.Remove("grantedModifiers");
        }

        var mastery = new JsonObject
        {
            ["tiers"] = new JsonArray(_tiers.Select(t => t.ToJson(_kind == "cultivation")).ToArray()),
            ["breakthroughs"] = new JsonArray(_breaks.Select(b => b.ToJson()).ToArray())
        };
        raw["mastery"] = mastery;
    }

    private static void SetNumber(JsonObject raw, string key, string? text, bool asInt)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            raw.Remove(key);
            return;
        }

        if (asInt)
        {
            if (int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                raw[key] = i;
            return;
        }

        if (double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            raw[key] = d;
    }
}

public sealed class TierRow : INotifyPropertyChanged
{
    private string _tier = "entry";
    private string _cultivationSpeed = "";
    private string _damageAttackMult = "";
    private string _attackBonusPercent = "";
    private string _damageFlat = "";

    public string Tier { get => _tier; set { _tier = value; OnChanged(); } }
    public string CultivationSpeed { get => _cultivationSpeed; set { _cultivationSpeed = value; OnChanged(); } }
    public string DamageAttackMult { get => _damageAttackMult; set { _damageAttackMult = value; OnChanged(); } }
    public string AttackBonusPercent { get => _attackBonusPercent; set { _attackBonusPercent = value; OnChanged(); } }
    public string DamageFlat { get => _damageFlat; set { _damageFlat = value; OnChanged(); } }

    public static TierRow FromJson(JsonObject node) => new()
    {
        Tier = node["tier"]?.GetValue<string>() ?? "entry",
        CultivationSpeed = Num(node, "cultivationSpeed"),
        DamageAttackMult = Num(node, "damageAttackMult"),
        AttackBonusPercent = Num(node, "attackBonusPercent"),
        DamageFlat = Num(node, "damageFlat")
    };

    static string Num(JsonObject node, string key)
    {
        if (!node.TryGetPropertyValue(key, out var n) || n == null) return "";
        return n.ToJsonString().Trim('"');
    }

    public JsonObject ToJson(bool cultivation)
    {
        var o = new JsonObject { ["tier"] = Tier?.Trim() ?? "entry" };
        if (cultivation)
            PutInt(o, "cultivationSpeed", CultivationSpeed);
        else
        {
            PutDouble(o, "damageAttackMult", DamageAttackMult);
            PutDouble(o, "attackBonusPercent", AttackBonusPercent);
            PutInt(o, "damageFlat", DamageFlat);
        }

        return o;
    }

    static void PutInt(JsonObject o, string key, string? text)
    {
        if (int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
            o[key] = i;
    }

    static void PutDouble(JsonObject o, string key, string? text)
    {
        if (double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            o[key] = d;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnChanged([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class BreakRow : INotifyPropertyChanged
{
    private string _from = "entry";
    private string _to = "minor";
    private string _progressRequired = "100";
    private string _costsText = "";

    public string From { get => _from; set { _from = value; OnChanged(); } }
    public string To { get => _to; set { _to = value; OnChanged(); } }
    public string ProgressRequired { get => _progressRequired; set { _progressRequired = value; OnChanged(); } }
    public string CostsText { get => _costsText; set { _costsText = value; OnChanged(); } }

    public static BreakRow FromJson(JsonObject node)
    {
        var costs = new List<string>();
        if (node["costs"] is JsonArray arr)
        {
            foreach (var c in arr.OfType<JsonObject>())
            {
                var id = c["itemId"]?.GetValue<string>() ?? "";
                var count = c["count"]?.GetValue<int>() ?? 0;
                if (!string.IsNullOrEmpty(id) && count > 0)
                    costs.Add(id + "×" + count);
            }
        }

        return new BreakRow
        {
            From = node["from"]?.GetValue<string>() ?? "entry",
            To = node["to"]?.GetValue<string>() ?? "minor",
            ProgressRequired = node["progressRequired"]?.ToJsonString().Trim('"') ?? "100",
            CostsText = string.Join(", ", costs)
        };
    }

    public JsonObject ToJson()
    {
        var o = new JsonObject
        {
            ["from"] = From?.Trim() ?? "entry",
            ["to"] = To?.Trim() ?? "minor"
        };
        if (int.TryParse(ProgressRequired?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pr))
            o["progressRequired"] = pr;

        var costs = new JsonArray();
        if (!string.IsNullOrWhiteSpace(CostsText))
        {
            foreach (var part in CostsText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var x = part.IndexOf('×');
                if (x < 0) x = part.IndexOf('x');
                if (x < 0) x = part.IndexOf('X');
                if (x <= 0) continue;
                var itemId = part[..x].Trim();
                var countText = part[(x + 1)..].Trim();
                if (!int.TryParse(countText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ||
                    count <= 0 || string.IsNullOrEmpty(itemId))
                    continue;
                costs.Add(new JsonObject { ["itemId"] = itemId, ["count"] = count });
            }
        }

        o["costs"] = costs;
        return o;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnChanged([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
