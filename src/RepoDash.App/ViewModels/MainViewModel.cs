using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoDash.App.Abstractions;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Settings;
using System.IO;
using System.Windows.Threading;

namespace RepoDash.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IReadOnlySettingsSource<GeneralSettings> _generalSettings;
    private readonly ISettingsStore<GeneralSettings> _generalStore;
    private readonly IRepoScanner _scanner;
    private readonly ILauncher _launcher;
    private readonly IGitService _git;
    private readonly IBranchProvider _branchProvider;
    private readonly IRemoteLinkProvider _links;

    public SearchBarViewModel SearchBar { get; }
    public RepoRootViewModel RepoRoot { get; }
    public SettingsMenuViewModel SettingsMenu { get; }
    public RepoGroupsViewModel RepoGroups { get; }
    public GlobalGitOperationsMenuViewModel GlobalGitOperations { get; }

    [ObservableProperty]
    private bool _focusSearchRequested;

    [RelayCommand]
    private void ApplyUiSettings()
    {
        Settings.ListItemVisibleCount = Math.Max(1, Settings.ListItemVisibleCount);
        Settings.GroupPanelWidth = Math.Max(1, Settings.GroupPanelWidth);
        Settings.GroupingSegment = Math.Max(1, Settings.GroupingSegment);
    }

    [RelayCommand]
    private void FocusSearch()
    {
        RequestFocusSearch();
    }

    public MainViewModel(
        IReadOnlySettingsSource<GeneralSettings> generalSettings,
        ISettingsStore<GeneralSettings> generalStore,
        IRepoScanner scanner,
        ILauncher launcher,
        IGitService git,
        IBranchProvider branchProvider,
        IRemoteLinkProvider links,
        SettingsMenuViewModel settingsMenuVm)
    {
        _generalSettings = generalSettings;
        _generalStore = generalStore;
        _scanner = scanner;
        _launcher = launcher;
        _git = git;
        _branchProvider = branchProvider;
        _links = links;

        // Child VMs
        SearchBar = new SearchBarViewModel();
        RepoRoot = new RepoRootViewModel();
        SettingsMenu = settingsMenuVm;
        RepoGroups = new RepoGroupsViewModel(_generalSettings);
        GlobalGitOperations = new GlobalGitOperationsMenuViewModel();

        // Initial values
        RepoRoot.RepoRootInput = _generalSettings.Current.RepoRoot;

        // Wiring
        SearchBar.OnFilterChanged = term => RepoGroups.ApplyFilter(term ?? string.Empty);

        RepoRoot.OnBrowse = () =>
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Select Repo Root",
                SelectedPath = Directory.Exists(RepoRoot.RepoRootInput)
                    ? RepoRoot.RepoRootInput
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (dlg.ShowDialog() == DialogResult.OK)
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

    // Called by code-behind on startup and by RepoRoot.OnLoad
    public async Task LoadCurrentRootAsync()
    {
        var root = RepoRoot.RepoRootInput?.Trim();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return;

        _generalSettings.Current.RepoRoot = root; // persistence saves later

        var scanned = await _scanner.ScanAsync(root, _generalSettings.Current.GroupingSegment, CancellationToken.None);

        // Project to item VMs and let RepoGroups own the collections & filtering
        var itemsByGroup = scanned
            .GroupBy(i => i.GroupKey, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(x => x.RepoName, StringComparer.OrdinalIgnoreCase)
                      .Select(r =>
                      {
                          var vm = new RepoItemViewModel(_launcher, _git, _links, _branchProvider)
                          {
                              Name = r.RepoName,
                              Path = r.RepoPath,
                              HasGit = r.HasGit,
                              HasSolution = r.HasSolution,
                              SolutionPath = r.SolutionPath
                          };
                          _ = vm.RefreshStatusAsync(CancellationToken.None);
                          return vm;
                      }).ToList());

        RepoGroups.Load(itemsByGroup);
        // apply current filter
        RepoGroups.ApplyFilter(SearchBar.SearchText ?? string.Empty);

        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(WindowTitle));
    }

    public void RequestFocusSearch()
    {
        FocusSearchRequested = true;
        App.Current?.Dispatcher.InvokeAsync(() => FocusSearchRequested = false, DispatcherPriority.ApplicationIdle);
    }
}
