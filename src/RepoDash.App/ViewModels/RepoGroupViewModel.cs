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
    private readonly ISettingsStore<ColorSettings> _colorSettingsStore;
    private readonly Dictionary<RepoItemViewModel, PropertyChangedEventHandler> _subscriptions = new();
    private string _currentFilter = string.Empty;
    private string _internalKey = string.Empty;
    private bool _allowPinning = true;

    private static ColorSettings DefaultColorSettings { get; } = new();

    public RepoGroupViewModel(
        IReadOnlySettingsSource<GeneralSettings> settings,
        ISettingsStore<GeneralSettings> generalSettingsStore,
        ISettingsStore<ColorSettings> colorSettingsStore)
    {
        _settings = settings;
        _generalSettingsStore = generalSettingsStore;
        _colorSettingsStore = colorSettingsStore;

        _settings.PropertyChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(Settings));
            RaiseVisibilityState();
        };
        _generalSettingsStore.SettingsChanged += (_, __) => RaiseVisibilityState();
        _colorSettingsStore.SettingsChanged += (_, __) => RaiseColorState();
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

    public bool ShowVisibilityToggleOption => IsRecentSpecial || IsFrequentSpecial;

    public string ToggleVisibilityLabel
    {
        get
        {
            var groupDescriptor = string.IsNullOrWhiteSpace(GroupKey)
                ? "this group"
                : $"\"{GroupKey}\" group";

            if (IsRecentSpecial)
                return (_generalSettingsStore.Current.ShowRecent ? "Hide " : "Show ") + groupDescriptor;

            if (IsFrequentSpecial)
                return (_generalSettingsStore.Current.ShowFrequent ? "Hide " : "Show ") + groupDescriptor;

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
        foreach (var existing in _allItems)
            Detach(existing);

        _allItems.Clear();

        foreach (var item in items)
        {
            _allItems.Add(item);
            Attach(item);
        }

        ApplyFilter(_currentFilter);
    }

    public void ApplyFilter(string term)
    {
        _currentFilter = term ?? string.Empty;

        var result = new List<RepoItemViewModel>(_allItems.Count);
        if (string.IsNullOrWhiteSpace(_currentFilter))
        {
            foreach (var item in _allItems)
            {
                if (ExcludeBlacklisted && item.IsBlacklisted) continue;
                result.Add(item);
            }
        }
        else
        {
            foreach (var item in _allItems)
            {
                if (ExcludeBlacklisted && item.IsBlacklisted) continue;
                if (item.Name.Contains(_currentFilter, StringComparison.OrdinalIgnoreCase) ||
                    item.Path.Contains(_currentFilter, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(item);
                }
            }
        }

        var ordered = SortItems(result);
        Items = new ObservableCollection<RepoItemViewModel>(ordered);
    }

    public void Upsert(RepoItemViewModel item)
    {
        var existing = _allItems.FirstOrDefault(i => string.Equals(i.Path, item.Path, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            _allItems.Add(item);
            Attach(item);
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

        Detach(existing);
        _allItems.Remove(existing);
        ApplyFilter(_currentFilter);
        return true;
    }

    private void Attach(RepoItemViewModel item)
    {
        if (_subscriptions.ContainsKey(item)) return;

        PropertyChangedEventHandler handler = (_, e) =>
        {
            if (e.PropertyName == nameof(RepoItemViewModel.IsPinned) ||
                e.PropertyName == nameof(RepoItemViewModel.Name) ||
                e.PropertyName == nameof(RepoItemViewModel.IsBlacklisted))
            {
                ApplyFilter(_currentFilter);
            }
        };

        item.PropertyChanged += handler;
        _subscriptions[item] = handler;
    }

    private void Detach(RepoItemViewModel item)
    {
        if (_subscriptions.TryGetValue(item, out var handler))
        {
            item.PropertyChanged -= handler;
            _subscriptions.Remove(item);
        }
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

        await _generalSettingsStore.UpdateAsync(settings =>
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
