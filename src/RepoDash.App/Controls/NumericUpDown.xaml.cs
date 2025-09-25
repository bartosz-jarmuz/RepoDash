using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace RepoDash.App.Controls;

public partial class NumericUpDown : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(int),
        typeof(NumericUpDown),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum),
        typeof(int),
        typeof(NumericUpDown),
        new PropertyMetadata(0));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum),
        typeof(int),
        typeof(NumericUpDown),
        new PropertyMetadata(100));

    public NumericUpDown()
    {
        InitializeComponent();
        ValueTextBox.LostFocus += (_, _) => CoerceFromText();
        ValueTextBox.PreviewKeyDown += (_, args) =>
        {
            if (args.Key == System.Windows.Input.Key.Enter)
            {
                CoerceFromText();
            }
        };
    }

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public int Minimum
    {
        get => (int)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public int Maximum
    {
        get => (int)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NumericUpDown control)
        {
            control.ValueTextBox.Text = control.Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    private void OnIncrement(object sender, RoutedEventArgs e)
    {
        if (Value < Maximum)
        {
            Value++;
        }
    }

    private void OnDecrement(object sender, RoutedEventArgs e)
    {
        if (Value > Minimum)
        {
            Value--;
        }
    }

    private void CoerceFromText()
    {
        if (int.TryParse(ValueTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            Value = Math.Max(Minimum, Math.Min(Maximum, parsed));
        }
        else
        {
            ValueTextBox.Text = Value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
