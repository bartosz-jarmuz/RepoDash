using System.Windows;
using System.Windows.Controls;
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
}
