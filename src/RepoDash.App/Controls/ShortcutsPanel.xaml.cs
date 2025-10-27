using System.Windows.Media.Media3D;

namespace RepoDash.App.Controls;

using Microsoft.Extensions.DependencyInjection;
using RepoDash.App.Abstractions;
using RepoDash.App.Services;
using RepoDash.App.Views.Shortcuts;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Settings;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

public partial class ShortcutsPanel : UserControl, INotifyPropertyChanged
{
    private bool _initialized;
    private IReadOnlySettingsSource<ShortcutsSettings>? _src;
    private ISettingsStore<ShortcutsSettings>? _store;
    private ILauncher? _launcher;
    private IShortcutIconProvider? _icons;
    private Point? _dragStartPoint;
    private FrameworkElement? _dropIndicatorElement;
    private DropInsertionAdorner? _dropIndicator;
    private AdornerLayer? _dropIndicatorLayer;
    private ShortcutsPanelPlacement _currentPlacement = ShortcutsPanelPlacement.Left;
    private double _lastPersistedWidth = DefaultPanelWidth;
    private double _baseIconSize = 28;
    private double _baseGlyphSize = 16;
    private double _baseLabelWidth = 96;
    private double _baseLabelFontSize = 12;
    private bool _isResizeEnabled;

    private const double DefaultPanelWidth = 148;
    private const double MinPanelWidth = 64;
    private const double MaxPanelWidth = 148;
    private const double MinScale = 0.45;

