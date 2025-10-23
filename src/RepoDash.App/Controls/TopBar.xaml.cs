using System.Windows;
using System.Windows.Controls;
using RepoDash.App.Services;
using UserControl = System.Windows.Controls.UserControl;

namespace RepoDash.App.Controls;

public partial class TopBar : UserControl
{
    public TopBar() => InitializeComponent();

    private void OnLayoutMetricChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        LayoutRefreshCoordinator.Default.Refresh();
    }
}
