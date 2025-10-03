using CommunityToolkit.Mvvm.ComponentModel;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Models;
using System.IO;

namespace RepoDash.App.ViewModels;

public class MainViewModel : ObservableObject
{
    private readonly GeneralSettings _settings;
    private readonly IRepoScanner _scanner;
    private readonly ILauncher _launcher;
    private readonly IGitService _git;
    private readonly IRemoteLinkProvider _links;

    public SearchBarViewModel SearchBar { get; }
    public RepoRootViewModel RepoRoot { get; }
    public SettingsMenuViewModel SettingsMenu { get; }
    public RepoGroupsViewModel RepoGroups { get; }

    public MainViewModel(
        GeneralSettings settings,
        IRepoScanner scanner,
        ILauncher launcher,
        IGitService git,
        IRemoteLinkProvider links)
    {
        _settings = settings;
        _scanner = scanner;
        _launcher = launcher;
        _git = git;
        _links = links;

        // Child VMs
        SearchBar = new SearchBarViewModel();
        RepoRoot = new RepoRootViewModel();
        SettingsMenu = new SettingsMenuViewModel();
        RepoGroups = new RepoGroupsViewModel(settings);

        // Initial values
        RepoRoot.RepoRootInput = _settings.RepoRoot;

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

        SettingsMenu.ResolveGitRepos = () => RepoGroups.GetAllRepoItems();
        SettingsMenu.OnFetchAll = async repos =>
        {
            await Task.WhenAll(repos.Select(r => _git.FetchAsync(r.Path, CancellationToken.None)));
            await Task.WhenAll(repos.Select(r => r.RefreshStatusAsync(CancellationToken.None)));
        };
        SettingsMenu.OnPullAll = async (repos, rebase) =>
        {
            foreach (var r in repos)
            {
                try { await _git.PullAsync(r.Path, rebase, CancellationToken.None); }
                catch { _launcher.OpenGitUi(r.Path); }
                await r.RefreshStatusAsync(CancellationToken.None);
            }
        };
    }

    public GeneralSettings Settings => _settings;

    // Called by code-behind on startup and by RepoRoot.OnLoad
    public async Task LoadCurrentRootAsync()
    {
        var root = RepoRoot.RepoRootInput?.Trim();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return;

        _settings.RepoRoot = root; // persistence saves later

        var scanned = await _scanner.ScanAsync(root, _settings.GroupingSegment, CancellationToken.None);

        // Project to item VMs and let RepoGroups own the collections & filtering
        var itemsByGroup = scanned
            .GroupBy(i => i.GroupKey, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(x => x.RepoName, StringComparer.OrdinalIgnoreCase)
                      .Select(r =>
                      {
                          var vm = new RepoItemViewModel(_launcher, _git, _links)
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
    }
}
