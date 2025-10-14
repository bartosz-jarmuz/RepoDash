using System.Windows;
using System.Windows.Input;

namespace RepoDash.App.Behaviors;

public static class WindowRestoreFocusBehavior
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached(
            "Command",
            typeof(ICommand),
            typeof(WindowRestoreFocusBehavior),
            new PropertyMetadata(null, OnCommandChanged));

    public static void SetCommand(DependencyObject element, ICommand? value) => element.SetValue(CommandProperty, value);
    public static ICommand? GetCommand(DependencyObject element) => (ICommand?)element.GetValue(CommandProperty);

    private static readonly DependencyProperty _lastStateProperty =
        DependencyProperty.RegisterAttached("_lastState", typeof(WindowState), typeof(WindowRestoreFocusBehavior), new PropertyMetadata(WindowState.Normal));

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Window w) return;

        // Unhook if command removed
        if (e.NewValue is null)
        {
            w.StateChanged -= OnStateChanged;
            w.Activated -= OnActivated;
            return;
        }

        // Hook once
        w.StateChanged -= OnStateChanged;
        w.Activated -= OnActivated;
        w.StateChanged += OnStateChanged;
        w.Activated += OnActivated;

        // store initial state
        w.SetValue(_lastStateProperty, w.WindowState);

        void OnActivated(object? sender, EventArgs args)
        {
            Execute(w);
        }

        void OnStateChanged(object? sender, EventArgs args)
        {
            var last = (WindowState)w.GetValue(_lastStateProperty);
            var now = w.WindowState;

            if (last == WindowState.Minimized && now != WindowState.Minimized)
            {
                Execute(w);
            }

            w.SetValue(_lastStateProperty, now);
        }

        static void Execute(Window w)
        {
            var cmd = GetCommand(w);
            if (cmd?.CanExecute(null) == true) cmd.Execute(null);
        }
    }
}