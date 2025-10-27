using System;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoDash.App.Abstractions;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Settings;
using RepoDash.Core.Usage;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace RepoDash.App.ViewModels;

public partial class RepoItemViewModel : ObservableObject, IDisposable
{
    private readonly ILauncher _launcher;
    private readonly IGitService _git;
    private readonly IRemoteLinkProvider _links;
    private readonly IBranchProvider _branchProvider;
    private readonly IRepoUsageService _usage;
    private readonly IReadOnlySettingsSource<GeneralSettings> _generalSettings;

    private CancellationTokenSource? _branchCts;
    private CancellationTokenSource? _statusCts;
    private Uri? _storyUri;

    public RepoItemViewModel(
        ILauncher launcher,
        IGitService git,
        IRemoteLinkProvider links,
        IBranchProvider branchProvider,
        IRepoUsageService usage,
        IReadOnlySettingsSource<GeneralSettings> generalSettings)
    {
        _launcher = launcher;
        _git = git;
        _links = links;
        _branchProvider = branchProvider;
        _usage = usage;
        _generalSettings = generalSettings;

        _branchProvider.BranchChanged += OnBranchProviderChanged;
        _generalSettings.PropertyChanged += OnGeneralSettingsChanged;
        UpdateStoryLink();
    }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _path = string.Empty;
    [ObservableProperty] private bool _hasSolution;
    [ObservableProperty] private string? _solutionPath;
    [ObservableProperty] private bool _hasGit;

    [ObservableProperty] private string? _currentBranch;
    [ObservableProperty] private BranchSyncState _syncState;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private string? _storyReference;

    [ObservableProperty] private bool _isPinned;
    [ObservableProperty] private bool _isBlacklisted;
    [ObservableProperty] private DateTimeOffset? _lastUsedUtc;
    [ObservableProperty] private int _usageCount;

    public Action? RequestClearSearch { get; set; }

    public bool HasStoryLink => !string.IsNullOrWhiteSpace(StoryReference) && _storyUri is not null;

    public bool CanOpenSolution => HasSolution && !string.IsNullOrWhiteSpace(SolutionPath);
    partial void OnHasSolutionChanged(bool value) => OnPropertyChanged(nameof(CanOpenSolution));
    partial void OnSolutionPathChanged(string? value) => OnPropertyChanged(nameof(CanOpenSolution));
    partial void OnNameChanged(string value) => RefreshUsageFlags();
    partial void OnPathChanged(string value) => RefreshUsageFlags();
    partial void OnHasGitChanged(bool value) => UpdateStoryLink();
    partial void OnCurrentBranchChanged(string? value) => UpdateStoryLink();

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

    public async Task<RepoStatusRefreshResult> RefreshStatusAsync(CancellationToken externalCt)
    {
        if (!HasGit || string.IsNullOrWhiteSpace(Path))
        {
            SyncState = BranchSyncState.Unknown;
            IsDirty = false;
            return new RepoStatusRefreshResult(false, null);
        }

        _ = EnsureBranchLoadedAsync();

        _statusCts?.Cancel();
        _statusCts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        var ct = _statusCts.Token;

        try
        {
            var status = await _git.GetStatusAsync(Path, ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return new RepoStatusRefreshResult(false, null);

            Ui(() =>
            {
                if (string.IsNullOrWhiteSpace(CurrentBranch) && !string.IsNullOrWhiteSpace(status.CurrentBranch))
                    CurrentBranch = status.CurrentBranch;

                SyncState = status.SyncState;
                IsDirty = status.IsDirty;
            });
            return new RepoStatusRefreshResult(true, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new RepoStatusRefreshResult(false, null);
        }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested)
            {
                Ui(() =>
                {
                    SyncState = BranchSyncState.Unknown;
                    IsDirty = false;
                });
                return new RepoStatusRefreshResult(false, ex);
            }
            return new RepoStatusRefreshResult(false, ex);
        }
    }

    private void OnGeneralSettingsChanged(object? sender, PropertyChangedEventArgs e)
        => UpdateStoryLink();

    private void UpdateStoryLink()
    {
        if (!HasGit)
        {
            ClearStoryLink();
            return;
        }

        var branch = CurrentBranch;
        if (string.IsNullOrWhiteSpace(branch) || string.Equals(branch, "-", StringComparison.Ordinal))
        {
            ClearStoryLink();
            return;
        }

        var settings = _generalSettings.Current;
        var baseUrl = settings.JiraBaseUrl?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            ClearStoryLink();
            return;
        }

        var patterns = settings.StoryReferenceRegularExpressions;
        if (patterns is null || patterns.Count == 0)
        {
            ClearStoryLink();
            return;
        }

        foreach (var pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern)) continue;

            Match match;
            try
            {
                match = Regex.Match(branch, pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (!match.Success) continue;

            var reference = ExtractStoryReference(match);
            if (string.IsNullOrWhiteSpace(reference)) continue;

            var uri = BuildStoryUri(baseUrl, reference);
            if (uri is null) continue;

            SetStoryLink(reference, uri);
            return;
        }

        ClearStoryLink();
    }

    private static string? ExtractStoryReference(Match match)
    {
        var group = match.Groups["story"];
        if (group.Success && !string.IsNullOrWhiteSpace(group.Value))
        {
            return group.Value;
        }

        for (var i = 1; i < match.Groups.Count; i++)
        {
            var g = match.Groups[i];
            if (g.Success && !string.IsNullOrWhiteSpace(g.Value))
            {
                return g.Value;
            }
        }

        return string.IsNullOrWhiteSpace(match.Value) ? null : match.Value;
    }

    private static Uri? BuildStoryUri(string baseUrl, string reference)
    {
        var urlCandidate = (baseUrl ?? string.Empty) + reference;
        if (Uri.TryCreate(urlCandidate, UriKind.Absolute, out var direct))
        {
            return direct;
        }

        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) &&
            Uri.TryCreate(baseUri, reference, out var combined))
        {
            return combined;
        }

        return null;
    }

    private void SetStoryLink(string? reference, Uri? uri)
    {
        _storyUri = uri;
        StoryReference = reference;
        OnPropertyChanged(nameof(HasStoryLink));
        OpenStoryCommand?.NotifyCanExecuteChanged();
    }

    private void ClearStoryLink() => SetStoryLink(null, null);

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

    [RelayCommand(CanExecute = nameof(CanOpenStory))]
    private void OpenStory()
    {
        if (_storyUri is null) return;
        _launcher.OpenUrl(_storyUri);
    }

    private bool CanOpenStory() => HasStoryLink;

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

    [RelayCommand(CanExecute = nameof(CanCopyBranchName))]
    private void CopyBranchName()
    {
        if (!string.IsNullOrWhiteSpace(CurrentBranch))
        {
            System.Windows.Clipboard.SetText(CurrentBranch);
        }
    }

    private bool CanCopyBranchName() => !string.IsNullOrWhiteSpace(CurrentBranch);

    [RelayCommand(CanExecute = nameof(CanCopyStoryReference))]
    private void CopyStoryReference()
    {
        if (!string.IsNullOrWhiteSpace(StoryReference))
        {
            System.Windows.Clipboard.SetText(StoryReference);
        }
    }

    private bool CanCopyStoryReference() => HasStoryLink;

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
        _generalSettings.PropertyChanged -= OnGeneralSettingsChanged;
        _branchCts?.Cancel();
        _branchCts?.Dispose();
        _branchCts = null;
        _statusCts?.Cancel();
        _statusCts?.Dispose();
        _statusCts = null;
    }
}
