using System.Windows;
using TextBox = System.Windows.Controls.TextBox;

namespace RepoDash.App.Behaviors;

public static class FocusOnRequest
{
    public static readonly DependencyProperty IsRequestedProperty =
        DependencyProperty.RegisterAttached(
            "IsRequested",
            typeof(bool),
            typeof(FocusOnRequest),
            new PropertyMetadata(false, OnIsRequestedChanged));

    public static void SetIsRequested(DependencyObject element, bool value) => element.SetValue(IsRequestedProperty, value);
    public static bool GetIsRequested(DependencyObject element) => (bool)element.GetValue(IsRequestedProperty);

    private static void OnIsRequestedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox tb) return;
        if (e.NewValue is true)
        {
            tb.Focus();
            tb.SelectAll();
        }
    }
}