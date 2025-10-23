using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using RepoDash.App.Abstractions;
using RepoDash.App.Services;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RepoDash.App.ViewModels;

public partial class RepoGroupsViewModel : ObservableObject
{
    private const string RecentKey = "__special_recent";
    private const string FrequentKey = "__special_frequent";

    private readonly IReadOnlySettingsSource<GeneralSettings> _settings;
    private readonly ISettingsStore<GeneralSettings> _generalSettingsStore;
    private readonly IReadOnlySettingsSource<ToolsPanelSettings> _toolsSettings;
    private readonly ISettingsStore<ToolsPanelSettings> _toolsSettingsStore;
    private readonly ISettingsStore<ColorSettings> _colorSettingsStore;
    private readonly Dictionary<string, RepoGroupViewModel> _groupsByKey = new(StringComparer.OrdinalIgnoreCase);
    private string _currentFilter = string.Empty;

    public RepoGroupsViewModel(
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
            LayoutRefreshCoordinator.Default.Refresh();
        };
        _toolsSettings.PropertyChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(ToolsSettings));
            LayoutRefreshCoordinator.Default.Refresh();
        };

        Groups = new ObservableCollection<RepoGroupViewModel>();
        SpecialGroups = new ObservableCollection<RepoGroupViewModel>();
    }

    [ObservableProperty] private ObservableCollection<RepoGroupViewModel> _groups;
    [ObservableProperty] private ObservableCollection<RepoGroupViewModel> _specialGroups;

    public GeneralSettings Settings => _settings.Current;
    public ToolsPanelSettings ToolsSettings => _toolsSettings.Current;

    public void Load(IDictionary<string, List<RepoItemViewModel>> itemsByGroup)
    {
        var standardKeys = new HashSet<string>(itemsByGroup.Keys, StringComparer.OrdinalIgnoreCase);

        foreach (var key in _groupsByKey.Keys.ToList())
        {
            if (IsSpecialKey(key)) continue;
            if (standardKeys.Contains(key)) continue;
            if (_groupsByKey.TryGetValue(key, out var obsolete))
            {
                _groupsByKey.Remove(key);
                Groups.Remove(obsolete);
                RemoveFromCustomOrder(key);
            }
        }

        foreach (var kv in itemsByGroup)
        {
            var group = EnsureStandardGroup(kv.Key);
            group.SetItems(kv.Value);
            group.ApplyFilter(_currentFilter);
            group.NotifyLayoutChanged();
        }

        ReorderGroups();
        NotifyLayoutChangedAll();
    }

    public void ApplyFilter(string term)
    {
        _currentFilter = term ?? string.Empty;
        foreach (var group in EnumerateAllGroups())
        {
            group.ApplyFilter(_currentFilter);
        }
    }

    public void Upsert(string groupKey, RepoItemViewModel item)
    {
        var group = EnsureStandardGroup(groupKey);
        group.Upsert(item);
        group.ApplyFilter(_currentFilter);
        group.NotifyLayoutChanged();
        ReorderGroups();
        NotifyLayoutChangedAll();
    }

    public void RemoveByRepoPath(string repoPath)
    {
        foreach (var g in EnumerateAllGroups())
        {
            if (g.RemoveByPath(repoPath))
                break;
        }
    }

    public async Task MoveGroupAsync(string sourceKey, string? targetKey, bool insertAfter)
    {
        if (string.IsNullOrWhiteSpace(sourceKey))
            return;

        if (!_groupsByKey.TryGetValue(sourceKey, out var sourceGroup))
            return;

        if (sourceGroup.IsSpecial || Groups.Count <= 1)
            return;

        if (!string.IsNullOrWhiteSpace(targetKey) &&
            string.Equals(sourceKey, targetKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var currentKeys = Groups
            .Select(g => g.InternalKey)
            .ToList();

        var fromIndex = currentKeys.FindIndex(key =>
            string.Equals(key, sourceKey, StringComparison.OrdinalIgnoreCase));
        if (fromIndex < 0)
            return;

        currentKeys.RemoveAt(fromIndex);

        int insertionIndex;
        if (string.IsNullOrWhiteSpace(targetKey))
        {
            insertionIndex = insertAfter ? currentKeys.Count : 0;
        }
        else
        {
            var targetIndex = currentKeys.FindIndex(key =>
                string.Equals(key, targetKey, StringComparison.OrdinalIgnoreCase));
            insertionIndex = targetIndex < 0
                ? currentKeys.Count
                : insertAfter ? targetIndex + 1 : targetIndex;
        }

        insertionIndex = Math.Max(0, Math.Min(insertionIndex, currentKeys.Count));
        currentKeys.Insert(insertionIndex, sourceKey);

        UpdateCustomOrder(currentKeys);
        ReorderGroups();
        NotifyLayoutChangedAll();

        await _generalSettingsStore.UpdateAsync(settings =>
        {
            settings.CustomGroupOrder.Clear();
            foreach (var key in currentKeys)
            {
                settings.CustomGroupOrder.Add(key);
            }
        });
    }

    public IReadOnlyList<RepoItemViewModel> GetAllRepoItems(bool gitOnly = true)
    {
        IEnumerable<RepoItemViewModel> items = Groups.SelectMany(g => g.Items);

        if (gitOnly)
            items = items.Where(i => i.HasGit);

        return items.ToList();
    }

    public RepoItemViewModel? TryGetByPath(string repoPath)
    {
        foreach (var group in EnumerateAllGroups())
        {
            var match = group.Items.FirstOrDefault(i => string.Equals(i.Path, repoPath, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }
        return null;
    }

    public RepoItemViewModel? TryGetByName(string repoName)
    {
        foreach (var group in Groups)
        {
            var match = group.Items.FirstOrDefault(i => string.Equals(i.Name, repoName, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }
        return null;
    }

    public void SetRecentItems(IEnumerable<RepoItemViewModel> items, bool isVisible)
        => SetSpecialGroup(RecentKey, "Recent", items, isVisible, sortOrder: 0, RecentComparison);

    public void SetFrequentItems(IEnumerable<RepoItemViewModel> items, bool isVisible)
        => SetSpecialGroup(FrequentKey, "Frequently Used", items, isVisible, sortOrder: 1, FrequentComparison);

    public void RefreshPinningSettings()
    {
        foreach (var kv in _groupsByKey)
        {
            kv.Value.AllowPinning = GetPinningSettingForGroup(kv.Key);
        }
    }

    private IEnumerable<RepoGroupViewModel> EnumerateAllGroups()
    {
        foreach (var group in Groups)
            yield return group;
        foreach (var special in SpecialGroups)
            yield return special;
    }

    private void SetSpecialGroup(
        string key,
        string displayName,
        IEnumerable<RepoItemViewModel> items,
        bool isVisible,
        int sortOrder,
        Comparison<RepoItemViewModel> comparison)
    {
        var materialized = items?.ToList() ?? new List<RepoItemViewModel>();
        if (!isVisible || materialized.Count == 0)
        {
            if (_groupsByKey.TryGetValue(key, out var existing))
            {
                _groupsByKey.Remove(key);
                SpecialGroups.Remove(existing);
                ReorderSpecialGroups();
                NotifyLayoutChangedAll();
            }

            return;
        }

        var group = EnsureSpecialGroup(key, displayName, sortOrder, comparison);
        group.AllowPinning = GetPinningSettingForGroup(key);
        group.SetItems(materialized);
        group.ApplyFilter(_currentFilter);
        group.NotifyLayoutChanged();
        ReorderSpecialGroups();
        NotifyLayoutChangedAll();
    }

    private RepoGroupViewModel EnsureStandardGroup(string groupKey)
    {
        if (_groupsByKey.TryGetValue(groupKey, out var existing))
        {
            if (SpecialGroups.Contains(existing))
                SpecialGroups.Remove(existing);
            if (!Groups.Contains(existing))
                Groups.Add(existing);

            existing.InternalKey = groupKey;
            existing.GroupKey = groupKey;
            existing.SortOrder = 10;
            existing.IsSpecial = false;
            existing.ExcludeBlacklisted = false;
            existing.AllowPinning = Settings.PinningAppliesToAutomaticGroupings;
            existing.NotifyLayoutChanged();
            return existing;
        }

        var group = new RepoGroupViewModel(_settings, _generalSettingsStore, _toolsSettings, _toolsSettingsStore, _colorSettingsStore)
        {
            InternalKey = groupKey,
            GroupKey = groupKey,
            SortOrder = 10,
            IsSpecial = false,
            ExcludeBlacklisted = false,
            AllowPinning = Settings.PinningAppliesToAutomaticGroupings
        };

        _groupsByKey[groupKey] = group;
        Groups.Add(group);
        return group;
    }

    private RepoGroupViewModel EnsureSpecialGroup(
        string key,
        string displayName,
        int sortOrder,
        Comparison<RepoItemViewModel> comparison)
    {
        if (_groupsByKey.TryGetValue(key, out var existing))
        {
            if (Groups.Contains(existing))
                Groups.Remove(existing);
            if (!SpecialGroups.Contains(existing))
                SpecialGroups.Add(existing);

            existing.InternalKey = key;
            existing.GroupKey = displayName;
            existing.SortOrder = sortOrder;
            existing.IsSpecial = true;
            existing.ExcludeBlacklisted = true;
            existing.SortComparison = comparison;
            existing.AllowPinning = GetPinningSettingForGroup(key);
            existing.NotifyLayoutChanged();
            return existing;
        }

        var group = new RepoGroupViewModel(_settings, _generalSettingsStore, _toolsSettings, _toolsSettingsStore, _colorSettingsStore)
        {
            InternalKey = key,
            GroupKey = displayName,
            SortOrder = sortOrder,
            IsSpecial = true,
            ExcludeBlacklisted = true,
            SortComparison = comparison,
            AllowPinning = GetPinningSettingForGroup(key)
        };

        _groupsByKey[key] = group;
        SpecialGroups.Add(group);
        return group;
    }

    private static bool IsSpecialKey(string key) =>
        string.Equals(key, RecentKey, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, FrequentKey, StringComparison.OrdinalIgnoreCase);

    private void ReorderGroups()
    {
        var orderMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var customOrder = Settings.CustomGroupOrder;
        for (var i = 0; i < customOrder.Count; i++)
        {
            orderMap[customOrder[i]] = i;
        }

        var ordered = Groups
            .OrderBy(g => g.SortOrder)
            .ThenBy(g => orderMap.TryGetValue(g.InternalKey, out var rank) ? rank : int.MaxValue)
            .ThenBy(g => g.GroupKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Groups = new ObservableCollection<RepoGroupViewModel>(ordered);
    }

    private void ReorderSpecialGroups()
    {
        var ordered = SpecialGroups
            .OrderBy(g => g.SortOrder)
            .ThenBy(g => g.GroupKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        SpecialGroups = new ObservableCollection<RepoGroupViewModel>(ordered);
    }

    private static readonly Comparison<RepoItemViewModel> RecentComparison = (a, b) =>
    {
        if (a.IsBlacklisted != b.IsBlacklisted)
            return a.IsBlacklisted ? 1 : -1;

        var aTime = a.LastUsedUtc ?? DateTimeOffset.MinValue;
        var bTime = b.LastUsedUtc ?? DateTimeOffset.MinValue;
        var cmp = bTime.CompareTo(aTime);
        if (cmp != 0) return cmp;

        return StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
    };

    private static readonly Comparison<RepoItemViewModel> FrequentComparison = (a, b) =>
    {
        if (a.IsBlacklisted != b.IsBlacklisted)
            return a.IsBlacklisted ? 1 : -1;

        var usageCompare = b.UsageCount.CompareTo(a.UsageCount);
        if (usageCompare != 0) return usageCompare;

        var aTime = a.LastUsedUtc ?? DateTimeOffset.MinValue;
        var bTime = b.LastUsedUtc ?? DateTimeOffset.MinValue;
        var timeCompare = bTime.CompareTo(aTime);
        if (timeCompare != 0) return timeCompare;

        return StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
    };

    private void UpdateCustomOrder(IList<string> orderedKeys)
    {
        var target = Settings.CustomGroupOrder;
        target.Clear();
        foreach (var key in orderedKeys)
        {
            target.Add(key);
        }
    }

    private void RemoveFromCustomOrder(string key)
    {
        var target = Settings.CustomGroupOrder;
        for (var i = target.Count - 1; i >= 0; i--)
        {
            if (string.Equals(target[i], key, StringComparison.OrdinalIgnoreCase))
            {
                target.RemoveAt(i);
            }
        }
    }

    private void NotifyLayoutChangedAll()
        => LayoutRefreshCoordinator.Default.Refresh();

    private bool GetPinningSettingForGroup(string key)
    {
        if (string.Equals(key, RecentKey, StringComparison.OrdinalIgnoreCase))
            return ToolsSettings.PinningAppliesToRecent;
        if (string.Equals(key, FrequentKey, StringComparison.OrdinalIgnoreCase))
            return ToolsSettings.PinningAppliesToFrequent;
        return Settings.PinningAppliesToAutomaticGroupings;
    }
}

