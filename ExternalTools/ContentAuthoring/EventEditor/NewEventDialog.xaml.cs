using System.Windows;
using System.Windows.Controls;
using ContentAuthoring.Shared;

namespace EventEditor;

public partial class NewEventDialog : Window
{
    readonly ContentPackage _package;
    public string EventKind { get; private set; } = "npcNormal";
    public string EventName => NameBox.Text.Trim();
    public string NpcDefinitionId => NpcPicker.SelectedId;
    public string WorldObjectKind => UiLabels.ToKey(UiLabels.WorldObjectKinds, ObjectKindBox.SelectedItem as string ?? ObjectKindBox.Text, "controlCore");
    public string WorldObjectId => ObjectIdBox.Text.Trim();

    public NewEventDialog(ContentPackage package, string? sourceFilter = null, string? suggestedNpc = null)
    {
        InitializeComponent();
        _package = package;
        KindBox.SelectedIndex = 0;
        ObjectKindBox.ItemsSource = UiLabels.Labels(UiLabels.WorldObjectKinds);
        ObjectKindBox.SelectedIndex = 0;
        NpcPicker.Configure(package);
        NpcPicker.SourceFilter = sourceFilter ?? "";
        NpcPicker.SelectedId = suggestedNpc ?? "";
    }

    void KindBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        EventKind = (KindBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "npcNormal";
        if (NpcPanel == null || ObjectPanel == null) return;
        NpcPanel.Visibility = EventKind is "npcNormal" or "npcSpecial" ? Visibility.Visible : Visibility.Collapsed;
        ObjectPanel.Visibility = EventKind == "object" ? Visibility.Visible : Visibility.Collapsed;
    }

    void ObjectKindBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ObjectIdBox == null) return;
        var keep = ObjectIdBox.Text;
        ObjectIdBox.ItemsSource = PackageStore.WorldObjectIds(_package, WorldObjectKind);
        ObjectIdBox.Text = keep;
    }

    void Create_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EventName)) { MessageBox.Show("请输入事件名称。"); return; }
        if (EventKind is "npcNormal" or "npcSpecial" && string.IsNullOrWhiteSpace(NpcDefinitionId)) { MessageBox.Show("请选择互动人物。"); return; }
        DialogResult = true;
    }
}
