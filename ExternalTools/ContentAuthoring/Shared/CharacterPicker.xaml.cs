using System.Windows;
using System.Windows.Controls;

namespace ContentAuthoring.Shared;

public partial class CharacterPicker : UserControl
{
    ContentPackage? _package;
    IReadOnlyList<CharacterDefinitionInfo> _characters = Array.Empty<CharacterDefinitionInfo>();
    string _selectedId = "";
    string _sourceFilter = "";

    public CharacterPicker()
    {
        InitializeComponent();
        IsEnabledChanged += (_, _) => ChooseButton.IsEnabled = IsEnabled;
        RefreshDisplay();
    }

    public event EventHandler? SelectionChanged;

    public string SelectedId
    {
        get => _selectedId;
        set
        {
            var normalized = value?.Trim() ?? "";
            if (string.Equals(_selectedId, normalized, StringComparison.Ordinal)) return;
            _selectedId = normalized;
            RefreshDisplay();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>由宿主编辑器持有的全局来源筛选；空字符串表示全部来源。</summary>
    public string SourceFilter
    {
        get => _sourceFilter;
        set
        {
            _sourceFilter = value?.Trim() ?? "";
            RefreshDisplay();
        }
    }

    public void Configure(ContentPackage? package)
    {
        _package = package;
        _characters = package == null
            ? Array.Empty<CharacterDefinitionInfo>()
            : PackageStore.AllCharacterDefinitions(package);
        RefreshDisplay();
    }

    void Choose_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null) return;
        var dialog = new CharacterPickerDialog(_package, SelectedId, SourceFilter)
        {
            Owner = Window.GetWindow(this)
        };
        if (dialog.ShowDialog() == true)
            SelectedId = dialog.SelectedId;
    }

    void RefreshDisplay()
    {
        if (SelectionText == null || SourceText == null || FilterHintText == null) return;
        var selected = _characters.FirstOrDefault(character =>
            string.Equals(character.Id, _selectedId, StringComparison.Ordinal));
        if (selected == null)
        {
            SelectionText.Text = string.IsNullOrWhiteSpace(_selectedId) ? "未选择人物" : _selectedId;
            SourceText.Text = string.IsNullOrWhiteSpace(_selectedId) ? "" : "当前内容包中未找到此人物定义";
            FilterHintText.Visibility = Visibility.Collapsed;
            ToolTip = SourceText.Text;
            return;
        }

        SelectionText.Text = $"{selected.Name} — {selected.Id}";
        SourceText.Text = "来源：" + selected.SourceRelativePath;
        FilterHintText.Visibility = string.IsNullOrWhiteSpace(SourceFilter) ||
            string.Equals(SourceFilter, selected.SourceRelativePath, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Collapsed
            : Visibility.Visible;
        ToolTip = $"{selected.Name}\n{selected.Id}\n来源：{selected.SourceRelativePath}";
    }
}
