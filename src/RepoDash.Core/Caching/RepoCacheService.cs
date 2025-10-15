using System.Collections.Concurrent;
using RepoDash.Core.Abstractions;

namespace RepoDash.Core.Caching;
public sealed class RepoCacheService
{
    private readonly IRepoCacheStore _store;
    private readonly IRepoScanner _scanner;
    private readonly IBranchProvider _branchProvider;

    public RepoCacheService(IRepoCacheStore store, IRepoScanner scanner, IBranchProvider branchProvider)
    {
        _store = store;
        _scanner = scanner;
        _branchProvider = branchProvider;
    }

    public async Task<IReadOnlyList<CachedRepo>> LoadFromCacheAsync(string rootPath, CancellationToken ct)
    {
        var key = NormalizePathString(rootPath);
        var cache = await _store.ReadAsync(key, ct);
        return cache?.Repos ?? [];
    }

    public async Task RefreshAsync(
        string rootPath,
        int groupingSegment,
        Action<CachedRepo> upsert,
        Action<string> removeByRepoPath,
        CancellationToken ct)
    {
        var key = NormalizePathString(rootPath);
        var cache = await _store.ReadAsync(key, ct) ?? new RepoRootCache
        {
            NormalizedRoot = key,
            CachedAtUtc = DateTimeOffset.UtcNow,
            Repos = []
        };

        var currentByPath = new ConcurrentDictionary<string, CachedRepo>(
            cache.Repos.Select(r => new KeyValuePair<string, CachedRepo>(TrimPath(r.RepoPath), r)));

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await foreach (var info in _scanner.ScanAsync(rootPath, groupingSegment, ct))
        {
            ct.ThrowIfCancellationRequested();

            var p = TrimPath(info.RepoPath);
            seen.Add(p);

            var signature = RepoSignatureCalculator.Compute(info.RepoPath, info.SolutionPath);

            if (!currentByPath.TryGetValue(p, out var existing) || existing.Signature != signature)
            {
                // lightweight branch is already used inside RepoItemViewModel; we only store cheap facts here
                var updated = new CachedRepo
                {
                    RepoName = info.RepoName,
                    RepoPath = info.RepoPath,
                    HasGit = info.HasGit,
                    HasSolution = info.HasSolution,
                    SolutionPath = info.SolutionPath,
                    GroupKey = info.GroupKey,
                    Signature = signature,
                    LastSeenUtc = DateTimeOffset.UtcNow
                };

                currentByPath[p] = updated;
                upsert(updated);
            }
        }

        foreach (var kv in currentByPath.ToArray())
        {
            if (!seen.Contains(kv.Key))
            {
                currentByPath.TryRemove(kv.Key, out _);
                removeByRepoPath(kv.Value.RepoPath);
            }
        }

        cache.Repos = currentByPath.Values
            .OrderBy(r => r.GroupKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.RepoName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        cache.CachedAtUtc = DateTimeOffset.UtcNow;

        await _store.WriteAsync(key, cache, ct);
    }

    public static string NormalizePathString(string path)
    {
        var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        // Make a filesystem-friendly key
        var key = full.Replace(':', '_').Replace('\\', '_').Replace('/', '_');
        return key;
    }

    static string TrimPath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}