using System.Windows;
using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;

namespace RepoDash.App.Controls;

public partial class NumericUpDown : UserControl
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(int), typeof(NumericUpDown),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public NumericUpDown() => InitializeComponent();

    private void Up_Click(object sender, RoutedEventArgs e) => Value++;
    private void Down_Click(object sender, RoutedEventArgs e) => Value--;
}