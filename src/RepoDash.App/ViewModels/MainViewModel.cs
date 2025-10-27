using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoDash.App.Abstractions;
using RepoDash.App.Services;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Caching;
using RepoDash.Core.Settings;
using RepoDash.Core.Usage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Threading;

namespace RepoDash.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IReadOnlySettingsSource<GeneralSettings> _generalSettings;
    private readonly ISettingsStore<GeneralSettings> _generalSettingsStore;
    private readonly ISettingsStore<ColorSettings> _colorSettingsStore;
    private readonly IReadOnlySettingsSource<ToolsPanelSettings> _toolsSettings;
    private readonly ISettingsStore<ToolsPanelSettings> _toolsSettingsStore;
    private readonly ILauncher _launcher;
    private readonly IGitService _git;
    private readonly IBranchProvider _branchProvider;
    private readonly IRemoteLinkProvider _links;
    private readonly RepoCacheService _cacheService;
    private readonly IRepoUsageService _usage;
    private readonly GitOperationCoordinator _gitCoordinator;
    private readonly RepoStatusRefreshService _statusRefresh;
    private readonly List<RepoItemViewModel> _detachedUsageItems = new();

    [ObservableProperty] private bool _focusSearchRequested;

    public SearchBarViewModel SearchBar { get; }
    public RepoRootViewModel RepoRoot { get; }
    public SettingsMenuViewModel SettingsMenu { get; }
    public RepoGroupsViewModel RepoGroups { get; }
    public GlobalGitOperationsMenuViewModel GlobalGitOperations { get; }
    public MainMenuViewModel MainMenu { get; }
    public StatusBarViewModel StatusBar { get; }

    public MainViewModel(
        IReadOnlySettingsSource<GeneralSettings> generalSettings,
        ISettingsStore<GeneralSettings> generalSettingsStore,
        IReadOnlySettingsSource<ToolsPanelSettings> toolsSettings,
        ISettingsStore<ToolsPanelSettings> toolsSettingsStore,
        ISettingsStore<ColorSettings> colorSettingsStore,
        ILauncher launcher,
        IGitService git,
        IBranchProvider branchProvider,
        IRemoteLinkProvider links,
        SettingsMenuViewModel settingsMenuVm,
        IApplicationLifetime applicationLifetime,
        IAboutWindowService aboutWindowService,
        RepoCacheService cacheService,
        RepoStatusRefreshService statusRefreshService,
        IRepoUsageService usage)
    {
        _generalSettings = generalSettings;
        _generalSettingsStore = generalSettingsStore;
        _toolsSettings = toolsSettings;
        _toolsSettingsStore = toolsSettingsStore;
        _colorSettingsStore = colorSettingsStore;
        _launcher = launcher;
        _git = git;
        _branchProvider = branchProvider;
        _links = links;
        _cacheService = cacheService;
        _statusRefresh = statusRefreshService;
        _usage = usage;

        // Child VMs
        SearchBar = new SearchBarViewModel();
        RepoRoot = new RepoRootViewModel();
        SettingsMenu = settingsMenuVm;
        RepoGroups = new RepoGroupsViewModel(_generalSettings, _generalSettingsStore, _toolsSettings, _toolsSettingsStore, _colorSettingsStore);
        GlobalGitOperations = new GlobalGitOperationsMenuViewModel();
        MainMenu = new MainMenuViewModel(SettingsMenu, GlobalGitOperations, applicationLifetime, aboutWindowService, _launcher);
        StatusBar = new StatusBarViewModel();
        StatusBar.SetLastRefresh(_statusRefresh.GetLastRefresh(_generalSettings.Current.RepoRoot));
        _gitCoordinator = new GitOperationCoordinator(_git, _launcher, StatusBar, Dispatch);
        _usage.Changed += OnUsageChanged;

        // Initial values
        RepoRoot.RepoRootInput = _generalSettings.Current.RepoRoot;

        // Wiring
        SearchBar.OnFilterChanged = term =>
            System.Windows.Application.Current?.Dispatcher?.InvokeAsync(
                () => RepoGroups.ApplyFilter(term ?? string.Empty),
                DispatcherPriority.Background);

        RepoRoot.OnBrowse = () =>
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                AutoUpgradeEnabled = true,
                ShowNewFolderButton = false,
                InitialDirectory = Directory.Exists(_generalSettings.Current.RepoRoot)
                    ? _generalSettings.Current.RepoRoot
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                RepoRoot.RepoRootInput = dlg.SelectedPath;
                _ = LoadCurrentRootAsync();
            }
        };

        RepoRoot.OnLoad = () => LoadCurrentRootAsync();

        GlobalGitOperations.ResolveGitRepos = () => RepoGroups.GetAllRepoItems();
        GlobalGitOperations.OnFetchAll = async repos =>
        {
            await _gitCoordinator.FetchAllAsync(repos, CancellationToken.None).ConfigureAwait(false);
            await RecordRefreshForCurrentRootAsync(CancellationToken.None).ConfigureAwait(false);
            Dispatch(UpdateStatusSummary);
        };
        GlobalGitOperations.OnPullAll = async (repos, rebase) =>
        {
            await _gitCoordinator.PullAllAsync(repos, rebase, CancellationToken.None).ConfigureAwait(false);
            await RecordRefreshForCurrentRootAsync(CancellationToken.None).ConfigureAwait(false);
            Dispatch(UpdateStatusSummary);
        };
        GlobalGitOperations.OnRefreshAll = async repos =>
        {
            _refreshCts?.Cancel();
            _refreshCts = new CancellationTokenSource();
            var ct = _refreshCts.Token;
            await RefreshStatusesAsync(repos, ct, force: true).ConfigureAwait(false);
            Dispatch(UpdateStatusSummary);
        };

        _generalSettings.PropertyChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(Settings));
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(ShowInTaskbar));
            StatusBar.SetLastRefresh(_statusRefresh.GetLastRefresh(_generalSettings.Current.RepoRoot));
            UpdateUsageGroups();
        };

        _toolsSettings.PropertyChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(ToolsSettings));
            UpdateUsageGroups();
        };
    }

    public string WindowTitle
    {
        get
        {
            var debugIndicator = "";
#if DEBUG
            debugIndicator = " - DEBUG";
#endif
            var path = Settings.RepoRoot;
            return string.IsNullOrWhiteSpace(path) ? "RepoDash"+debugIndicator : $"RepoDash{debugIndicator} - {path}";
        }
    }

    public GeneralSettings Settings => _generalSettings.Current;
    public ToolsPanelSettings ToolsSettings => _toolsSettings.Current;
    public bool ShowInTaskbar => !_generalSettings.Current.NeverShowInTaskbar;

    private CancellationTokenSource? _refreshCts;

    [RelayCommand]
    private void FocusSearch()
    {
        RequestFocusSearch();
    }

    [RelayCommand]
    public async Task LoadCurrentRootAsync()
    {
        var root = RepoRoot.RepoRootInput?.Trim();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return;

        // keep existing persistence semantics
        _generalSettings.Current.RepoRoot = root;
        StatusBar.SetLastRefresh(_statusRefresh.GetLastRefresh(root));

        _refreshCts?.Cancel();
        _refreshCts = new CancellationTokenSource();
        var ct = _refreshCts.Token;

        try
        {
            // 1) Fast initial paint from cache
            var cached = await _cacheService.LoadFromCacheAsync(root, ct).ConfigureAwait(false);
            Dispatch(() => ApplySnapshot(cached));

            // Full refresh streaming into the UI
            await _cacheService.RefreshAsync(
                root,
                groupingSegment: _generalSettings.Current.GroupingSegment,
                upsert: r => Dispatch(() => Upsert(r)),
                removeByRepoPath: p => Dispatch(() => RepoGroups.RemoveByRepoPath(p)),
                ct).ConfigureAwait(false);

            if (!ct.IsCancellationRequested)
            {
                var finalItems = RepoGroups
                    .GetAllRepoItems()
                    .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                await RefreshStatusesAsync(
                    finalItems,
                    ct,
                    force: false).ConfigureAwait(false);
                UpdateStatusSummary();
            }
        }
        catch (OperationCanceledException)
        {
            // expected on root change / shutdown
        }

        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(ShowInTaskbar));
        UpdateUsageGroups();
    }

    private void ApplySnapshot(IReadOnlyList<CachedRepo> snapshot)
    {
        var itemsByGroup = new Dictionary<string, List<RepoItemViewModel>>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in snapshot)
        {
            if (!itemsByGroup.TryGetValue(r.GroupKey, out var list))
            {
                list = [];
                itemsByGroup[r.GroupKey] = list;
            }
            var vm = MakeRepoItemVm(r);
            list.Add(vm);
        }

        RepoGroups.Load(itemsByGroup);
        RepoGroups.ApplyFilter(SearchBar.SearchText ?? string.Empty);
        UpdateStatusSummary();
        UpdateUsageGroups();
    }

    private void Upsert(CachedRepo r)
    {
        var vm = MakeRepoItemVm(r);
        RepoGroups.Upsert(r.GroupKey, vm);
        _ = Task.Run(async () =>
        {
            var result = await vm.RefreshStatusAsync(CancellationToken.None).ConfigureAwait(false);
            if (!result.Success && result.Error is null) return;
            Dispatch(UpdateStatusSummary);
        });
    }

    private RepoItemViewModel MakeRepoItemVm(CachedRepo r)
    {
        var vm = new RepoItemViewModel(_launcher, _git, _links, _branchProvider, _usage, _generalSettings)
        {
            Name = r.RepoName,
            Path = r.RepoPath,
            HasGit = r.HasGit,
            HasSolution = r.HasSolution,
            SolutionPath = r.SolutionPath
        };
        // use lightweight branch load first; heavy status can be triggered by user or virtualization
        _ = vm.EnsureBranchLoadedAsync();
        vm.RequestClearSearch = () => SearchBar.ClearCommand.Execute(null);
        vm.RefreshUsageFlags();

        return vm;
    }

    private void OnUsageChanged(object? sender, EventArgs e)
        => Dispatch(() => UpdateUsageGroups());

    private void UpdateUsageGroups()
    {
        DisposeDetachedUsageItems();

        var tools = _toolsSettings.Current;

        var recentLimit = Math.Max(0, tools.RecentItemsLimit);
        var recentEntries = recentLimit > 0 ? _usage.GetRecent(recentLimit) : Array.Empty<RepoUsageEntry>();
        var recentItems = BuildRecentItems(recentEntries);
        var showRecent = tools.ShowPanel && tools.ShowRecent && recentLimit > 0;
        RepoGroups.SetRecentItems(recentItems, showRecent);

        var frequentLimit = Math.Max(0, tools.FrequentItemsLimit);
        var frequentEntries = frequentLimit > 0 ? _usage.GetFrequent(frequentLimit) : Array.Empty<RepoUsageSummary>();
        var frequentItems = BuildFrequentItems(frequentEntries);
        var showFrequent = tools.ShowPanel && tools.ShowFrequent && frequentLimit > 0;
        RepoGroups.SetFrequentItems(frequentItems, showFrequent);

        RepoGroups.RefreshPinningSettings();
        RepoGroups.ApplyFilter(SearchBar.SearchText ?? string.Empty);
        UpdateStatusSummary();
    }

    private void UpdateStatusSummary()
    {
        var repos = RepoGroups.GetAllRepoItems(gitOnly: true);
        var total = repos.Count;
        var upToDate = 0;
        var behind = 0;
        var ahead = 0;
        var unknown = 0;

        foreach (var repo in repos)
        {
            switch (repo.SyncState)
            {
                case BranchSyncState.UpToDate:
                    upToDate++;
                    break;
                case BranchSyncState.Behind:
                    behind++;
                    break;
                case BranchSyncState.Ahead:
                    ahead++;
                    break;
                default:
                    unknown++;
                    break;
            }
        }

        StatusBar.UpdateSummary(total, upToDate, behind, ahead, unknown);
    }

    private void DisposeDetachedUsageItems()
    {
        if (_detachedUsageItems.Count == 0) return;
        foreach (var vm in _detachedUsageItems)
        {
            vm.Dispose();
        }
        _detachedUsageItems.Clear();
    }

    private IEnumerable<RepoItemViewModel> BuildRecentItems(IReadOnlyList<RepoUsageEntry> entries)
    {
        var result = new List<RepoItemViewModel>(entries.Count);
        foreach (var entry in entries)
        {
            var existing = RepoGroups.TryGetByPath(entry.RepoPath);
            if (existing is not null)
            {
                existing.ApplyUsageMetrics(entry.LastUsedUtc, entry.UsageCount);
                existing.RefreshUsageFlags();
                result.Add(existing);
                continue;
            }

            var vm = CreateDetachedUsageItem(entry);
            result.Add(vm);
        }

        return result;
    }

    private IEnumerable<RepoItemViewModel> BuildFrequentItems(IReadOnlyList<RepoUsageSummary> summaries)
    {
        var result = new List<RepoItemViewModel>(summaries.Count);
        foreach (var summary in summaries)
        {
            var existing = RepoGroups.TryGetByName(summary.RepoName);
            if (existing is null) continue;

            existing.ApplyUsageMetrics(summary.LastUsedUtc, summary.UsageCount);
            existing.RefreshUsageFlags();
            result.Add(existing);
        }
        return result;
    }

    private RepoItemViewModel CreateDetachedUsageItem(RepoUsageEntry entry)
    {
        var vm = new RepoItemViewModel(_launcher, _git, _links, _branchProvider, _usage, _generalSettings)
        {
            Name = entry.RepoName,
            Path = entry.RepoPath,
            HasGit = entry.HasGit,
            HasSolution = entry.HasSolution,
            SolutionPath = entry.SolutionPath
        };
        vm.RequestClearSearch = () => SearchBar.ClearCommand.Execute(null);
        vm.ApplyUsageMetrics(entry.LastUsedUtc, entry.UsageCount);
        vm.RefreshUsageFlags();
        _ = vm.EnsureBranchLoadedAsync();
        _detachedUsageItems.Add(vm);
        return vm;
    }

    private async Task<bool> RefreshStatusesAsync(
        IReadOnlyList<RepoItemViewModel> repos,
        CancellationToken ct,
        bool force,
        string description = "Refreshing repository statuses")
    {
        if (repos.Count == 0 || ct.IsCancellationRequested)
            return false;

        var root = _generalSettings.Current.RepoRoot;
        if (!_statusRefresh.ShouldRefresh(root, force))
            return false;

        await _gitCoordinator.RefreshStatusesAsync(repos, ct, description).ConfigureAwait(false);
        if (ct.IsCancellationRequested)
            return false;

        await RecordRefreshForCurrentRootAsync(ct).ConfigureAwait(false);
        return true;
    }

    private async Task RecordRefreshForCurrentRootAsync(CancellationToken ct)
    {
        var root = _generalSettings.Current.RepoRoot;
        if (string.IsNullOrWhiteSpace(root))
            return;

        var stamp = await _statusRefresh.MarkRefreshedAsync(root, ct).ConfigureAwait(false);
        Dispatch(() =>
        {
            StatusBar.SetLastRefresh(stamp);
            OnPropertyChanged(nameof(Settings));
        });
    }

    public async Task RefreshStatusesOnRestoreAsync()
    {
        var repos = RepoGroups.GetAllRepoItems(gitOnly: true);
        if (repos.Count == 0)
            return;

        if (!_statusRefresh.ShouldRefresh(_generalSettings.Current.RepoRoot, force: false))
            return;

        _refreshCts?.Cancel();
        _refreshCts = new CancellationTokenSource();
        var ct = _refreshCts.Token;

        try
        {
            var refreshed = await RefreshStatusesAsync(repos, ct, force: false).ConfigureAwait(false);
            if (refreshed && !ct.IsCancellationRequested)
            {
                Dispatch(UpdateStatusSummary);
            }
        }
        catch (OperationCanceledException)
        {
            // swallow; restore-triggered refresh was aborted
        }
    }

    public void RequestFocusSearch()
    {
        FocusSearchRequested = false;
        FocusSearchRequested = true;
        App.Current?.Dispatcher.InvokeAsync(() => FocusSearchRequested = false, DispatcherPriority.ApplicationIdle);
    }

    static void Dispatch(Action a)
    {
        if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() == true) a();
        else System.Windows.Application.Current?.Dispatcher?.Invoke(a);
    }
}
