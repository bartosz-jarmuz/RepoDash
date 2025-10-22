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
        _store = services.GetRequiredService<ISettingsStore<ShortcutsSettings>>();
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
                Entry = e,
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
        _dragStartPoint = e.GetPosition(null);
    }

    private void OnShortcutPreviewMouseMove(object sender, MouseEventArgs e)
    {
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

    private sealed class DropInsertionAdorner : Adorner
    {
        private static readonly Pen IndicatorPen;
        private Orientation _orientation;
        private bool _insertAfter;

        static DropInsertionAdorner()
        {
            IndicatorPen = new Pen(Brushes.Black, 2);
            IndicatorPen.Freeze();
        }

        public DropInsertionAdorner(UIElement adornedElement) : base(adornedElement)
        {
            IsHitTestVisible = false;
        }

        public void Update(Orientation orientation, bool insertAfter)
        {
            _orientation = orientation;
            _insertAfter = insertAfter;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            var size = AdornedElement.RenderSize;
            if (size.Width <= 0 || size.Height <= 0) return;

            if (_orientation == Orientation.Vertical)
            {
                var y = _insertAfter ? size.Height : 0;
                drawingContext.DrawLine(IndicatorPen, new Point(0, y), new Point(size.Width, y));
            }
            else
            {
                var x = _insertAfter ? size.Width : 0;
                drawingContext.DrawLine(IndicatorPen, new Point(x, 0), new Point(x, size.Height));
            }
        }
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
