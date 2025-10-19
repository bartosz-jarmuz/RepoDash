using System.Windows;

namespace RepoDash.App.Controls
{
    /// <summary>
    /// Interaction logic for RepoGroup.xaml
    /// </summary>
    public partial class RepoGroup : System.Windows.Controls.UserControl
    {
        public RepoGroup()
        {
            InitializeComponent();
        }

        private void OptionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button button || button.ContextMenu is null)
                return;

            button.ContextMenu.DataContext = button.DataContext;
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.IsOpen = true;
            e.Handled = true;
        }
    }
}
