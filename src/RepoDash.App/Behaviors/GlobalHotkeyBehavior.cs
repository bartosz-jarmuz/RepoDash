using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace RepoDash.App.Behaviors;

public sealed class GlobalHotkeyBehavior : DependencyObject
{
    public static readonly DependencyProperty HotkeyProperty =
        DependencyProperty.RegisterAttached(
            "Hotkey",
            typeof(string),
            typeof(GlobalHotkeyBehavior),
            new PropertyMetadata(string.Empty, OnHotkeyChanged));

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached(
            "Command",
            typeof(ICommand),
            typeof(GlobalHotkeyBehavior),
            new PropertyMetadata(null));

    public static void SetHotkey(DependencyObject element, string value) => element.SetValue(HotkeyProperty, value);
    public static string GetHotkey(DependencyObject element) => (string)element.GetValue(HotkeyProperty);

    public static void SetCommand(DependencyObject element, ICommand? value) => element.SetValue(CommandProperty, value);
    public static ICommand? GetCommand(DependencyObject element) => (ICommand?)element.GetValue(CommandProperty);

    private const int WM_HOTKEY = 0x0312;

    private static void OnHotkeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Window w) return;

        var source = (HwndSource?)PresentationSource.FromVisual(w);
        void EnsureHook()
        {
            var handle = new WindowInteropHelper(w).EnsureHandle();
            var hs = HwndSource.FromHwnd(handle);
            hs?.AddHook(Hook);
            w.SetValue(_hwndSourceProperty, hs);
        }

        if (source is null) w.SourceInitialized += (_, __) => EnsureHook();
        else EnsureHook();

        RegisterHotkey(w, e.NewValue as string);
    }

    private static void RegisterHotkey(Window w, string? spec)
    {
        UnregisterHotkey(w);

        if (string.IsNullOrWhiteSpace(spec)) return;
        if (!TryParse(spec, out var mods, out var key)) return;

        var handle = new WindowInteropHelper(w).Handle;
        RegisterHotKey(handle, 1, mods, key);
        w.SetValue(_hotkeyRegisteredProperty, true);
    }

    private static void UnregisterHotkey(Window w)
    {
        var registered = (bool)(w.GetValue(_hotkeyRegisteredProperty) ?? false);
        if (!registered) return;
        var handle = new WindowInteropHelper(w).Handle;
        UnregisterHotKey(handle, 1);
        w.SetValue(_hotkeyRegisteredProperty, false);
    }

    private static readonly DependencyProperty _hwndSourceProperty =
        DependencyProperty.RegisterAttached("_hwndSource", typeof(HwndSource), typeof(GlobalHotkeyBehavior));

    private static readonly DependencyProperty _hotkeyRegisteredProperty =
        DependencyProperty.RegisterAttached("_hotkeyRegistered", typeof(bool), typeof(GlobalHotkeyBehavior), new PropertyMetadata(false));

    private static IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == 1)
        {
            var w = App.Current?.Windows.OfType<Window>().FirstOrDefault(x => new WindowInteropHelper(x).Handle == hwnd);
            if (w is not null)
            {
                if (!w.IsVisible) w.Show();
                if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
                w.Activate();

                var cmd = GetCommand(w);
                if (cmd?.CanExecute(null) == true) cmd.Execute(null);
            }
            handled = true;
        }
        return IntPtr.Zero;
    }

    private static bool TryParse(string spec, out uint modifiers, out uint key)
    {
        modifiers = 0;
        key = 0;

        var parts = spec.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;

        foreach (var p in parts[..^1])
        {
            if (p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) modifiers |= 0x0002;
            else if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase)) modifiers |= 0x0001;
            else if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase)) modifiers |= 0x0004;
            else if (p.Equals("Win", StringComparison.OrdinalIgnoreCase)) modifiers |= 0x0008;
        }

        if (Enum.TryParse<Key>(parts[^1], true, out var wpfKey))
        {
            key = (uint)KeyInterop.VirtualKeyFromKey(wpfKey);
            return true;
        }

        return false;
    }

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}