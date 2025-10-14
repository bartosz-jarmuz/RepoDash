using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Application = System.Windows.Application;

namespace RepoDash.App.Commands;

public sealed class FocusElementCommand : ICommand
{
    public static ICommand Instance { get; } = new FocusElementCommand();

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) =>
        parameter is IInputElement
        || parameter is FrameworkElement
        || parameter is string s && !string.IsNullOrWhiteSpace(s);

    public void Execute(object? parameter)
    {
        // 1) Direct element given
        if (parameter is IInputElement el)
        {
            Focus(el);
            return;
        }

        // 2) FrameworkElement given
        if (parameter is FrameworkElement fe)
        {
            Focus(fe);
            return;
        }

        // 3) Name string given
        if (parameter is string name && !string.IsNullOrWhiteSpace(name))
        {
            var window = GetActiveWindow();
            if (window is null) return;

            var target = FindByName(window, name);
            if (target is IInputElement ie)
                Focus(ie);

            return;
        }
    }

    private FocusElementCommand() { }

    private static Window? GetActiveWindow()
    {
        foreach (Window w in Application.Current.Windows)
            if (w.IsActive) return w;
        return Application.Current.MainWindow;
    }

    private static FrameworkElement? FindByName(DependencyObject root, string name)
    {
        if (root is FrameworkElement fe && fe.Name == name)
            return fe;

        var visualCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < visualCount; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            var match = FindByName(child, name);
            if (match is not null) return match;
        }

        if (root is FrameworkElement fe2)
        {
            foreach (var obj in LogicalTreeHelper.GetChildren(fe2))
            {
                if (obj is DependencyObject d)
                {
                    var match = FindByName(d, name);
                    if (match is not null) return match;
                }
            }
        }

        return null;
    }

    private static void Focus(IInputElement el)
    {
        el.Focus();
        if (el is TextBox tb)
            tb.SelectAll();
    }
}