using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using RepoDash.App.ViewModels;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using UserControl = System.Windows.Controls.UserControl;

namespace RepoDash.App.Controls;

public partial class RepoItem : UserControl
{
    public RepoItem()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty AllowPinningProperty =
        DependencyProperty.Register(
            nameof(AllowPinning),
            typeof(bool),
            typeof(RepoItem),
            new PropertyMetadata(true));

    public bool AllowPinning
    {
        get => (bool)GetValue(AllowPinningProperty);
        set => SetValue(AllowPinningProperty, value);
    }

    private void OnRootMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2)
            return;

        if (IsInteractiveChild(e.OriginalSource as DependencyObject))
            return;

        if (DataContext is RepoItemViewModel vm && vm.LaunchCommand.CanExecute(null))
        {
            vm.LaunchCommand.Execute(null);
            e.Handled = true;
        }
    }

    private static bool IsInteractiveChild(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ButtonBase || source is Hyperlink)
                return true;

            if (source is TextElement textElement)
            {
                source = textElement.Parent as DependencyObject;
                continue;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }
}
