#nullable enable
using System.ComponentModel;
using System.Windows;
using System.Windows.Forms;

namespace RepoDash.App.Behaviors;

public sealed class TrayBehavior : DependencyObject
{
    public static readonly DependencyProperty EnabledProperty =
        DependencyProperty.RegisterAttached(
            "Enabled",
            typeof(bool),
            typeof(TrayBehavior),
            new PropertyMetadata(false, OnEnabledChanged));

    public static readonly DependencyProperty ShowCommandProperty =
        DependencyProperty.RegisterAttached(
            "ShowCommand",
            typeof(System.Windows.Input.ICommand),
            typeof(TrayBehavior),
            new PropertyMetadata(null));

    public static void SetEnabled(DependencyObject element, bool value) => element.SetValue(EnabledProperty, value);
    public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);

    public static void SetShowCommand(DependencyObject element, System.Windows.Input.ICommand? value) => element.SetValue(ShowCommandProperty, value);
    public static System.Windows.Input.ICommand? GetShowCommand(DependencyObject element) => (System.Windows.Input.ICommand?)element.GetValue(ShowCommandProperty);

    private static readonly DependencyProperty _iconProperty =
        DependencyProperty.RegisterAttached("_icon", typeof(NotifyIcon), typeof(TrayBehavior));

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Window w) return;

        // Dispose old icon if toggled off
        if (e.NewValue is false)
        {
            if (w.GetValue(_iconProperty) is NotifyIcon oldNi)
            {
                oldNi.Visible = false;
                oldNi.Dispose();
                w.ClearValue(_iconProperty);
            }
            return;
        }

        // Enabled == true => create tray icon and wire close-to-tray
        var icon = new NotifyIcon
        {
            Text = "RepoDash",
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true
        };

        var menu = new ContextMenuStrip();

        var show = new ToolStripMenuItem("Show");
        show.Click += (_, __) => ShowWindow(w);
        menu.Items.Add(show);

        var exit = new ToolStripMenuItem("Exit");
        exit.Click += (_, __) => App.Current.Shutdown();
        menu.Items.Add(exit);

        icon.ContextMenuStrip = menu;
        icon.DoubleClick += (_, __) => ShowWindow(w);

        // IMPORTANT: close-to-tray
        w.Closing += OnClosingToTray;

        // Dispose tray icon when window is closed for real (via Exit)
        w.Closed += (_, __) =>
        {
            icon.Visible = false;
            icon.Dispose();
        };

        w.SetValue(_iconProperty, icon);

        void OnClosingToTray(object? sender, CancelEventArgs args)
        {
            // If behavior still enabled, intercept close => hide to tray
            if (GetEnabled(w))
            {
                args.Cancel = true;
                w.Hide();
            }
            else
            {
                // If disabled, unhook and let it close
                w.Closing -= OnClosingToTray;
            }
        }
    }

    private static void ShowWindow(Window w)
    {
        w.Show();
        if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
        w.Activate();

        var cmd = GetShowCommand(w);
        if (cmd?.CanExecute(null) == true) cmd.Execute(null);
    }
}
