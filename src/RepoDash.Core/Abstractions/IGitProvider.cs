using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RepoDash.Core.Models;

namespace RepoDash.Core.Abstractions;

public interface IGitProvider
{
    Task<RepoStatus> GetStatusAsync(string repositoryPath, CancellationToken cancellationToken = default);
    Task<string?> GetRemoteUrlAsync(string repositoryPath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetLocalBranchesAsync(string repositoryPath, CancellationToken cancellationToken = default);
    Task CheckoutAsync(string repositoryPath, string branchName, CancellationToken cancellationToken = default);
    Task FetchAsync(string repositoryPath, CancellationToken cancellationToken = default);
    Task PullAsync(string repositoryPath, bool rebase, CancellationToken cancellationToken = default);
}
