using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoDash.App.Abstractions;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Caching;
using RepoDash.Core.Settings;
using System.IO;
using System.Windows.Threading;

namespace RepoDash.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IReadOnlySettingsSource<GeneralSettings> _generalSettings;
    private readonly ISettingsStore<GeneralSettings> _generalStore;
    private readonly IRepoScanner _scanner; // still injected (used by RepoCacheService)
    private readonly ILauncher _launcher;
    private readonly IGitService _git;
    private readonly IBranchProvider _branchProvider;
    private readonly IRemoteLinkProvider _links;
    private readonly RepoCacheService _cacheService;

    [ObservableProperty] private bool _focusSearchRequested;

    public SearchBarViewModel SearchBar { get; }
    public RepoRootViewModel RepoRoot { get; }
    public SettingsMenuViewModel SettingsMenu { get; }
    public RepoGroupsViewModel RepoGroups { get; }
    public GlobalGitOperationsMenuViewModel GlobalGitOperations { get; }

    public MainViewModel(
        IReadOnlySettingsSource<GeneralSettings> generalSettings,
        ISettingsStore<GeneralSettings> generalStore,
        IRepoScanner scanner,
        ILauncher launcher,
        IGitService git,
        IBranchProvider branchProvider,
        IRemoteLinkProvider links,
        SettingsMenuViewModel settingsMenuVm,
        RepoCacheService cacheService)
    {
        _generalSettings = generalSettings;
        _generalStore = generalStore;
        _scanner = scanner;
        _launcher = launcher;
        _git = git;
        _branchProvider = branchProvider;
        _links = links;
        _cacheService = cacheService;

        // Child VMs
        SearchBar = new SearchBarViewModel();
        RepoRoot = new RepoRootViewModel();
        SettingsMenu = settingsMenuVm;
        RepoGroups = new RepoGroupsViewModel(_generalSettings);
        GlobalGitOperations = new GlobalGitOperationsMenuViewModel();

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
            await Task.WhenAll(repos.Select(r => _git.FetchAsync(r.Path, CancellationToken.None)));
            await Task.WhenAll(repos.Select(r => r.RefreshStatusAsync(CancellationToken.None)));
        };
        GlobalGitOperations.OnPullAll = async (repos, rebase) =>
        {
            foreach (var r in repos)
            {
                try { await _git.PullAsync(r.Path, rebase, CancellationToken.None); }
                catch { _launcher.OpenGitUi(r.Path); }
                await r.RefreshStatusAsync(CancellationToken.None);
            }
        };

        _generalSettings.PropertyChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(Settings));
            OnPropertyChanged(nameof(WindowTitle));
        };
    }

    public string WindowTitle
    {
        get
        {
            var path = Settings.RepoRoot;
            return string.IsNullOrWhiteSpace(path) ? "RepoDash" : $"RepoDash - {path}";
        }
    }

    public GeneralSettings Settings => _generalSettings.Current;

    private CancellationTokenSource? _refreshCts;

    [RelayCommand]
    public async Task LoadCurrentRootAsync()
    {
        var root = RepoRoot.RepoRootInput?.Trim();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return;

        // Keep your persistence semantics
        _generalSettings.Current.RepoRoot = root;

        _refreshCts?.Cancel();
        _refreshCts = new CancellationTokenSource();
        var ct = _refreshCts.Token;

        // 1) paint from cache immediately
        var cached = await _cacheService.LoadFromCacheAsync(root, ct);
        ApplySnapshot(cached);

        // 2) background refresh with streaming upserts
        _ = Task.Run(async () =>
        {
            try
            {
                await _cacheService.RefreshAsync(
                    root,
                    groupingSegment: _generalSettings.Current.GroupingSegment,
                    upsert: repo => Dispatch(() => Upsert(repo)),
                    removeByRepoPath: repoPath => Dispatch(() => RepoGroups.RemoveByRepoPath(repoPath)),
                    ct);
            }
            catch
            {
                // best effort
            }
        }, ct);

        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(WindowTitle));
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
    }

    private void Upsert(CachedRepo r)
    {
        var vm = MakeRepoItemVm(r);
        RepoGroups.Upsert(r.GroupKey, vm);
    }

    private RepoItemViewModel MakeRepoItemVm(CachedRepo r)
    {
        var vm = new RepoItemViewModel(_launcher, _git, _links, _branchProvider)
        {
            Name = r.RepoName,
            Path = r.RepoPath,
            HasGit = r.HasGit,
            HasSolution = r.HasSolution,
            SolutionPath = r.SolutionPath
        };
        // use lightweight branch load first; heavy status can be triggered by user or virtualization
        _ = vm.EnsureBranchLoadedAsync();
        return vm;
    }

    public void RequestFocusSearch()
    {
        FocusSearchRequested = true;
        App.Current?.Dispatcher.InvokeAsync(() => FocusSearchRequested = false, DispatcherPriority.ApplicationIdle);
    }

    static void Dispatch(Action a)
    {
        if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() == true) a();
        else System.Windows.Application.Current?.Dispatcher?.Invoke(a);
    }
}
