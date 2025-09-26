namespace RepoDash.Core.Abstractions;

public interface IGitService
{
    Task<bool> IsGitRepoAsync(string repoPath, CancellationToken ct);
    Task<string?> GetRemoteUrlAsync(string repoPath, CancellationToken ct);
    Task<RepoStatus> GetStatusAsync(string repoPath, CancellationToken ct);
    Task FetchAsync(string repoPath, CancellationToken ct);
    Task PullAsync(string repoPath, bool rebase, CancellationToken ct);
    Task CheckoutAsync(string repoPath, string branchName, CancellationToken ct);
    Task<IReadOnlyList<string>> GetLocalBranchesAsync(string repoPath, CancellationToken ct);
}