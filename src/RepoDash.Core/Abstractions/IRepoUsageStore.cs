using RepoDash.Core.Usage;

namespace RepoDash.Core.Abstractions;

public interface IRepoUsageStore
{
    Task<RepoUsageState> ReadAsync(CancellationToken ct);
    Task WriteAsync(RepoUsageState state, CancellationToken ct);
}
