using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Models;
using System.Collections.ObjectModel;
using System.IO;

namespace RepoDash.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly GeneralSettings _settings;
    private readonly IRepoScanner _scanner;
    private readonly ILauncher _launcher;

    public MainViewModel(GeneralSettings settings, IRepoScanner scanner, ILauncher launcher)
    {
        _settings = settings;
        _scanner = scanner;
        _launcher = launcher;

        RepoRootInput = _settings.RepoRoot;
    }

    public GeneralSettings Settings => _settings;

    [ObservableProperty]
    private string searchText = string.Empty;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [ObservableProperty]
    private string repoRootInput = string.Empty;

    public ObservableCollection<RepoGroupViewModel> Groups { get; } = new();

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    [RelayCommand]
    private void BrowseRepoRoot()
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select Repo Root",
            SelectedPath = Directory.Exists(RepoRootInput)
                ? RepoRootInput
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            RepoRootInput = dlg.SelectedPath;
            _ = LoadCurrentRootAsync();
        }
    }

    // Called from code-behind on Enter and on initial load.
    public async Task LoadCurrentRootAsync()
    {
        var root = RepoRootInput?.Trim();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return;

        _settings.RepoRoot = root; // persistence will save later

        var items = await _scanner.ScanAsync(root, _settings.GroupingSegment, CancellationToken.None);

        var grouped = items
            .GroupBy(i => i.GroupKey, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        Groups.Clear();
        foreach (var g in grouped)
        {
            var groupVm = new RepoGroupViewModel { GroupKey = g.Key };
            groupVm.SetItems(g.Select(r => new RepoItemViewModel
            {
                Name = r.RepoName,
                Path = r.RepoPath,
                HasGit = r.HasGit,
                HasSolution = r.HasSolution,
                SolutionPath = r.SolutionPath
            }));
            Groups.Add(groupVm);

        }

        ApplyFilter();
    }

    // Optional convenience: a command that invokes LoadCurrentRootAsync.
    [RelayCommand]
    private Task LoadRoot() => LoadCurrentRootAsync();

    private void ApplyFilter()
    {
        var term = SearchText ?? string.Empty;

        // Simple filter for now: rebuild each group's Items with matches.
        foreach (var g in Groups)
        {
            g.ApplyFilter(SearchText ?? string.Empty);
        }

    }
}