    public ShortcutsPanel()
    {
        InitializeComponent();
        SizeChanged += OnPanelSizeChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ShortcutDisplayItem> DisplayItems { get; } = new();

    public Orientation ItemsOrientation { get; private set; } = Orientation.Vertical;

    private double _iconPixelSize = 32;
    public double IconPixelSize
    {
        get => _iconPixelSize;
        private set => SetDoubleField(ref _iconPixelSize, value, nameof(IconPixelSize));
    }

    private double _glyphFontSize = 16;
    public double GlyphFontSize
    {
        get => _glyphFontSize;
        private set => SetDoubleField(ref _glyphFontSize, value, nameof(GlyphFontSize));
    }

    private double _maxLabelWidth = 96;
    public double MaxLabelWidth
    {
        get => _maxLabelWidth;
        private set => SetDoubleField(ref _maxLabelWidth, value, nameof(MaxLabelWidth));
    }

    private double _labelFontSize = 12;
    public double LabelFontSize
    {
        get => _labelFontSize;
        private set => SetDoubleField(ref _labelFontSize, value, nameof(LabelFontSize));
    }

    public bool IsResizeEnabled
    {
        get => _isResizeEnabled;
        private set => SetBoolField(ref _isResizeEnabled, value, nameof(IsResizeEnabled));
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        if (DesignerProperties.GetIsInDesignMode(this)) return;

        var services = App.Services;
        _src = services.GetRequiredService<IReadOnlySettingsSource<ShortcutsSettings>>();
        _store = services.GetRequiredService<ISettingsStore<ShortcutsSettings>>();
        _launcher = services.GetRequiredService<ILauncher>();
        _icons = services.GetRequiredService<IShortcutIconProvider>();

        _src.PropertyChanged += OnSettingsChanged;
        _initialized = true;

        PanelBorder.SizeChanged += OnPanelBorderSizeChanged;

        RefreshFromSettings();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        PanelBorder.SizeChanged -= OnPanelBorderSizeChanged;

        if (_src is not null)
        {
            _src.PropertyChanged -= OnSettingsChanged;
        }
        HideDropIndicator();
        _src = null;
        _store = null;
        _launcher = null;
        _icons = null;
        _dragStartPoint = null;
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
        ApplyPanelWidth(cur.PanelWidth);
        RebuildItems(cur);

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayItems)));
    }

    private void ApplyPlacement(ShortcutsPanelPlacement placement)
    {
        _currentPlacement = placement;

        var orientation = (placement == ShortcutsPanelPlacement.Top || placement == ShortcutsPanelPlacement.Bottom)
            ? Orientation.Horizontal
            : Orientation.Vertical;

        if (ItemsOrientation != orientation)
        {
            ItemsOrientation = orientation;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemsOrientation)));
        }

        var dock = placement switch
        {
            ShortcutsPanelPlacement.Top => Dock.Top,
            ShortcutsPanelPlacement.Bottom => Dock.Bottom,
            ShortcutsPanelPlacement.Left => Dock.Left,
            ShortcutsPanelPlacement.Right => Dock.Right,
            _ => Dock.Left
        };

        DockPanel.SetDock(this, dock);

        var isVertical = IsVerticalPlacement();
        IsResizeEnabled = isVertical;

        if (isVertical)
        {
            MinWidth = MinPanelWidth;
            MaxWidth = MaxPanelWidth;
        }
        else
        {
            Width = double.NaN;
            MinWidth = 0;
            MaxWidth = double.PositiveInfinity;
        }

        UpdateResizeThumbPlacement();
    }

    private void ApplyItemSize(ShortcutsItemSize size)
    {
        _baseIconSize = size switch
        {
            ShortcutsItemSize.Small => 20,
            ShortcutsItemSize.Medium => 28,
            ShortcutsItemSize.Large => 40,
            _ => 28
        };

        _baseGlyphSize = Math.Round(_baseIconSize * 0.6, MidpointRounding.AwayFromZero);
        _baseLabelWidth = _baseIconSize * 3.2;
        _baseLabelFontSize = size switch
        {
            ShortcutsItemSize.Small => 11,
            ShortcutsItemSize.Medium => 12,
            ShortcutsItemSize.Large => 14,
            _ => 12
        };

        UpdateAdaptiveSizing(GetEffectiveWidthForSizing());
    }

    private void ApplyPanelWidth(double width)
    {
        var clamped = ClampWidth(width);
        _lastPersistedWidth = clamped;

        if (!IsVerticalPlacement())
        {
            UpdateAdaptiveSizing(GetEffectiveWidthForSizing());
            return;
        }

        Width = clamped;
        UpdateAdaptiveSizing(clamped);
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
                Entry = e,
                DisplayName = name,
                Target = e.Target,
                Arguments = e.Arguments,
                Icon = icon,
                FallbackGlyph = ComputeFallbackGlyph(name)
            });
        }
    }

    private void OnPanelSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (!_initialized) return;
        if (e.WidthChanged)
        {
            var width = IsVerticalPlacement()
                ? ClampWidth(e.NewSize.Width)
                : Math.Min(MaxPanelWidth, e.NewSize.Width);
            UpdateAdaptiveSizing(width);
        }
    }

    private void OnResizeThumbDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!IsResizeEnabled) return;

        var delta = e.HorizontalChange;
        if (_currentPlacement == ShortcutsPanelPlacement.Right)
        {
            delta = -delta;
        }

        if (Math.Abs(delta) < double.Epsilon) return;

        var target = ClampWidth(GetEffectiveWidth() + delta);
        Width = target;
        UpdateAdaptiveSizing(target);
    }

    private async void OnResizeThumbDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (!IsResizeEnabled || _store is null) return;

        var width = ClampWidth(GetEffectiveWidth());
        if (Math.Abs(width - _lastPersistedWidth) < 0.5) return;

        _lastPersistedWidth = width;
        await _store.UpdateAsync(settings => settings.PanelWidth = width);
    }

    private bool IsVerticalPlacement()
        => _currentPlacement == ShortcutsPanelPlacement.Left || _currentPlacement == ShortcutsPanelPlacement.Right;

    private double GetEffectiveWidthForSizing()
    {
        if (IsVerticalPlacement())
        {
            return ClampWidth(GetEffectiveWidth());
        }

        var actual = ActualWidth;
        if (double.IsNaN(actual) || actual <= 0)
            return MaxPanelWidth;
        return Math.Min(MaxPanelWidth, actual);
    }

    private double GetEffectiveWidth()
    {
        var width = double.IsNaN(Width) ? ActualWidth : Width;
        if (double.IsNaN(width) || width <= 0)
            width = _lastPersistedWidth;
        return width;
    }

    private void UpdateResizeThumbAlignment()
    {
        UpdateResizeThumbPlacement();
    }

    private void UpdateResizeThumbPlacement()
    {
        if (PanelBorder is null || ResizeThumb is null) return;

        if (IsVerticalPlacement())
        {
            var thumbColumn = _currentPlacement == ShortcutsPanelPlacement.Right ? 0 : 1;
            var panelColumn = thumbColumn == 0 ? 1 : 0;

            Grid.SetColumnSpan(PanelBorder, 1);
            Grid.SetColumn(PanelBorder, panelColumn);
            Grid.SetColumn(ResizeThumb, thumbColumn);
        }
        else
        {
            Grid.SetColumn(PanelBorder, 0);
            Grid.SetColumnSpan(PanelBorder, 2);
            Grid.SetColumn(ResizeThumb, 1);
        }
    }

    private void OnPanelBorderSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateResizeThumbPlacement();
    }

    private static bool IsScrollComponent(object? origin)
    {
        if (origin is not DependencyObject dep) return false;

        while (dep is not null)
        {
            if (dep is ScrollBar or ScrollViewer)
                return true;
            if (dep is Visual or Visual3D)
                dep = VisualTreeHelper.GetParent(dep);
            else
                return false;
        }

        return false;
    }

    private static double ClampWidth(double width)
    {
        if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
            return DefaultPanelWidth;
        return Clamp(width, MinPanelWidth, MaxPanelWidth);
    }

    private void UpdateAdaptiveSizing(double width)
    {
        if (width <= 0)
        {
            width = DefaultPanelWidth;
        }

        var clamped = Clamp(width, MinPanelWidth, MaxPanelWidth);
        var scale = Clamp(clamped / MaxPanelWidth, MinScale, 1.0);

        IconPixelSize = Math.Max(14, _baseIconSize * scale);
        GlyphFontSize = Math.Max(9, _baseGlyphSize * scale);
        MaxLabelWidth = Math.Max(36, _baseLabelWidth * scale);
        LabelFontSize = Math.Max(9, _baseLabelFontSize * scale);
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    private void SetDoubleField(ref double field, double value, string propertyName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return;
        if (Math.Abs(field - value) < 0.1) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void SetBoolField(ref bool field, bool value, string propertyName)
    {
        if (field == value) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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

    private async Task PersistAsync(Action<ShortcutsSettings> mutate)
    {
        if (_store is null) return;
        await _store.UpdateAsync(mutate);
        SettingsChangeNotifier.Default.Bump();
        RefreshFromSettings();
    }

    private async void OnAddShortcutClick(object sender, RoutedEventArgs e)
    {
        if (_store is null) return;

        var entry = new ShortcutEntry();
        if (!ShowShortcutEditor(entry, "Add Shortcut")) return;

        entry.DisplayName = string.IsNullOrWhiteSpace(entry.DisplayName) ? null : entry.DisplayName.Trim();
        entry.Target = entry.Target?.Trim() ?? string.Empty;
        entry.Arguments = string.IsNullOrWhiteSpace(entry.Arguments) ? null : entry.Arguments.Trim();
        entry.IconPath = string.IsNullOrWhiteSpace(entry.IconPath) ? null : entry.IconPath.Trim();

        if (string.IsNullOrWhiteSpace(entry.Target))
        {
            MessageBox.Show(Window.GetWindow(this), "Target path or URL is required.", "Add Shortcut", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        await PersistAsync(settings => settings.ShortcutEntries.Add(entry));
    }

    private async void OnRenameShortcutClick(object sender, RoutedEventArgs e)
    {
        if (_store is null) return;
        if (sender is not MenuItem menu) return;
        if (menu.CommandParameter is not ShortcutDisplayItem item) return;

        var window = new ShortcutRenameWindow
        {
            Owner = Window.GetWindow(this),
            DisplayName = item.Entry.DisplayName ?? item.DisplayName
        };

        if (window.ShowDialog() != true) return;

        var trimmed = window.DisplayName?.Trim();
        var newValue = string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;

        if (string.Equals(item.Entry.DisplayName, newValue, StringComparison.Ordinal))
        {
            return;
        }

        await PersistAsync(_ => item.Entry.DisplayName = newValue);
    }

    private async void OnDeleteShortcutClick(object sender, RoutedEventArgs e)
    {
        if (_store is null) return;
        if (sender is not MenuItem menu) return;
        if (menu.CommandParameter is not ShortcutDisplayItem item) return;

        var owner = Window.GetWindow(this);
        var message = $"Delete shortcut \"{item.DisplayName}\"?";
        var result = MessageBox.Show(owner, message, "Delete Shortcut", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        await PersistAsync(settings =>
        {
            var entries = settings.ShortcutEntries;
            if (entries.Contains(item.Entry))
            {
                entries.Remove(item.Entry);
            }
        });
    }

    private void OnShortcutPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = null;
        if (IsScrollComponent(e.OriginalSource)) return;
        _dragStartPoint = e.GetPosition(null);
    }

    private void OnShortcutPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (IsScrollComponent(e.OriginalSource))
        {
            _dragStartPoint = null;
            return;
        }
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (_dragStartPoint is null) return;
        var position = e.GetPosition(null);
        var start = _dragStartPoint.Value;

        if (Math.Abs(position.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _dragStartPoint = null;

        if (sender is Button btn && btn.DataContext is ShortcutDisplayItem item)
        {
            DragDrop.DoDragDrop(btn, item, DragDropEffects.Move);
        }
    }

    private void OnItemsPreviewDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(ShortcutDisplayItem)))
        {
            e.Effects = DragDropEffects.None;
            HideDropIndicator();
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        var (_, insertAfter, container) = ResolveDropTarget(e);
        ShowDropIndicator(container ?? AddButton, insertAfter);

        e.Handled = true;
    }

    private void OnItemsDragLeave(object sender, DragEventArgs e)
    {
        if (Items is null) return;
        if (!Items.IsMouseOver)
        {
            HideDropIndicator();
        }
    }

    private void ShowDropIndicator(FrameworkElement? element, bool insertAfter)
    {
        if (element is null)
        {
            HideDropIndicator();
            return;
        }

        if (!ReferenceEquals(_dropIndicatorElement, element))
        {
            HideDropIndicator();

            var layer = AdornerLayer.GetAdornerLayer(element);
            if (layer is null) return;

            _dropIndicatorElement = element;
            _dropIndicator = new DropInsertionAdorner(element);
            _dropIndicatorLayer = layer;
            layer.Add(_dropIndicator);
        }

        _dropIndicator?.Update(ItemsOrientation, insertAfter);
    }

    private void HideDropIndicator()
    {
        if (_dropIndicator is not null && _dropIndicatorLayer is not null)
        {
            _dropIndicatorLayer.Remove(_dropIndicator);
        }

        _dropIndicator = null;
        _dropIndicatorElement = null;
        _dropIndicatorLayer = null;
    }

    private FrameworkElement? GetContainerForItem(ShortcutDisplayItem item)
    {
        if (Items is null) return null;

        var container = Items.ItemContainerGenerator.ContainerFromItem(item);
        return container is null ? null : FindItemContainer(container);
    }

    private async void OnItemsDrop(object sender, DragEventArgs e)
    {
        if (_store is null) return;
        _dragStartPoint = null;
        e.Handled = true;

        if (!e.Data.GetDataPresent(typeof(ShortcutDisplayItem)))
        {
            HideDropIndicator();
            return;
        }

        if (e.Data.GetData(typeof(ShortcutDisplayItem)) is not ShortcutDisplayItem draggedItem)
        {
            HideDropIndicator();
            return;
        }

        var (targetItem, insertAfter, container) = ResolveDropTarget(e);
        HideDropIndicator();

        if (targetItem is null && DisplayItems.Count <= 1) return;

        await PersistAsync(settings =>
        {
            var entries = settings.ShortcutEntries;
            var fromIndex = entries.IndexOf(draggedItem.Entry);
            if (fromIndex < 0) return;

            int toIndex;
            if (targetItem is null)
            {
                toIndex = insertAfter ? entries.Count : entries.Count - 1;
            }
            else
            {
                toIndex = entries.IndexOf(targetItem.Entry);
                if (toIndex < 0) return;

                if (ReferenceEquals(targetItem.Entry, draggedItem.Entry))
                {
                    if (!insertAfter) return;
                    toIndex = Math.Min(entries.Count - 1, fromIndex + 1);
                }
                else if (insertAfter)
                {
                    toIndex++;
                }
            }

            toIndex = Math.Max(0, Math.Min(toIndex, entries.Count - 1));

            if (toIndex == fromIndex) return;

            entries.Move(fromIndex, toIndex);
        });
    }

    private (ShortcutDisplayItem? item, bool insertAfter, FrameworkElement? container) ResolveDropTarget(DragEventArgs e)
    {
        if (Items is null) return (null, true, null);

        if (DisplayItems.Count == 0)
        {
            return (null, false, AddButton);
        }

        var position = e.GetPosition(Items);
        FrameworkElement? lastContainer = null;

        foreach (var displayItem in DisplayItems)
        {
            var container = GetContainerForItem(displayItem);
            if (container is null) continue;

            lastContainer = container;
            var origin = container.TranslatePoint(new Point(0, 0), Items);

            if (ItemsOrientation == Orientation.Vertical)
            {
                var top = origin.Y;
                var bottom = top + container.ActualHeight;
                var midpoint = top + (container.ActualHeight / 2);

                if (position.Y < midpoint)
                {
                    return (displayItem, false, container);
                }

                if (position.Y < bottom)
                {
                    return (displayItem, true, container);
                }
            }
            else
            {
                var left = origin.X;
                var right = left + container.ActualWidth;
                var midpoint = left + (container.ActualWidth / 2);

                if (position.X < midpoint)
                {
                    return (displayItem, false, container);
                }

                if (position.X < right)
                {
                    return (displayItem, true, container);
                }
            }
        }

        var fallbackItem = DisplayItems.Last();
        var fallbackContainer = lastContainer ?? GetContainerForItem(fallbackItem);
        return (fallbackItem, true, fallbackContainer);
    }

    private FrameworkElement? FindItemContainer(DependencyObject? element)
    {
        while (element is not null && element != Items)
        {
            if (element is FrameworkElement fe && fe.DataContext is ShortcutDisplayItem)
            {
                return fe;
            }
            element = VisualTreeHelper.GetParent(element);
        }

        return null;
    }

    private bool ShowShortcutEditor(ShortcutEntry entry, string title)
    {
        var window = new ShortcutEntryEditorWindow
        {
            Owner = Window.GetWindow(this),
            DataContext = entry,
            Title = title
        };

        return window.ShowDialog() == true;
    }

    public sealed class ShortcutDisplayItem
    {
        public ShortcutEntry Entry { get; init; } = null!;
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
