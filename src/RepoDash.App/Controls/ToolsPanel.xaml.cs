namespace RepoDash.App.Controls;

using Microsoft.Extensions.DependencyInjection;
using RepoDash.App.Abstractions;
using RepoDash.App.ViewModels;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Settings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

public partial class ToolsPanel : UserControl, INotifyPropertyChanged
{
    private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;

    private bool _initialized;
    private IReadOnlySettingsSource<ToolsPanelSettings>? _settingsSource;
    private ISettingsStore<ToolsPanelSettings>? _settingsStore;
    private RepoGroupsViewModel? _repoGroups;
    private ObservableCollection<RepoGroupViewModel>? _specialGroups;

    private Point? _dragStartPoint;
    private FrameworkElement? _dropIndicatorElement;
    private DropInsertionAdorner? _dropIndicator;
    private AdornerLayer? _dropIndicatorLayer;

    public ToolsPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ToolDisplayItem> DisplayItems { get; } = new();

    public Orientation ItemsOrientation { get; private set; } = Orientation.Vertical;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        if (DesignerProperties.GetIsInDesignMode(this)) return;

        var services = App.Services;
        _settingsSource = services.GetRequiredService<IReadOnlySettingsSource<ToolsPanelSettings>>();
        _settingsStore = services.GetRequiredService<ISettingsStore<ToolsPanelSettings>>();

        _settingsSource.PropertyChanged += OnSettingsChanged;

        HookRepoGroups(DataContext as RepoGroupsViewModel);

