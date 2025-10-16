using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoDash.Core.Abstractions;

namespace RepoDash.App.ViewModels;

public partial class RepoItemViewModel : ObservableObject, IDisposable
{
    private readonly ILauncher _launcher;
    private readonly IGitService _git;
    private readonly IRemoteLinkProvider _links;
    private readonly IBranchProvider _branchProvider;

    private CancellationTokenSource? _branchCts;
    private CancellationTokenSource? _statusCts;

    public RepoItemViewModel(ILauncher launcher, IGitService git, IRemoteLinkProvider links, IBranchProvider branchProvider)
    {
        _launcher = launcher;
        _git = git;
        _links = links;
        _branchProvider = branchProvider;

        // react to HEAD changes (LightweightBranchProvider raises this)
        _branchProvider.BranchChanged += OnBranchProviderChanged;
    }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _path = string.Empty;
    [ObservableProperty] private bool _hasSolution;
    [ObservableProperty] private string? _solutionPath;
    [ObservableProperty] private bool _hasGit;

    // Status
    [ObservableProperty] private string? _currentBranch;                // from IBranchProvider (primary)
    [ObservableProperty] private BranchSyncState _syncState;            // from IGitService (heavy)
    [ObservableProperty] private bool _isDirty;                         // from IGitService (heavy)

    public Action? RequestClearSearch { get; set; }

    // Convenience flags for UI bindings
    public bool CanOpenSolution => _hasSolution && !string.IsNullOrWhiteSpace(_solutionPath);
    partial void OnHasSolutionChanged(bool value) => OnPropertyChanged(nameof(CanOpenSolution));
    partial void OnSolutionPathChanged(string? value) => OnPropertyChanged(nameof(CanOpenSolution));

    /// <summary>
    /// Fast/lazy branch load that uses IBranchProvider (HEAD parse).
    /// Call this when the item becomes visible/materialized.
    /// </summary>
    public async Task EnsureBranchLoadedAsync()
    {
        if (!_hasGit || string.IsNullOrWhiteSpace(_path))
        {
            CurrentBranch = null;
            return;
        }

        // instant cache hit if available
        CurrentBranch = _branchProvider.TryGetCached(_path) ?? CurrentBranch;

        _branchCts?.Cancel();
        _branchCts = new CancellationTokenSource();
        var ct = _branchCts.Token;

        try
        {
            var branch = await _branchProvider.GetCurrentBranchAsync(_path, ct).ConfigureAwait(false);
            if (!ct.IsCancellationRequested)
            {
                Ui(() => CurrentBranch = branch ?? CurrentBranch ?? "—");
            }
        }
        catch
        {
            if (!ct.IsCancellationRequested)
            {
                Ui(() => CurrentBranch = CurrentBranch ?? "—");
            }
        }
    }

    /// <summary>
    /// Heavy status refresh (dirty/sync). Uses IGitService.
    /// Branch label remains owned by IBranchProvider; however, if it is empty we fallback to IGitService status branch.
    /// </summary>
    public async Task RefreshStatusAsync(CancellationToken externalCt)
    {
        if (!_hasGit || string.IsNullOrWhiteSpace(_path))
        {
            SyncState = BranchSyncState.Unknown;
            IsDirty = false;
            return;
        }

        // Kick off branch load in parallel (cheap)
        _ = EnsureBranchLoadedAsync();

        // Debounce/cancel previous heavy request
        _statusCts?.Cancel();
        _statusCts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        var ct = _statusCts.Token;

        try
        {
            var status = await _git.GetStatusAsync(_path, ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return;

            Ui(() =>
            {
                // Only override branch if provider returned nothing
                if (string.IsNullOrWhiteSpace(CurrentBranch) && !string.IsNullOrWhiteSpace(status.CurrentBranch))
                    CurrentBranch = status.CurrentBranch;

                SyncState = status.SyncState;
                IsDirty = status.IsDirty;
            });
        }
        catch
        {
            if (!ct.IsCancellationRequested)
            {
                Ui(() =>
                {
                    // Leave CurrentBranch as-is (provider-owned)
                    SyncState = BranchSyncState.Unknown;
                    IsDirty = false;
                });
            }
        }
    }

    private void OnBranchProviderChanged(object? sender, string changedRepoPath)
    {
        if (!PathsEqual(changedRepoPath, _path)) return;
        _ = EnsureBranchLoadedAsync(); // fire&forget; this is lightweight
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(System.IO.Path.GetFullPath(a).TrimEnd(System.IO.Path.DirectorySeparatorChar),
                      System.IO.Path.GetFullPath(b).TrimEnd(System.IO.Path.DirectorySeparatorChar),
                      StringComparison.OrdinalIgnoreCase);

    private void Ui(Action action)
    {
        var app = System.Windows.Application.Current;
        var disp = app?.Dispatcher;
        if (disp is null || disp.CheckAccess()) action();
        else disp.Invoke(action);
    }

    [RelayCommand]
    private void Launch()
    {
        if (CanOpenSolution)
        {
            _launcher.OpenSolution(_solutionPath!);
        }
        else
        {
            _launcher.OpenNonSlnRepo(_path);
        }
        RequestClearSearch?.Invoke();
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
    private void OpenGitCli() => _launcher.OpenGitCommandLine(_path);

    [RelayCommand]
    private void CopyName() => System.Windows.Clipboard.SetText(_name);

    [RelayCommand]
    private void CopyPath() => System.Windows.Clipboard.SetText(_path);

    public void Dispose()
    {
        _branchProvider.BranchChanged -= OnBranchProviderChanged;
        _branchCts?.Cancel(); _branchCts?.Dispose(); _branchCts = null;
        _statusCts?.Cancel(); _statusCts?.Dispose(); _statusCts = null;
    }
}
