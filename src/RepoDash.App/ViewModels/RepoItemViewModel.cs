using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoDash.Core.Abstractions;

namespace RepoDash.App.ViewModels;

public partial class RepoItemViewModel : ObservableObject
{
    private readonly ILauncher _launcher;
    private readonly IGitService _git;
    private readonly IRemoteLinkProvider _links;

    public RepoItemViewModel(ILauncher launcher, IGitService git, IRemoteLinkProvider links)
    { _launcher = launcher; _git = git; _links = links; }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _path = string.Empty;
    [ObservableProperty] private bool _hasSolution;
    [ObservableProperty] private string? _solutionPath;
    [ObservableProperty] private bool _hasGit;

    // Status
    [ObservableProperty] private string _branch = "";
    [ObservableProperty] private BranchSyncState _syncState = BranchSyncState.Unknown;
    [ObservableProperty] private bool _isDirty;

    public async Task RefreshStatusAsync(CancellationToken ct)
    {
        if (!_hasGit) { Branch = ""; SyncState = BranchSyncState.Unknown; IsDirty = false; return; }
        var status = await _git.GetStatusAsync(_path, ct);
        Branch = status.CurrentBranch;
        SyncState = status.SyncState;
        IsDirty = status.IsDirty;
    }

    // Actions
    [RelayCommand]
    private void Launch()
    {
        if (_hasSolution && !string.IsNullOrEmpty(_solutionPath))
            _launcher.OpenSolution(_solutionPath!);
        else
            _launcher.OpenFolder(_path);
    }

    [RelayCommand]
    private void Browse() => _launcher.OpenFolder(_path);

    [RelayCommand]
    private async Task OpenRemoteAsync()
    {
        if (!_hasGit) return;
        var url = await _git.GetRemoteUrlAsync(_path, CancellationToken.None);
        if (url is null) return;
        if (_links.TryGetProjectLinks(url, out var repo, out _))
            _launcher.OpenUrl(repo!);
    }

    [RelayCommand]
    private async Task OpenPipelinesAsync()
    {
        if (!_hasGit) return;
        var url = await _git.GetRemoteUrlAsync(_path, CancellationToken.None);
        if (url is null) return;
        if (_links.TryGetProjectLinks(url, out _, out var pipelines))
            _launcher.OpenUrl(pipelines!);
    }

    [RelayCommand]
    private void OpenGitUi() => _launcher.OpenGitUi(_path);

    [RelayCommand]
    private void CopyName() => System.Windows.Clipboard.SetText(_name);

    [RelayCommand]
    private void CopyPath() => System.Windows.Clipboard.SetText(_path);
}
