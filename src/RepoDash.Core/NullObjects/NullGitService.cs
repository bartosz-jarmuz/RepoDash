using RepoDash.Core.Abstractions;

namespace RepoDash.Core.NullObjects;

public sealed class NullGitService : IGitService
{
    public Task<bool> IsGitRepoAsync(string repoPath, CancellationToken ct) => Task.FromResult(false);
    public Task<string?> GetRemoteUrlAsync(string repoPath, CancellationToken ct) => Task.FromResult<string?>(null);
    public Task<RepoStatus> GetStatusAsync(string repoPath, CancellationToken ct)
        => Task.FromResult(new RepoStatus("(unknown)", BranchSyncState.Unknown, false));
    public Task FetchAsync(string repoPath, CancellationToken ct) => Task.CompletedTask;
    public Task PullAsync(string repoPath, bool rebase, CancellationToken ct) => Task.CompletedTask;
    public Task CheckoutAsync(string repoPath, string branchName, CancellationToken ct) => Task.CompletedTask;
    public Task<IReadOnlyList<string>> GetLocalBranchesAsync(string repoPath, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
}