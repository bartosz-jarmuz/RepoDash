using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoDash.App.Abstractions;
using RepoDash.App.Services;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Settings;

namespace RepoDash.App.ViewModels;

public partial class RepoGroupViewModel : ObservableObject
{
    private const string RecentKey = "__special_recent";
    private const string FrequentKey = "__special_frequent";

    private readonly List<RepoItemViewModel> _allItems = [];
    private readonly IReadOnlySettingsSource<GeneralSettings> _settings;
    private readonly ISettingsStore<GeneralSettings> _generalSettingsStore;
    private readonly IReadOnlySettingsSource<ToolsPanelSettings> _toolsSettings;
    private readonly ISettingsStore<ToolsPanelSettings> _toolsSettingsStore;
    private readonly ISettingsStore<ColorSettings> _colorSettingsStore;
    private readonly IDisposable _layoutRefreshSubscription;
    private readonly Dictionary<RepoItemViewModel, PropertyChangedEventHandler> _subscriptions = new();
    private string _currentFilter = string.Empty;
    private string _internalKey = string.Empty;
    private bool _allowPinning = true;

    private static ColorSettings DefaultColorSettings { get; } = new();

    public RepoGroupViewModel(
        IReadOnlySettingsSource<GeneralSettings> settings,
        ISettingsStore<GeneralSettings> generalSettingsStore,
        IReadOnlySettingsSource<ToolsPanelSettings> toolsSettings,
        ISettingsStore<ToolsPanelSettings> toolsSettingsStore,
        ISettingsStore<ColorSettings> colorSettingsStore)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _generalSettingsStore = generalSettingsStore ?? throw new ArgumentNullException(nameof(generalSettingsStore));
        _toolsSettings = toolsSettings ?? throw new ArgumentNullException(nameof(toolsSettings));
        _toolsSettingsStore = toolsSettingsStore ?? throw new ArgumentNullException(nameof(toolsSettingsStore));
        _colorSettingsStore = colorSettingsStore ?? throw new ArgumentNullException(nameof(colorSettingsStore));

