using System.Collections.ObjectModel;
using RepoDash.App.Abstractions;
using RepoDash.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RepoDash.App.ViewModels;

public partial class RepoGroupsViewModel : ObservableObject
{
    private const string RecentKey = "__special_recent";
    private const string FrequentKey = "__special_frequent";
    private readonly IReadOnlySettingsSource<GeneralSettings> _settings;
    private readonly Dictionary<string, RepoGroupViewModel> _groupsByKey = new(StringComparer.OrdinalIgnoreCase);
    private string _currentFilter = string.Empty;

    public RepoGroupsViewModel(IReadOnlySettingsSource<GeneralSettings> settings)
    {
        _settings = settings;
        _settings.PropertyChanged += (_, __) => OnPropertyChanged(nameof(Settings));
        Groups = new ObservableCollection<RepoGroupViewModel>();
    }

    [ObservableProperty] private ObservableCollection<RepoGroupViewModel> _groups;

    public GeneralSettings Settings => _settings.Current;

    public void Load(IDictionary<string, List<RepoItemViewModel>> itemsByGroup)
    {
        var standardKeys = new HashSet<string>(itemsByGroup.Keys, StringComparer.OrdinalIgnoreCase);

        // Remove groups no longer present (standard only)
        foreach (var key in _groupsByKey.Keys.ToList())
        {
            if (IsSpecialKey(key)) continue;
            if (standardKeys.Contains(key)) continue;
            if (_groupsByKey.TryGetValue(key, out var obsolete))
            {
                _groupsByKey.Remove(key);
                Groups.Remove(obsolete);
            }
        }

        foreach (var kv in itemsByGroup)
        {
            var group = EnsureStandardGroup(kv.Key);
            group.SetItems(kv.Value);
            group.ApplyFilter(_currentFilter);
        }

        ReorderGroups();
    }

    public void ApplyFilter(string term)
    {
        _currentFilter = term ?? string.Empty;
        foreach (var group in Groups)
        {
            group.ApplyFilter(_currentFilter);
        }
    }

    public void Upsert(string groupKey, RepoItemViewModel item)
    {
        var group = EnsureStandardGroup(groupKey);
        group.Upsert(item);
        group.ApplyFilter(_currentFilter);
        ReorderGroups();
    }

    public void RemoveByRepoPath(string repoPath)
    {
        foreach (var g in Groups)
        {
            if (g.RemoveByPath(repoPath))
                break;
        }
    }

    public IReadOnlyList<RepoItemViewModel> GetAllRepoItems(bool gitOnly = true)
    {
        IEnumerable<RepoItemViewModel> items = Groups
            .Where(g => !g.IsSpecial)
            .SelectMany(g => g.Items);

        if (gitOnly)
            items = items.Where(i => i.HasGit);

        return items.ToList();
    }

    public RepoItemViewModel? TryGetByPath(string repoPath)
    {
        foreach (var group in Groups)
        {
            var match = group.Items.FirstOrDefault(i => string.Equals(i.Path, repoPath, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }
        return null;
    }

    public RepoItemViewModel? TryGetByName(string repoName)
    {
        foreach (var group in Groups.Where(g => !g.IsSpecial))
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
                Groups.Remove(existing);
                ReorderGroups();
            }
            return;
        }

        var group = EnsureSpecialGroup(key, displayName, sortOrder, comparison);
        group.SetItems(materialized);
        group.ApplyFilter(_currentFilter);
        ReorderGroups();
    }

    private RepoGroupViewModel EnsureStandardGroup(string groupKey)
    {
        if (_groupsByKey.TryGetValue(groupKey, out var existing))
        {
            if (!Groups.Contains(existing))
                Groups.Add(existing);

            existing.GroupKey = groupKey;
            existing.SortOrder = 10;
            existing.IsSpecial = false;
            existing.ExcludeBlacklisted = false;
            return existing;
        }

        var group = new RepoGroupViewModel(_settings)
        {
            InternalKey = groupKey,
            GroupKey = groupKey,
            SortOrder = 10,
            IsSpecial = false,
            ExcludeBlacklisted = false
        };

        _groupsByKey[groupKey] = group;
        Groups.Add(group);
        return group;
    }

    private RepoGroupViewModel EnsureSpecialGroup(string key, string displayName, int sortOrder, Comparison<RepoItemViewModel> comparison)
    {
        if (_groupsByKey.TryGetValue(key, out var existing))
        {
            if (!Groups.Contains(existing))
                Groups.Add(existing);

            existing.GroupKey = displayName;
            existing.SortOrder = sortOrder;
            existing.IsSpecial = true;
            existing.ExcludeBlacklisted = true;
            existing.SortComparison = comparison;
            return existing;
        }

        var group = new RepoGroupViewModel(_settings)
        {
            InternalKey = key,
            GroupKey = displayName,
            SortOrder = sortOrder,
            IsSpecial = true,
            ExcludeBlacklisted = true,
            SortComparison = comparison
        };

        _groupsByKey[key] = group;
        Groups.Add(group);
        return group;
    }

    private static bool IsSpecialKey(string key) => string.Equals(key, RecentKey, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, FrequentKey, StringComparison.OrdinalIgnoreCase);

    private void ReorderGroups()
    {
        var ordered = _groupsByKey.Values
            .Where(g => Groups.Contains(g))
            .OrderBy(g => g.SortOrder)
            .ThenBy(g => g.GroupKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Groups = new ObservableCollection<RepoGroupViewModel>(ordered);
    }

    private static readonly Comparison<RepoItemViewModel> RecentComparison = (a, b) =>
    {
        if (a.IsBlacklisted != b.IsBlacklisted)
            return a.IsBlacklisted ? 1 : -1;
        if (a.IsPinned != b.IsPinned)
            return a.IsPinned ? -1 : 1;

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
        if (a.IsPinned != b.IsPinned)
            return a.IsPinned ? -1 : 1;

        var usageCompare = b.UsageCount.CompareTo(a.UsageCount);
        if (usageCompare != 0) return usageCompare;

        var aTime = a.LastUsedUtc ?? DateTimeOffset.MinValue;
        var bTime = b.LastUsedUtc ?? DateTimeOffset.MinValue;
        var timeCompare = bTime.CompareTo(aTime);
        if (timeCompare != 0) return timeCompare;

        return StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
    };
}
