using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Usage;

namespace RepoDash.App.ViewModels;

public partial class RepoItemViewModel : ObservableObject, IDisposable
{
    private readonly ILauncher _launcher;
    private readonly IGitService _git;
    private readonly IRemoteLinkProvider _links;
    private readonly IBranchProvider _branchProvider;
    private readonly IRepoUsageService _usage;

    private CancellationTokenSource? _branchCts;
    private CancellationTokenSource? _statusCts;

    public RepoItemViewModel(
        ILauncher launcher,
        IGitService git,
        IRemoteLinkProvider links,
        IBranchProvider branchProvider,
        IRepoUsageService usage)
    {
        _launcher = launcher;
        _git = git;
        _links = links;
        _branchProvider = branchProvider;
        _usage = usage;

        _branchProvider.BranchChanged += OnBranchProviderChanged;
    }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _path = string.Empty;
    [ObservableProperty] private bool _hasSolution;
    [ObservableProperty] private string? _solutionPath;
    [ObservableProperty] private bool _hasGit;

    [ObservableProperty] private string? _currentBranch;
    [ObservableProperty] private BranchSyncState _syncState;
    [ObservableProperty] private bool _isDirty;

    [ObservableProperty] private bool _isPinned;
    [ObservableProperty] private bool _isBlacklisted;
    [ObservableProperty] private DateTimeOffset? _lastUsedUtc;
    [ObservableProperty] private int _usageCount;

    public Action? RequestClearSearch { get; set; }

    public bool CanOpenSolution => HasSolution && !string.IsNullOrWhiteSpace(SolutionPath);
    partial void OnHasSolutionChanged(bool value) => OnPropertyChanged(nameof(CanOpenSolution));
    partial void OnSolutionPathChanged(string? value) => OnPropertyChanged(nameof(CanOpenSolution));
    partial void OnNameChanged(string value) => RefreshUsageFlags();
    partial void OnPathChanged(string value) => RefreshUsageFlags();

    public async Task EnsureBranchLoadedAsync()
    {
        if (!HasGit || string.IsNullOrWhiteSpace(Path))
        {
            CurrentBranch = null;
            return;
        }

        CurrentBranch = _branchProvider.TryGetCached(Path) ?? CurrentBranch;

        _branchCts?.Cancel();
        _branchCts = new CancellationTokenSource();
        var ct = _branchCts.Token;

        try
        {
            var branch = await _branchProvider.GetCurrentBranchAsync(Path, ct).ConfigureAwait(false);
            if (!ct.IsCancellationRequested)
            {
                Ui(() => CurrentBranch = branch ?? CurrentBranch ?? "-");
            }
        }
        catch
        {
            if (!ct.IsCancellationRequested)
            {
                Ui(() => CurrentBranch = CurrentBranch ?? "-");
            }
        }
    }

    public async Task RefreshStatusAsync(CancellationToken externalCt)
    {
        if (!HasGit || string.IsNullOrWhiteSpace(Path))
        {
            SyncState = BranchSyncState.Unknown;
            IsDirty = false;
            return;
        }

        _ = EnsureBranchLoadedAsync();

        _statusCts?.Cancel();
        _statusCts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        var ct = _statusCts.Token;

        try
        {
            var status = await _git.GetStatusAsync(Path, ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return;

            Ui(() =>
            {
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
                    SyncState = BranchSyncState.Unknown;
                    IsDirty = false;
                });
            }
        }
    }

    private void OnBranchProviderChanged(object? sender, string changedRepoPath)
    {
        if (!PathsEqual(changedRepoPath, Path)) return;
        _ = EnsureBranchLoadedAsync();
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
            _launcher.OpenSolution(SolutionPath!);
        }
        else
        {
            _launcher.OpenNonSlnRepo(Path);
        }

        MarkUsed();
        RequestClearSearch?.Invoke();
    }

    [RelayCommand]
    private void Browse()
    {
        _launcher.OpenFolder(Path);
    }

    [RelayCommand]
    private async Task OpenRemoteAsync()
    {
        if (!HasGit) return;
        var url = await _git.GetRemoteUrlAsync(Path, CancellationToken.None);
        if (url is null) return;
        if (_links.TryGetProjectLinks(url, out var repo, out _))
        {
            _launcher.OpenUrl(repo!);
        }
    }

    [RelayCommand]
    private async Task OpenPipelinesAsync()
    {
        if (!HasGit) return;
        var url = await _git.GetRemoteUrlAsync(Path, CancellationToken.None);
        if (url is null) return;
        if (_links.TryGetProjectLinks(url, out _, out var pipelines))
        {
            _launcher.OpenUrl(pipelines!);
        }
    }

    [RelayCommand]
    private void OpenGitUi()
    {
        _launcher.OpenGitUi(Path);
    }

    [RelayCommand]
    private void OpenGitCli()
    {
        _launcher.OpenGitCommandLine(Path);
    }

    [RelayCommand]
    private void CopyName() => System.Windows.Clipboard.SetText(Name);

    [RelayCommand]
    private void CopyPath() => System.Windows.Clipboard.SetText(Path);

    [RelayCommand]
    private void TogglePin()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Path)) return;
        IsPinned = _usage.TogglePinned(Name, Path);
    }

    [RelayCommand]
    private void ToggleBlacklist()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Path)) return;
        IsBlacklisted = _usage.ToggleBlacklisted(Name, Path);
    }

    public void RefreshUsageFlags()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Path)) return;
        IsPinned = _usage.IsPinned(Name, Path);
        IsBlacklisted = _usage.IsBlacklisted(Name, Path);
    }

    public void ApplyUsageMetrics(DateTimeOffset? lastUsedUtc, int usageCount)
    {
        LastUsedUtc = lastUsedUtc;
        UsageCount = usageCount;
    }

    private void MarkUsed()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Path)) return;

        _usage.RecordUsage(new RepoUsageSnapshot
        {
            RepoName = Name,
            RepoPath = Path,
            HasGit = HasGit,
            HasSolution = HasSolution,
            SolutionPath = SolutionPath
        });

        RefreshUsageFlags();
    }

    public void Dispose()
    {
        _branchProvider.BranchChanged -= OnBranchProviderChanged;
        _branchCts?.Cancel();
        _branchCts?.Dispose();
        _branchCts = null;
        _statusCts?.Cancel();
        _statusCts?.Dispose();
        _statusCts = null;
    }
}
