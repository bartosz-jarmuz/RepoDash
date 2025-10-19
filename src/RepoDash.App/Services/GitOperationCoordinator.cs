using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RepoDash.App.Abstractions;
using RepoDash.App.ViewModels;
using RepoDash.Core.Abstractions;

namespace RepoDash.App.Services;

internal sealed class GitOperationCoordinator
{
    private const int StatusRefreshParallelism = 4;
    private const int GitOperationParallelism = 12;

    private readonly IGitService _git;
    private readonly ILauncher _launcher;
    private readonly StatusBarViewModel _statusBar;
    private readonly Action<Action> _dispatch;

    public GitOperationCoordinator(
        IGitService git,
        ILauncher launcher,
        StatusBarViewModel statusBar,
        Action<Action> dispatch)
    {
        _git = git;
        _launcher = launcher;
        _statusBar = statusBar;
        _dispatch = dispatch;
    }

    public async Task FetchAllAsync(IReadOnlyList<RepoItemViewModel> repos, CancellationToken ct)
    {
        var gitRepos = repos.Where(r => r.HasGit).ToList();
        if (gitRepos.Count == 0) return;

        var failures = new FailureTracker();
        long operationId = 0;
        _dispatch(() =>
        {
            _statusBar.ClearError();
            operationId = _statusBar.BeginProgress("Fetching remotes...", gitRepos.Count);
        });

        using var throttler = new SemaphoreSlim(GitOperationParallelism);
        var tasks = gitRepos.Select(repo => Task.Run(async () =>
        {
            await throttler.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (!ct.IsCancellationRequested)
                {
                    try
                    {
                        await _git.FetchAsync(repo.Path, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        RecordFailure(failures, "fetch", new OperationFailure(repo, "Fetching from remote", ex));
                    }
                }

                if (!ct.IsCancellationRequested)
                {
                    var refresh = await repo.RefreshStatusAsync(ct).ConfigureAwait(false);
                    if (!refresh.Success && refresh.Error is not null)
                    {
                        RecordFailure(failures, "fetch", new OperationFailure(repo, "Refreshing git status", refresh.Error));
                    }
                }
            }
            finally
            {
                var localId = operationId;
                if (localId != 0)
                    _dispatch(() => _statusBar.ReportProgressIncrement(localId));
                throttler.Release();
            }
        }, ct)).ToList();

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }

        var hasFailures = failures.HasFailures;
        var completionMessage = ct.IsCancellationRequested
            ? "Fetch cancelled"
            : hasFailures ? "Fetch completed with issues" : "Fetch complete";

        var finalId = operationId;
        if (finalId != 0)
        {
            var message = ct.IsCancellationRequested
                ? completionMessage
                : hasFailures ? null : completionMessage;
            _dispatch(() => _statusBar.CompleteProgress(message, finalId));
        }

        if (ct.IsCancellationRequested || hasFailures) return;

