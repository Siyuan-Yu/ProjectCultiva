using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ContentAuthoring.Shared;

public partial class CharacterPickerDialog : Window
{
    readonly IReadOnlyList<CharacterDefinitionInfo> _characters;
    readonly ICollectionView _view;

    public string SelectedId { get; private set; } = "";

    public CharacterPickerDialog(ContentPackage package, string? selectedId = null, string? sourceFilter = null)
    {
        InitializeComponent();
        var allCharacters = PackageStore.AllCharacterDefinitions(package);
        _characters = string.IsNullOrWhiteSpace(sourceFilter)
            ? allCharacters
            : allCharacters.Where(character => string.Equals(
                character.SourceRelativePath, sourceFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        ResultList.ItemsSource = _characters;
        _view = CollectionViewSource.GetDefaultView(ResultList.ItemsSource);
        _view.Filter = MatchesFilter;
        if (!string.IsNullOrWhiteSpace(selectedId))
        {
            ResultList.SelectedItem = _characters.FirstOrDefault(character =>
                string.Equals(character.Id, selectedId, StringComparison.Ordinal));
            if (ResultList.SelectedItem != null)
                ResultList.ScrollIntoView(ResultList.SelectedItem);
        }
        Loaded += (_, _) => SearchBox.Focus();
        UpdateCount();
    }

    bool MatchesFilter(object item)
    {
        if (item is not CharacterDefinitionInfo character) return false;
        var query = SearchBox?.Text?.Trim() ?? "";
        return query.Length == 0 || character.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            character.Id.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    void Filter_Changed(object sender, EventArgs e)
    {
        if (_view == null) return;
        _view.Refresh();
        if (ResultList.SelectedItem != null && !_view.Contains(ResultList.SelectedItem))
            ResultList.SelectedItem = null;
        UpdateCount();
    }

    void UpdateCount() => CountText.Text = $"显示 {_view.Cast<object>().Count()} / {_characters.Count} 个人物";

    void Accept_Click(object sender, RoutedEventArgs e)
    {
        if (ResultList.SelectedItem is not CharacterDefinitionInfo selected)
        {
            MessageBox.Show(this, "请选择一个人物。", "尚未选择", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        SelectedId = selected.Id;
        DialogResult = true;
    }

    void ResultList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ResultList.SelectedItem is CharacterDefinitionInfo)
            Accept_Click(sender, e);
    }
}
