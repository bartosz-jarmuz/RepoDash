using System.Collections.Concurrent;
using System.IO;
using LibGit2Sharp;
using RepoDash.Core.Abstractions;

namespace RepoDash.Infrastructure.Git;

public sealed class LibGit2SharpGitService : IGitService
{
    private static readonly ConcurrentDictionary<string, DateTimeOffset> _lastTrackingFetch =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan TrackingRefreshInterval = TimeSpan.FromMinutes(1);

    public Task<bool> IsGitRepoAsync(string repoPath, CancellationToken ct)
        => Task.FromResult(Repository.IsValid(repoPath));

    public Task<string?> GetRemoteUrlAsync(string repoPath, CancellationToken ct)
    {
        try
        {
            using var repo = new Repository(repoPath);
            return Task.FromResult(repo.Network.Remotes["origin"]?.Url);
        }
        catch { return Task.FromResult<string?>(null); }
    }

    public Task<IReadOnlyList<string>> GetLocalBranchesAsync(string repoPath, CancellationToken ct)
    {
        using var repo = new Repository(repoPath);
        return Task.FromResult<IReadOnlyList<string>>(repo.Branches
            .Where(b => !b.IsRemote)
            .Select(b => b.FriendlyName)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    public Task<RepoStatus> GetStatusAsync(string repoPath, CancellationToken ct)
    {
        using var repo = new Repository(repoPath);
        var head = repo.Head;
        var branch = head?.FriendlyName ?? "(detached)";
        var dirty = repo.RetrieveStatus().IsDirty;
        RefreshTrackedBranchIfNeeded(repo, head, repoPath, ct);
        head = repo.Head;
        var ahead = head?.TrackingDetails?.AheadBy ?? 0;
        var behind = head?.TrackingDetails?.BehindBy ?? 0;

        var sync = BranchSyncState.Unknown;
        if (ahead == 0 && behind == 0) sync = BranchSyncState.UpToDate;
        else if (ahead > 0 && behind == 0) sync = BranchSyncState.Ahead;
        else if (behind > 0 && ahead == 0) sync = BranchSyncState.Behind;

        return Task.FromResult(new RepoStatus(branch, sync, dirty));
    }

    public Task FetchAsync(string repoPath, CancellationToken ct)
    {
        using var repo = new Repository(repoPath);
        Commands.Fetch(repo, "origin", Array.Empty<string>(), null, null);
        return Task.CompletedTask;
    }

    public Task PullAsync(string repoPath, bool rebase, CancellationToken ct)
    {
        using var repo = new Repository(repoPath);
        var sig = repo.Config.BuildSignature(DateTimeOffset.Now) ??
                  new Signature("RepoDash", "repodash@local", DateTimeOffset.Now);
        var options = new PullOptions
        {
            FetchOptions = new FetchOptions(),
            MergeOptions = rebase ? new MergeOptions { FastForwardStrategy = FastForwardStrategy.NoFastForward }
                                  : new MergeOptions()
        };
        Commands.Pull(repo, sig, options);
        return Task.CompletedTask;
    }

    public Task CheckoutAsync(string repoPath, string branchName, CancellationToken ct)
    {
        using var repo = new Repository(repoPath);
        Commands.Checkout(repo, branchName);
        return Task.CompletedTask;
    }

    private static void RefreshTrackedBranchIfNeeded(Repository repo, Branch? head, string repoPath, CancellationToken ct)
    {
        if (head?.IsTracking != true) return;
        if (ct.IsCancellationRequested) return;

        var repoKey = repo.Info.WorkingDirectory ?? Path.GetFullPath(repoPath);
        var now = DateTimeOffset.UtcNow;
        if (_lastTrackingFetch.TryGetValue(repoKey, out var lastFetch) &&
            now - lastFetch < TrackingRefreshInterval)
        {
            return;
        }

        var remoteName = head.RemoteName;
        if (string.IsNullOrWhiteSpace(remoteName)) return;

        var remote = repo.Network.Remotes[remoteName];
        if (remote is null) return;

        try
        {
            Commands.Fetch(repo, remote.Name, Array.Empty<string>(), null, null);
            _lastTrackingFetch[repoKey] = now;
        }
        catch
        {
            _lastTrackingFetch.TryRemove(repoKey, out _);
        }
    }
}
