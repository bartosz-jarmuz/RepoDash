using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoDash.App.State;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Models;
using RepoDash.Core.Settings;

namespace RepoDash.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly AppState _appState;
    private readonly IRepoCacheService _cacheService;
    private readonly IRepoDiscoveryService _discoveryService;
    private readonly IUsageTracker _usageTracker;
    private readonly IRepoLauncher _launcher;
    private readonly IColorizer _colorizer;

    private IReadOnlyDictionary<RepoIdentifier, RepoUsageEntry> _usage = new Dictionary<RepoIdentifier, RepoUsageEntry>();
    private List<RepoSnapshot> _snapshots = new();

    public ObservableCollection<RepoGroupViewModel> Groups { get; } = new();
    public ObservableCollection<RepoItemViewModel> Suggestions { get; } = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _repoRoot = string.Empty;

    [ObservableProperty]
    private int _listItemHeight;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _suggestionsVisible;

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand<RepoItemViewModel?> LaunchCommand { get; }

    public MainViewModel(
        AppState appState,
        IRepoCacheService cacheService,
        IRepoDiscoveryService discoveryService,
        IUsageTracker usageTracker,
        IRepoLauncher launcher,
        IColorizer colorizer)
    {
        _appState = appState;
        _cacheService = cacheService;
        _discoveryService = discoveryService;
        _usageTracker = usageTracker;
        _launcher = launcher;
        _colorizer = colorizer;

        _repoRoot = appState.RepositoriesSettings.RepoRoot;
        _listItemHeight = appState.GeneralSettings.ListItemHeight;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        LaunchCommand = new AsyncRelayCommand<RepoItemViewModel?>(LaunchAsync);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _usage = await _usageTracker.LoadAsync(cancellationToken).ConfigureAwait(false);
        await LoadFromCacheAsync(cancellationToken).ConfigureAwait(false);
        _ = RefreshAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnRepoRootChanged(string value)
    {
        _appState.RepositoriesSettings.RepoRoot = value;
    }

    private async Task RefreshAsync()
    {
        if (string.IsNullOrWhiteSpace(RepoRoot))
        {
            return;
        }

        try
        {
            IsBusy = true;
            var descriptors = await _discoveryService.ScanAsync(RepoRoot, CancellationToken.None).ConfigureAwait(false);
            var snapshots = descriptors.Select(CreateSnapshot).ToList();
            _snapshots = snapshots;
            await _cacheService.SaveAsync(new RepoCacheSnapshot(RepoRoot, descriptors, DateTimeOffset.UtcNow)).ConfigureAwait(false);
            ApplyFilter();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LaunchAsync(RepoItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        await _launcher.LaunchAsync(item.Snapshot.Descriptor).ConfigureAwait(false);
        await _usageTracker.IncrementLaunchAsync(item.Snapshot.Identifier).ConfigureAwait(false);
    }

    private async Task LoadFromCacheAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(RepoRoot))
        {
            return;
        }

        var cache = await _cacheService.LoadAsync(RepoRoot, cancellationToken).ConfigureAwait(false);
        if (cache is null)
        {
            return;
        }

        _snapshots = cache.Repositories.Select(CreateSnapshot).ToList();
        ApplyFilter();
    }

    private RepoSnapshot CreateSnapshot(RepoDescriptor descriptor)
    {
        if (!_usage.TryGetValue(descriptor.Identifier, out var usageEntry))
        {
            usageEntry = RepoUsageEntry.Empty(descriptor.Identifier);
        }

        return new RepoSnapshot(descriptor, RepoStatus.Unknown, usageEntry);
    }

    private void ApplyFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _snapshots
            : _snapshots.Where(s => s.Descriptor.MatchesSearch(SearchText)).ToList();

        UpdateGroups(filtered);
        UpdateSuggestions(filtered);
    }

    private void UpdateGroups(IReadOnlyList<RepoSnapshot> filtered)
    {
        var groupWidth = _appState.GeneralSettings.GroupWidth;
        var existing = Groups.ToDictionary(g => g.Header, StringComparer.OrdinalIgnoreCase);

        foreach (var key in existing.Keys.Except(filtered.Select(s => s.Descriptor.GroupKey), StringComparer.OrdinalIgnoreCase).ToList())
        {
            var obsolete = Groups.FirstOrDefault(g => g.Header.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (obsolete is not null)
            {
                Groups.Remove(obsolete);
            }
        }

        foreach (var grouping in filtered.GroupBy(s => s.Descriptor.GroupKey).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!existing.TryGetValue(grouping.Key, out var viewModel))
            {
                viewModel = new RepoGroupViewModel(grouping.Key, groupWidth);
                Groups.Add(viewModel);
                existing[grouping.Key] = viewModel;
            }

            SyncGroupItems(viewModel, grouping.OrderBy(x => x.Identifier.Name, StringComparer.OrdinalIgnoreCase));
        }
    }

    private void SyncGroupItems(RepoGroupViewModel group, IEnumerable<RepoSnapshot> snapshots)
    {
        group.Items.Clear();
        foreach (var snapshot in snapshots)
        {
            var itemVm = new RepoItemViewModel(snapshot);
            if (_colorizer.TryGetColor(snapshot.Identifier.Name, out var swatch))
            {
                itemVm.ApplyAccent($"#{swatch.R:X2}{swatch.G:X2}{swatch.B:X2}");
            }

            group.Items.Add(itemVm);
        }
    }

    private void UpdateSuggestions(IReadOnlyList<RepoSnapshot> filtered)
    {
        Suggestions.Clear();
        var threshold = _appState.GeneralSettings.AutocompleteThreshold;
        if (threshold <= 0 || filtered.Count == 0 || filtered.Count > threshold)
        {
            SuggestionsVisible = false;
            return;
        }

        foreach (var snapshot in filtered.Take(threshold))
        {
            Suggestions.Add(new RepoItemViewModel(snapshot));
        }

        SuggestionsVisible = true;
    }
}
