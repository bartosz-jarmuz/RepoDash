namespace RepoDash.App.Views.Shortcuts;

using System.Windows;

public partial class ShortcutEntryEditorWindow : Window
{
    public ShortcutEntryEditorWindow()
    {
        InitializeComponent();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
        => DialogResult = true;

    private void OnCancelClick(object sender, RoutedEventArgs e)
        => DialogResult = false;
}

