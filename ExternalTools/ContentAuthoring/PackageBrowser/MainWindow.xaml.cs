using System.Windows;
using System.Windows.Controls;
using ContentAuthoring.Shared;
using Microsoft.Win32;

namespace PackageBrowser;

public partial class MainWindow : Window
{
    private ContentPackage? _package;

    public MainWindow()
    {
        InitializeComponent();
        Title = "XianXia · 包总览与校验台";
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
        try
        {
            _package = PackageStore.Load(root);
            RootText.Text = root;
            StatusText.Text = $"已加载 {_package.Definitions.Count} 条 · {_package.Files.Count} 个 JSON";
            TypeList.ItemsSource = _package.Definitions
                .GroupBy(d => d.Type)
                .OrderBy(g => g.Key)
                .Select(g => $"{g.Key} ({g.Count()})")
                .ToList();
            if (TypeList.Items.Count > 0) TypeList.SelectedIndex = 0;
            IssueList.ItemsSource = null;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "加载失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenPackage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "选择 Content/BaseGame 包目录" };
        if (dlg.ShowDialog() == true) LoadRoot(dlg.FolderName);
    }

    private void TypeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_package == null || TypeList.SelectedItem is not string label) return;
        var type = label.Split(' ')[0];
        DefGrid.ItemsSource = _package.OfType(type)
            .Select(d => new { d.Id, d.Name, File = System.IO.Path.GetFileName(d.FilePath) })
            .ToList();
    }

    private void Validate_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null) return;
        var issues = PackageValidator.Validate(_package);
        IssueList.ItemsSource = issues.Select(i => $"[{i.Level}] {i.Message}").ToList();
        StatusText.Text = issues.Count == 0 ? "校验通过" : $"校验完成：{issues.Count} 条问题";
    }

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(RootText.Text)) LoadRoot(RootText.Text);
    }
}
