namespace RepoDash.App.Views.Shortcuts;

using System.Windows;

public partial class ShortcutRenameWindow : Window
{
    public ShortcutRenameWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    public string? DisplayName { get; set; }

    private void OnSaveClick(object sender, RoutedEventArgs e)
        => DialogResult = true;

    private void OnCancelClick(object sender, RoutedEventArgs e)
        => DialogResult = false;
}

