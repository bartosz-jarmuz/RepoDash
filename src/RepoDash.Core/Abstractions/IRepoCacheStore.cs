using RepoDash.Core.Caching;

namespace RepoDash.Core.Abstractions;

public interface IRepoCacheStore
{
    Task<RepoRootCache?> ReadAsync(string normalizedRootPath, CancellationToken ct);
    Task WriteAsync(string normalizedRootPath, RepoRootCache cache, CancellationToken ct);
}