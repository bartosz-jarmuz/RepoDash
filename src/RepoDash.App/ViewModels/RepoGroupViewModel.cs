using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RepoDash.App.Abstractions;
using RepoDash.Core.Settings;

namespace RepoDash.App.ViewModels;

public partial class RepoGroupViewModel : ObservableObject
{
    private readonly List<RepoItemViewModel> _allItems = [];
    private readonly IReadOnlySettingsSource<GeneralSettings> _settings;
    private readonly Dictionary<RepoItemViewModel, PropertyChangedEventHandler> _subscriptions = new();
    private string _currentFilter = string.Empty;

    public RepoGroupViewModel(IReadOnlySettingsSource<GeneralSettings> settings)
    {
        _settings = settings;
        _settings.PropertyChanged += (_, __) => OnPropertyChanged(nameof(Settings));
    }

    [ObservableProperty] private string _groupKey = string.Empty;
    [ObservableProperty] private ObservableCollection<RepoItemViewModel> _items = [];
    [ObservableProperty] private int _sortOrder;

    public string InternalKey { get; set; } = string.Empty;
    public bool IsSpecial { get; set; }
    public bool ExcludeBlacklisted { get; set; }
    public Comparison<RepoItemViewModel> SortComparison { get; set; } = DefaultComparison;
    private bool _allowPinning = true;

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
}