        _dispatch(() => _statusBar.ClearError());
    }

    public async Task PullAllAsync(IReadOnlyList<RepoItemViewModel> repos, bool rebase, CancellationToken ct)
    {
        var gitRepos = repos.Where(r => r.HasGit).ToList();
        if (gitRepos.Count == 0) return;

        var failures = new FailureTracker();
        long operationId = 0;
        var operationLabel = rebase ? "Pulling (rebase)" : "Pulling";
        _dispatch(() =>
        {
            _statusBar.ClearError();
            operationId = _statusBar.BeginProgress($"{operationLabel} repositories...", gitRepos.Count);
        });

        using var throttler = new SemaphoreSlim(GitOperationParallelism);
        var tasks = gitRepos.Select(repo => Task.Run(async () =>
        {
            await throttler.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (!ct.IsCancellationRequested)
                {
                    try
                    {
                        await _git.PullAsync(repo.Path, rebase, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        RecordFailure(failures, "pull", new OperationFailure(repo, "Pulling changes", ex));
                        _launcher.OpenGitUi(repo.Path);
                    }
                }

                if (!ct.IsCancellationRequested)
                {
                    var refresh = await repo.RefreshStatusAsync(ct).ConfigureAwait(false);
                    if (!refresh.Success && refresh.Error is not null)
                    {
                        RecordFailure(failures, "pull", new OperationFailure(repo, "Refreshing git status", refresh.Error));
                    }
                }
            }
            finally
            {
                var localId = operationId;
                if (localId != 0)
                    _dispatch(() => _statusBar.ReportProgressIncrement(localId));
                throttler.Release();
            }
        }, ct)).ToList();

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }

        var hasFailures = failures.HasFailures;
        var completionMessage = ct.IsCancellationRequested
            ? "Pull cancelled"
            : hasFailures ? "Pull completed with issues" : "Pull complete";

        var finalId = operationId;
        if (finalId != 0)
        {
            var message = ct.IsCancellationRequested
                ? completionMessage
                : hasFailures ? null : completionMessage;
            _dispatch(() => _statusBar.CompleteProgress(message, finalId));
        }

        if (ct.IsCancellationRequested || hasFailures) return;

        _dispatch(() => _statusBar.ClearError());
    }

    public async Task RefreshStatusesAsync(
        IReadOnlyList<RepoItemViewModel> repos,
        CancellationToken ct,
        string description)
    {
        if (repos.Count == 0 || ct.IsCancellationRequested)
            return;

        var failures = new FailureTracker();
        long operationId = 0;

        _dispatch(() =>
        {
            _statusBar.ClearError();
            operationId = _statusBar.BeginProgress(description, repos.Count);
        });

        using var gate = new SemaphoreSlim(StatusRefreshParallelism);
        var tasks = repos.Select(repo => Task.Run(async () =>
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var refresh = await repo.RefreshStatusAsync(ct).ConfigureAwait(false);
                if (!refresh.Success && refresh.Error is not null && !ct.IsCancellationRequested)
                {
                    RecordFailure(failures, "refresh git status", new OperationFailure(repo, description, refresh.Error));
                }
            }
            finally
            {
                var localId = operationId;
                if (localId != 0)
                    _dispatch(() => _statusBar.ReportProgressIncrement(localId));
                gate.Release();
            }
        }, ct)).ToList();

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }

        var hasFailures = failures.HasFailures;
        var completionMessage = ct.IsCancellationRequested
            ? $"{description} cancelled"
            : hasFailures ? $"{description} finished with issues" : $"{description} complete";

        var finalId = operationId;
        if (finalId != 0)
        {
            var message = ct.IsCancellationRequested
                ? completionMessage
                : hasFailures ? null : completionMessage;
            _dispatch(() => _statusBar.CompleteProgress(message, finalId));
        }

        if (ct.IsCancellationRequested || hasFailures) return;

        _dispatch(() => _statusBar.ClearError());
    }

    private void RecordFailure(FailureTracker tracker, string operation, OperationFailure failure)
    {
        var snapshot = tracker.Add(failure);
        _dispatch(() =>
        {
            var (summary, details) = BuildFailureMessage(operation, snapshot);
            _statusBar.ShowError(summary, details);
        });
    }

    private static (string Summary, string Details) BuildFailureMessage(string operation, IReadOnlyList<OperationFailure> failures)
    {
        var summary = failures.Count == 1
            ? $"Failed to {operation} for {failures[0].Repo.Name}"
            : $"Failed to {operation} for {failures.Count} repositories";

        var details = new StringBuilder();
        foreach (var failure in failures)
        {
            details.AppendLine($"{failure.Repo.Name} ({failure.Repo.Path})");
            details.AppendLine($"  {failure.Stage}: {failure.Error.Message}");
        }

        return (summary, details.ToString().TrimEnd());
    }

    private sealed record OperationFailure(RepoItemViewModel Repo, string Stage, Exception Error);

    private sealed class FailureTracker
    {
        private readonly List<OperationFailure> _items = new();

        public IReadOnlyList<OperationFailure> Add(OperationFailure failure)
        {
            lock (_items)
            {
                _items.Add(failure);
                return _items.ToArray();
            }
        }

        public bool HasFailures
        {
            get
            {
                lock (_items)
                {
                    return _items.Count > 0;
                }
            }
        }
    }
}