        _settings.PropertyChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(Settings));
            RaiseVisibilityState();
            LayoutRefreshCoordinator.Default.Refresh();
        };
        _generalSettingsStore.SettingsChanged += (_, __) =>
        {
            RaiseVisibilityState();
            LayoutRefreshCoordinator.Default.Refresh();
        };
        _toolsSettings.PropertyChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(ToolsSettings));
            RaiseVisibilityState();
            LayoutRefreshCoordinator.Default.Refresh();
        };
        _toolsSettingsStore.SettingsChanged += (_, __) =>
        {
            RaiseVisibilityState();
            LayoutRefreshCoordinator.Default.Refresh();
        };
        _colorSettingsStore.SettingsChanged += (_, __) => RaiseColorState();

        _layoutRefreshSubscription = LayoutRefreshCoordinator.Default.Register(NotifyLayoutChanged);
        NotifyLayoutChanged();
    }

    [ObservableProperty] private string _groupKey = string.Empty;
    [ObservableProperty] private ObservableCollection<RepoItemViewModel> _items = [];
    [ObservableProperty] private int _sortOrder;

    public string InternalKey
    {
        get => _internalKey;
        set
        {
            if (SetProperty(ref _internalKey, value))
            {
                RaiseVisibilityState();
                RaiseColorState();
            }
        }
    }

    public bool IsSpecial { get; set; }
    public bool ExcludeBlacklisted { get; set; }
    public Comparison<RepoItemViewModel> SortComparison { get; set; } = DefaultComparison;

    public bool AllowPinning
    {
        get => _allowPinning;
        set
        {
            if (_allowPinning == value) return;
            _allowPinning = value;
            ApplyFilter(_currentFilter);
        }
    }

    public GeneralSettings Settings => _settings.Current;

    public ToolsPanelSettings ToolsSettings => _toolsSettings.Current;

    public int PanelWidth => Settings.GroupPanelWidth;

    public int VisibleItemCount
    {
        get
        {
            if (IsRecentSpecial)
                return Math.Max(1, ToolsSettings.RecentListVisibleCount);
            if (IsFrequentSpecial)
                return Math.Max(1, ToolsSettings.FrequentListVisibleCount);
            return Math.Max(1, Settings.ListItemVisibleCount);
        }
    }

    public bool ShowVisibilityToggleOption => IsRecentSpecial || IsFrequentSpecial;

    public string ToggleVisibilityLabel
    {
        get
        {
            var groupDescriptor = string.IsNullOrWhiteSpace(GroupKey)
                ? "this group"
                : $"\"{GroupKey}\" group";

            if (IsRecentSpecial)
                return (_toolsSettingsStore.Current.ShowRecent ? "Hide " : "Show ") + groupDescriptor;

            if (IsFrequentSpecial)
                return (_toolsSettingsStore.Current.ShowFrequent ? "Hide " : "Show ") + groupDescriptor;

            return string.Empty;
        }
    }

    public bool CanResetColor
    {
        get
        {
            if (IsRecentSpecial)
            {
                return !string.Equals(
                    _colorSettingsStore.Current.RecentGroupColor,
                    DefaultColorSettings.RecentGroupColor,
                    StringComparison.OrdinalIgnoreCase);
            }

            if (IsFrequentSpecial)
            {
                return !string.Equals(
                    _colorSettingsStore.Current.FrequentGroupColor,
                    DefaultColorSettings.FrequentGroupColor,
                    StringComparison.OrdinalIgnoreCase);
            }

            var displayName = GroupKey ?? string.Empty;
            return _colorSettingsStore.Current.GroupColorOverrides
                .Any(o => string.Equals(o.GroupName, displayName, StringComparison.OrdinalIgnoreCase));
        }
    }

    private bool IsRecentSpecial => string.Equals(InternalKey, RecentKey, StringComparison.OrdinalIgnoreCase);
    private bool IsFrequentSpecial => string.Equals(InternalKey, FrequentKey, StringComparison.OrdinalIgnoreCase);

    partial void OnGroupKeyChanged(string value)
    {
        RaiseVisibilityState();
        RaiseColorState();
    }

    public void SetItems(IEnumerable<RepoItemViewModel> items)
    {
        UnsubscribeAll();
        _allItems.Clear();

        foreach (var item in items)
        {
            _allItems.Add(item);
            Subscribe(item);
        }

        ApplyFilter(_currentFilter);
    }

    public void ApplyFilter(string filter)
    {
        _currentFilter = filter ?? string.Empty;

        IEnumerable<RepoItemViewModel> source = _allItems;

        if (ExcludeBlacklisted)
        {
            source = source.Where(item => !item.IsBlacklisted);
        }

        if (!string.IsNullOrWhiteSpace(_currentFilter))
        {
            source = source.Where(item =>
                item.Name.Contains(_currentFilter, StringComparison.OrdinalIgnoreCase) ||
                item.Path.Contains(_currentFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (IsSpecial && SortComparison is not null)
        {
            source = source.OrderBy(item => item, Comparer<RepoItemViewModel>.Create(SortComparison));
        }

        var ordered = SortItems(source);
        Items = new ObservableCollection<RepoItemViewModel>(ordered);
    }

    public void Upsert(RepoItemViewModel item)
    {
        var existing = _allItems.FirstOrDefault(i => string.Equals(i.Path, item.Path, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            _allItems.Add(item);
            Subscribe(item);
        }
        else
        {
            existing.Name = item.Name;
            existing.HasGit = item.HasGit;
            existing.HasSolution = item.HasSolution;
            existing.SolutionPath = item.SolutionPath;
            existing.RefreshUsageFlags();
        }

        ApplyFilter(_currentFilter);
    }

    public bool RemoveByPath(string repoPath)
    {
        var existing = _allItems.FirstOrDefault(i => string.Equals(i.Path, repoPath, StringComparison.OrdinalIgnoreCase));
        if (existing is null) return false;

        Unsubscribe(existing);
        _allItems.Remove(existing);
        ApplyFilter(_currentFilter);
        return true;
    }

    public void SetItemsFromSpecial(IEnumerable<RepoItemViewModel> items)
    {
        _allItems.Clear();
        foreach (var item in items)
        {
            _allItems.Add(item);
        }

        ApplyFilter(_currentFilter);
    }

    public void SetItems(IEnumerable<RepoItemViewModel> items, bool allowPinning)
    {
        AllowPinning = allowPinning;
        SetItems(items);
    }

    private void Subscribe(RepoItemViewModel item)
    {
        if (_subscriptions.ContainsKey(item)) return;

        PropertyChangedEventHandler handler = (_, e) =>
        {
            if (e.PropertyName is nameof(RepoItemViewModel.IsPinned)
                or nameof(RepoItemViewModel.Name)
                or nameof(RepoItemViewModel.IsBlacklisted))
            {
                ApplyFilter(_currentFilter);
            }
        };

        item.PropertyChanged += handler;
        _subscriptions[item] = handler;
    }

    private void Unsubscribe(RepoItemViewModel item)
    {
        if (_subscriptions.TryGetValue(item, out var handler))
        {
            item.PropertyChanged -= handler;
            _subscriptions.Remove(item);
        }
    }

    private void UnsubscribeAll()
    {
        foreach (var kvp in _subscriptions)
        {
            kvp.Key.PropertyChanged -= kvp.Value;
        }
        _subscriptions.Clear();
    }

    private List<RepoItemViewModel> SortItems(IEnumerable<RepoItemViewModel> items)
    {
        var list = items.ToList();
        var comparer = SortComparison ?? DefaultComparison;
        if (AllowPinning)
        {
            list.Sort((a, b) =>
            {
                if (a.IsPinned != b.IsPinned)
                    return a.IsPinned ? -1 : 1;
                return comparer(a, b);
            });
        }
        else
        {
            var indexMap = _allItems
                .Select((item, idx) => new { item, idx })
                .ToDictionary(x => x.item, x => x.idx);

            list.Sort((a, b) =>
            {
                var idxA = indexMap.TryGetValue(a, out var ia) ? ia : int.MaxValue;
                var idxB = indexMap.TryGetValue(b, out var ib) ? ib : int.MaxValue;
                return idxA.CompareTo(idxB);
            });
        }
        return list;
    }

    private static int DefaultComparison(RepoItemViewModel a, RepoItemViewModel b)
    {
        return StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
    }

    [RelayCommand]
    private async Task ChangeColorAsync()
    {
        var picker = new ColorDialog
        {
            AllowFullOpen = true,
            AnyColor = true,
            FullOpen = true
        };

        var initial = GetConfiguredColor();
        if (initial is System.Drawing.Color preset)
        {
            picker.Color = preset;
        }

        if (picker.ShowDialog() != DialogResult.OK)
            return;

        var selected = picker.Color;
        var hex = $"#{selected.A:X2}{selected.R:X2}{selected.G:X2}{selected.B:X2}";

        await _colorSettingsStore.UpdateAsync(settings =>
        {
            if (IsRecentSpecial)
            {
                settings.RecentGroupColor = hex;
            }
            else if (IsFrequentSpecial)
            {
                settings.FrequentGroupColor = hex;
            }
            else
            {
                var displayName = GroupKey ?? string.Empty;
                var existing = settings.GroupColorOverrides
                    .FirstOrDefault(o => string.Equals(o.GroupName, displayName, StringComparison.OrdinalIgnoreCase));

                if (existing is null)
                {
                    settings.GroupColorOverrides.Add(new GroupColorOverride
                    {
                        GroupName = displayName,
                        ColorCode = hex
                    });
                }
                else
                {
                    existing.ColorCode = hex;
                }
            }
        });

        SettingsChangeNotifier.Default.Bump();
        RaiseColorState();
    }

    [RelayCommand(CanExecute = nameof(ShowVisibilityToggleOption))]
    private async Task ToggleVisibilityAsync()
    {
        if (!ShowVisibilityToggleOption)
            return;

        await _toolsSettingsStore.UpdateAsync(settings =>
        {
            if (IsRecentSpecial)
                settings.ShowRecent = !settings.ShowRecent;
            else if (IsFrequentSpecial)
                settings.ShowFrequent = !settings.ShowFrequent;
        });

        RaiseVisibilityState();
    }

    [RelayCommand]
    private async Task ResetColorAsync()
    {
        if (IsRecentSpecial)
        {
            await _colorSettingsStore.UpdateAsync(settings =>
            {
                settings.RecentGroupColor = DefaultColorSettings.RecentGroupColor;
            });
        }
        else if (IsFrequentSpecial)
        {
            await _colorSettingsStore.UpdateAsync(settings =>
            {
                settings.FrequentGroupColor = DefaultColorSettings.FrequentGroupColor;
            });
        }
        else
        {
            var displayName = GroupKey ?? string.Empty;
            await _colorSettingsStore.UpdateAsync(settings =>
            {
                var existing = settings.GroupColorOverrides
                    .FirstOrDefault(o => string.Equals(o.GroupName, displayName, StringComparison.OrdinalIgnoreCase));
                if (existing is not null)
                    settings.GroupColorOverrides.Remove(existing);
            });
        }

        SettingsChangeNotifier.Default.Bump();
        RaiseColorState();
    }

    private System.Drawing.Color? GetConfiguredColor()
    {
        if (IsRecentSpecial)
            return ParseColor(_colorSettingsStore.Current.RecentGroupColor);
        if (IsFrequentSpecial)
            return ParseColor(_colorSettingsStore.Current.FrequentGroupColor);

        var displayName = GroupKey ?? string.Empty;
        var overrideHex = _colorSettingsStore.Current.GroupColorOverrides
            .FirstOrDefault(o => string.Equals(o.GroupName, displayName, StringComparison.OrdinalIgnoreCase))
            ?.ColorCode;

        return ParseColor(overrideHex);
    }

    private static System.Drawing.Color? ParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return null;

        try
        {
            var mediaColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!;
            return System.Drawing.Color.FromArgb(mediaColor.A, mediaColor.R, mediaColor.G, mediaColor.B);
        }
        catch
        {
            return null;
        }
    }

    internal void NotifyLayoutChanged()
    {
        OnPropertyChanged(nameof(PanelWidth));
        OnPropertyChanged(nameof(VisibleItemCount));
    }

    private void RaiseVisibilityState()
    {
        OnPropertyChanged(nameof(ShowVisibilityToggleOption));
        OnPropertyChanged(nameof(ToggleVisibilityLabel));
        ToggleVisibilityCommand?.NotifyCanExecuteChanged();
    }

    private void RaiseColorState()
    {
        OnPropertyChanged(nameof(CanResetColor));
    }
}

