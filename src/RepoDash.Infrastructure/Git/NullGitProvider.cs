using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Models;

namespace RepoDash.Infrastructure.Git;

public sealed class NullGitProvider : IGitProvider
{
    public Task<RepoStatus> GetStatusAsync(string repositoryPath, CancellationToken cancellationToken = default)
        => Task.FromResult(RepoStatus.Unknown);

    public Task<string?> GetRemoteUrlAsync(string repositoryPath, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);

    public Task<IReadOnlyList<string>> GetLocalBranchesAsync(string repositoryPath, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

    public Task CheckoutAsync(string repositoryPath, string branchName, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task FetchAsync(string repositoryPath, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task PullAsync(string repositoryPath, bool rebase, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