        _initialized = true;
        RefreshFromSettings();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_settingsSource is not null)
        {
            _settingsSource.PropertyChanged -= OnSettingsChanged;
        }

        DetachSpecialGroups();
        _repoGroups = null;
        _settingsSource = null;
        _settingsStore = null;
        HideDropIndicator();
        _initialized = false;
    }

    private void OnDataContextChanged(object? sender, DependencyPropertyChangedEventArgs e)
        => HookRepoGroups(e.NewValue as RepoGroupsViewModel);

    private void HookRepoGroups(RepoGroupsViewModel? repoGroups)
    {
        if (ReferenceEquals(_repoGroups, repoGroups)) return;

        if (_repoGroups is not null)
        {
            _repoGroups.PropertyChanged -= OnRepoGroupsPropertyChanged;
        }

        DetachSpecialGroups();

        _repoGroups = repoGroups;

        if (_repoGroups is not null)
        {
            _repoGroups.PropertyChanged += OnRepoGroupsPropertyChanged;
            AttachSpecialGroups(_repoGroups.SpecialGroups);
        }

        RefreshFromSettings();
    }

    private void OnRepoGroupsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RepoGroupsViewModel.SpecialGroups))
        {
            AttachSpecialGroups(_repoGroups?.SpecialGroups);
            RefreshFromSettings();
        }
    }

    private void AttachSpecialGroups(ObservableCollection<RepoGroupViewModel>? collection)
    {
        if (ReferenceEquals(_specialGroups, collection)) return;

        DetachSpecialGroups();

        _specialGroups = collection;
        if (_specialGroups is not null)
        {
            _specialGroups.CollectionChanged += OnSpecialGroupsChanged;
        }
    }

    private void DetachSpecialGroups()
    {
        if (_specialGroups is not null)
        {
            _specialGroups.CollectionChanged -= OnSpecialGroupsChanged;
        }
        _specialGroups = null;
    }

    private void OnSpecialGroupsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => Dispatcher.InvokeAsync(RefreshFromSettings, System.Windows.Threading.DispatcherPriority.DataBind);

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IReadOnlySettingsSource<ToolsPanelSettings>.Current) || e.PropertyName is null)
        {
            RefreshFromSettings();
        }
    }

    private void RefreshFromSettings()
    {
        if (!_initialized) return;

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(RefreshFromSettings);
            return;
        }

        if (_settingsSource?.Current is null) return;

        ApplyPlacement(_settingsSource.Current.Placement);
        UpdateVisibility(_settingsSource.Current.ShowPanel);
        RebuildItems();
    }

    private void ApplyPlacement(ToolsPanelPlacement placement)
    {
        ItemsOrientation = placement switch
        {
            ToolsPanelPlacement.Top or ToolsPanelPlacement.Bottom => Orientation.Horizontal,
            _ => Orientation.Vertical
        };
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemsOrientation)));

        var dock = placement switch
        {
            ToolsPanelPlacement.Top => Dock.Top,
            ToolsPanelPlacement.Bottom => Dock.Bottom,
            ToolsPanelPlacement.Left => Dock.Left,
            ToolsPanelPlacement.Right => Dock.Right,
            _ => Dock.Left
        };

        DockPanel.SetDock(this, dock);
    }

    private void UpdateVisibility(bool isVisible)
        => Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;

    private void RebuildItems()
    {
        DisplayItems.Clear();

        if (_settingsSource?.Current is null) return;
        if (_repoGroups is null) return;

        var groups = _repoGroups.SpecialGroups;
        if (groups.Count == 0) return;

        var order = BuildOrderedKeys(_settingsSource.Current, groups);
        foreach (var key in order)
        {
            var group = groups.FirstOrDefault(g => KeyComparer.Equals(g.InternalKey, key));
            if (group is null) continue;

            DisplayItems.Add(new ToolDisplayItem
            {
                Key = key,
                Group = group
            });
        }
    }

    private static IReadOnlyList<string> BuildOrderedKeys(ToolsPanelSettings settings, IEnumerable<RepoGroupViewModel> groups)
    {
        var ordered = new List<string>();
        var seen = new HashSet<string>(KeyComparer);

        foreach (var entry in settings.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Key)) continue;
            if (seen.Add(entry.Key))
                ordered.Add(entry.Key);
        }

        foreach (var group in groups)
        {
            if (seen.Add(group.InternalKey))
                ordered.Add(group.InternalKey);
        }

        return ordered;
    }

    private void OnToolPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void OnToolPreviewMouseMove(object sender, MouseEventArgs e)
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

        if (sender is Border border && border.DataContext is ToolDisplayItem item)
        {
            DragDrop.DoDragDrop(border, item, DragDropEffects.Move);
        }
    }

    private void OnItemsPreviewDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(ToolDisplayItem)))
        {
            e.Effects = DragDropEffects.None;
            HideDropIndicator();
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        var (_, insertAfter, container) = ResolveDropTarget(e);

        if (container is not null)
        {
            ShowDropIndicator(container, insertAfter);
        }
        else
        {
            HideDropIndicator();
        }

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

    private async void OnItemsDrop(object sender, DragEventArgs e)
    {
        if (_settingsStore is null) return;
        _dragStartPoint = null;
        e.Handled = true;

        if (!e.Data.GetDataPresent(typeof(ToolDisplayItem)))
        {
            HideDropIndicator();
            return;
        }

        if (e.Data.GetData(typeof(ToolDisplayItem)) is not ToolDisplayItem draggedItem)
        {
            HideDropIndicator();
            return;
        }

        var (targetItem, insertAfter, container) = ResolveDropTarget(e);
        HideDropIndicator();

        if (targetItem is null && DisplayItems.Count <= 1 && container is null) return;

        await PersistAsync(settings =>
        {
            var entries = settings.Entries;
            var fromIndex = FindEntryIndex(entries, draggedItem.Key);
            if (fromIndex < 0) return;

            int toIndex;
            if (targetItem is null)
            {
                toIndex = insertAfter ? entries.Count - 1 : 0;
            }
            else
            {
                toIndex = FindEntryIndex(entries, targetItem.Key);
                if (toIndex < 0) return;

                if (KeyComparer.Equals(targetItem.Key, draggedItem.Key))
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

    private static int FindEntryIndex(IList<ToolsPanelEntry> entries, string key)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            if (KeyComparer.Equals(entries[i].Key, key))
                return i;
        }
        return -1;
    }

    private void ShowDropIndicator(FrameworkElement element, bool insertAfter)
    {
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

    private (ToolDisplayItem? item, bool insertAfter, FrameworkElement? container) ResolveDropTarget(DragEventArgs e)
    {
        if (Items is null) return (null, true, null);
        if (DisplayItems.Count == 0) return (null, false, null);

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

    private FrameworkElement? GetContainerForItem(ToolDisplayItem item)
    {
        if (Items is null) return null;
        var container = Items.ItemContainerGenerator.ContainerFromItem(item);
        return container as FrameworkElement;
    }

    private async Task PersistAsync(Action<ToolsPanelSettings> mutate)
    {
        if (_settingsStore is null) return;
        await _settingsStore.UpdateAsync(mutate);
        RefreshFromSettings();
    }

    public sealed class ToolDisplayItem
    {
        public string Key { get; init; } = string.Empty;
        public RepoGroupViewModel Group { get; init; } = null!;
    }
}

