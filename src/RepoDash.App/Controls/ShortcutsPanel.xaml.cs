namespace RepoDash.App.Controls;

using Microsoft.Extensions.DependencyInjection;
using RepoDash.App.Abstractions;
using RepoDash.App.Services;
using RepoDash.App.ViewModels.Settings;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Settings;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

public partial class ShortcutsPanel : UserControl, INotifyPropertyChanged
{
    private bool _initialized;
    private IReadOnlySettingsSource<ShortcutsSettings>? _src;
    private ILauncher? _launcher;
    private IShortcutIconProvider? _icons;

    public ShortcutsPanel()
    {
        InitializeComponent();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ShortcutDisplayItem> DisplayItems { get; } = new();

    public Orientation ItemsOrientation { get; private set; } = Orientation.Vertical;

    public double IconPixelSize { get; private set; } = 32;

    public double GlyphFontSize { get; private set; } = 16;

    public double MaxLabelWidth { get; private set; } = 96;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        if (DesignerProperties.GetIsInDesignMode(this)) return;

        var services = App.Services;
        _src = services.GetRequiredService<IReadOnlySettingsSource<ShortcutsSettings>>();
        _launcher = services.GetRequiredService<ILauncher>();
        _icons = services.GetRequiredService<IShortcutIconProvider>();

        _src.PropertyChanged += OnSettingsChanged;
        _initialized = true;

        RefreshFromSettings();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_src is not null)
        {
            _src.PropertyChanged -= OnSettingsChanged;
        }
        _initialized = false;
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IReadOnlySettingsSource<ShortcutsSettings>.Current) || e.PropertyName is null)
        {
            RefreshFromSettings();
        }
    }

    private void RefreshFromSettings()
    {
        var cur = _src?.Current;
        if (cur is null) return;

        ApplyPlacement(cur.Placement);
        ApplyItemSize(cur.ItemSize);
        RebuildItems(cur);

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayItems)));
    }

    private void ApplyPlacement(ShortcutsPanelPlacement placement)
    {
        ItemsOrientation = (placement == ShortcutsPanelPlacement.Top || placement == ShortcutsPanelPlacement.Bottom)
            ? Orientation.Horizontal : Orientation.Vertical;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemsOrientation)));

        var dock = placement switch
        {
            ShortcutsPanelPlacement.Top => Dock.Top,
            ShortcutsPanelPlacement.Bottom => Dock.Bottom,
            ShortcutsPanelPlacement.Left => Dock.Left,
            ShortcutsPanelPlacement.Right => Dock.Right,
            _ => Dock.Left
        };
        DockPanel.SetDock(this, dock);
    }

    private void ApplyItemSize(ShortcutsItemSize size)
    {
        IconPixelSize = size switch
        {
            ShortcutsItemSize.Small => 20,
            ShortcutsItemSize.Medium => 28,
            ShortcutsItemSize.Large => 40,
            _ => 28
        };
        GlyphFontSize = Math.Round(IconPixelSize * 0.6, MidpointRounding.AwayFromZero);
        MaxLabelWidth = IconPixelSize * 3.2;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IconPixelSize)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GlyphFontSize)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MaxLabelWidth)));
    }

    private void RebuildItems(ShortcutsSettings s)
    {
        DisplayItems.Clear();
        foreach (var e in s.ShortcutEntries)
        {
            var name = string.IsNullOrWhiteSpace(e.DisplayName)
                ? SafeFileNameOrUrlHost(e.Target)
                : e.DisplayName;

            var icon = _icons?.GetIcon(e);

            DisplayItems.Add(new ShortcutDisplayItem
            {
                DisplayName = name,
                Target = e.Target,
                Arguments = e.Arguments,
                Icon = icon,
                FallbackGlyph = ComputeFallbackGlyph(name)
            });
        }
    }

    private static string SafeFileNameOrUrlHost(string target)
    {
        if (Uri.TryCreate(target, UriKind.Absolute, out var uri))
        {
            if (!string.IsNullOrEmpty(uri.Host)) return uri.Host;
            return uri.Segments.LastOrDefault()?.Trim('/') ?? target;
        }
        try { return Path.GetFileNameWithoutExtension(target); }
        catch { return target; }
    }

    private static string ComputeFallbackGlyph(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "🔗";
        var ch = name.Trim()[0];
        return char.IsLetterOrDigit(ch) ? ch.ToString().ToUpperInvariant() : "🔗";
    }

    private void OnShortcutClick(object sender, RoutedEventArgs e)
    {
        if (_launcher is null) return;
        if (sender is not Button btn) return;
        if (btn.DataContext is not ShortcutDisplayItem item) return;

        _launcher.OpenTarget(item.Target, item.Arguments);
    }

    public sealed class ShortcutDisplayItem
    {
        public string DisplayName { get; init; } = string.Empty;
        public string Target { get; init; } = string.Empty;
        public string? Arguments { get; init; }
        public ImageSource? Icon { get; init; }
        public string FallbackGlyph { get; init; } = "🔗";
    }
}

public static class VisibilityConverters
{
    public static IValueConverter FromNullToCollapsed { get; } = new NullToVisibilityConverter(false);
    public static IValueConverter FromNotNullToCollapsed { get; } = new NullToVisibilityConverter(true);

    private sealed class NullToVisibilityConverter : IValueConverter
    {
        private readonly bool _invert;
        public NullToVisibilityConverter(bool invert) => _invert = invert;

        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            var isNull = value is null;
            if (!_invert) return isNull ? Visibility.Collapsed : Visibility.Visible;
            return isNull ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            => throw new NotSupportedException();
    }
}
