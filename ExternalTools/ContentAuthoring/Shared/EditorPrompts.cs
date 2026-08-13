using System.Windows;
using System.Windows.Controls;

namespace ContentAuthoring.Shared;

public static class EditorPrompts
{
    public static bool TryPromptText(string title, string label, string initial, out string value)
    {
        value = initial;
        var win = new Window
        {
            Title = title,
            Width = 420,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false
        };
        try
        {
            if (Application.Current?.MainWindow is { IsLoaded: true } owner)
                win.Owner = owner;
        }
        catch { /* ignore */ }

        var box = new TextBox { Text = initial, Margin = new Thickness(0, 8, 0, 8) };
        var ok = false;
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var okBtn = new Button { Content = "确定", Width = 72, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var cancelBtn = new Button { Content = "取消", Width = 72, IsCancel = true };
        okBtn.Click += (_, _) => { ok = true; win.DialogResult = true; };
        cancelBtn.Click += (_, _) => { win.DialogResult = false; };
        buttons.Children.Add(okBtn);
        buttons.Children.Add(cancelBtn);
        var stack = new StackPanel { Margin = new Thickness(12) };
        stack.Children.Add(new TextBlock { Text = label });
        stack.Children.Add(box);
        stack.Children.Add(buttons);
        win.Content = stack;
        win.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };
        var result = win.ShowDialog() == true && ok;
        value = box.Text ?? "";
        return result;
    }
}
