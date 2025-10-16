using RepoDash.Core.Abstractions;

namespace RepoDash.Core.Usage;

public sealed class RepoUsageService : IRepoUsageService
{
    private readonly IRepoUsageStore _store;
    private readonly object _gate = new();
    private readonly Dictionary<string, RepoUsageEntry> _entriesByPath;
    private readonly HashSet<string> _pinnedPaths;
    private readonly HashSet<string> _pinnedNames;
    private readonly HashSet<string> _blacklistedPaths;
    private readonly HashSet<string> _blacklistedNames;
    private readonly StringComparer _pathComparer = StringComparer.OrdinalIgnoreCase;
    private readonly StringComparer _nameComparer = StringComparer.OrdinalIgnoreCase;

    public event EventHandler? Changed;

    public RepoUsageService(IRepoUsageStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));

        RepoUsageState state;
        try
        {
            state = _store.ReadAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        catch
        {
            state = new RepoUsageState();
        }

        _entriesByPath = new Dictionary<string, RepoUsageEntry>(_pathComparer);
        foreach (var entry in state.Entries)
        {
            var normalizedPath = NormalizePath(entry.RepoPath);
            // Keep the original path for user-facing scenarios, just normalize the key
            _entriesByPath[normalizedPath] = entry;
        }

        _pinnedPaths = new HashSet<string>(state.PinnedPaths.Select(NormalizePath), _pathComparer);
        _pinnedNames = new HashSet<string>(state.PinnedNames ?? [], _nameComparer);
        _blacklistedPaths = new HashSet<string>(state.BlacklistedPaths.Select(NormalizePath), _pathComparer);
        _blacklistedNames = new HashSet<string>(state.BlacklistedNames ?? [], _nameComparer);
    }

    public void RecordUsage(RepoUsageSnapshot snapshot)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));

        var now = DateTimeOffset.UtcNow;
        var normalizedPath = NormalizePath(snapshot.RepoPath);
        var normalizedName = NormalizeName(snapshot.RepoName);

        lock (_gate)
        {
            if (_entriesByPath.TryGetValue(normalizedPath, out var current))
            {
                var nextCount = current.UsageCount >= int.MaxValue
                    ? int.MaxValue
                    : current.UsageCount + 1;

                _entriesByPath[normalizedPath] = current with
                {
                    RepoName = snapshot.RepoName,
                    RepoPath = snapshot.RepoPath,
                    HasGit = snapshot.HasGit,
                    HasSolution = snapshot.HasSolution,
                    SolutionPath = snapshot.SolutionPath,
                    LastUsedUtc = now,
                    UsageCount = nextCount
                };
            }
            else
            {
                _entriesByPath[normalizedPath] = new RepoUsageEntry
                {
                    RepoName = snapshot.RepoName,
                    RepoPath = snapshot.RepoPath,
                    HasGit = snapshot.HasGit,
                    HasSolution = snapshot.HasSolution,
                    SolutionPath = snapshot.SolutionPath,
                    LastUsedUtc = now,
                    UsageCount = 1
                };
            }

            // ensure pinned/blacklisted hash-sets also track normalized names when entry recorded
            if (_pinnedNames.Contains(normalizedName))
            {
                _pinnedPaths.Add(normalizedPath);
            }

            if (_blacklistedNames.Contains(normalizedName))
            {
                _blacklistedPaths.Add(normalizedPath);
            }
        }

        ScheduleSave();
        RaiseChanged();
    }

    public IReadOnlyList<RepoUsageEntry> GetRecent(int maxCount)
    {
        if (maxCount <= 0) return Array.Empty<RepoUsageEntry>();

        lock (_gate)
        {
            var ordered = _entriesByPath.Values
                .Where(e => !IsBlacklistedInternal(e.RepoName, e.RepoPath))
                .OrderByDescending(e => e.LastUsedUtc)
                .Take(maxCount)
                .Select(e => e with { })
                .ToList();

            return ordered;
        }
    }

    public IReadOnlyList<RepoUsageSummary> GetFrequent(int maxCount)
    {
        if (maxCount <= 0) return Array.Empty<RepoUsageSummary>();

        lock (_gate)
        {
            var aggregates = new Dictionary<string, (int Usage, DateTimeOffset LastUsed)>(_nameComparer);

            foreach (var entry in _entriesByPath.Values)
            {
                if (IsBlacklistedInternal(entry.RepoName, entry.RepoPath)) continue;

                var key = NormalizeName(entry.RepoName);
                if (!aggregates.TryGetValue(key, out var agg))
                {
                    aggregates[key] = (SafeAdd(0, entry.UsageCount), entry.LastUsedUtc);
                }
                else
                {
                    var usage = SafeAdd(agg.Usage, entry.UsageCount);
                    var lastUsed = entry.LastUsedUtc > agg.LastUsed ? entry.LastUsedUtc : agg.LastUsed;
                    aggregates[key] = (usage, lastUsed);
                }
            }

            var summaries = aggregates
                .Select(kv => new RepoUsageSummary
                {
                    RepoName = kv.Key,
                    UsageCount = kv.Value.Usage,
                    LastUsedUtc = kv.Value.LastUsed
                })
                .OrderByDescending(s => s.UsageCount)
                .ThenByDescending(s => s.LastUsedUtc)
                .ThenBy(s => s.RepoName, _nameComparer)
                .Take(maxCount)
                .ToList();

            return summaries;
        }
    }

    public bool TogglePinned(string repoName, string repoPath)
    {
        var normalizedPath = NormalizePath(repoPath);
        var normalizedName = NormalizeName(repoName);
        bool result;

        lock (_gate)
        {
            if (_pinnedNames.Contains(normalizedName) || _pinnedPaths.Contains(normalizedPath))
            {
                _pinnedNames.Remove(normalizedName);
                RemovePinnedPathsForName(normalizedName);
                result = false;
            }
            else
            {
                _pinnedNames.Add(normalizedName);
                _pinnedPaths.Add(normalizedPath);
                result = true;
            }
        }

        ScheduleSave();
        RaiseChanged();
        return result;
    }

    public bool ToggleBlacklisted(string repoName, string repoPath)
    {
        var normalizedPath = NormalizePath(repoPath);
        var normalizedName = NormalizeName(repoName);
        bool result;

        lock (_gate)
        {
            if (_blacklistedNames.Contains(normalizedName) || _blacklistedPaths.Contains(normalizedPath))
            {
                _blacklistedNames.Remove(normalizedName);
                RemoveBlacklistedPathsForName(normalizedName);
                result = false;
            }
            else
            {
                _blacklistedNames.Add(normalizedName);
                _blacklistedPaths.Add(normalizedPath);
                result = true;
            }
        }

        ScheduleSave();
        RaiseChanged();
        return result;
    }

    public bool IsPinned(string repoName, string repoPath)
    {
        var normalizedPath = NormalizePath(repoPath);
        var normalizedName = NormalizeName(repoName);

        lock (_gate)
        {
            return _pinnedNames.Contains(normalizedName) || _pinnedPaths.Contains(normalizedPath);
        }
    }

    public bool IsBlacklisted(string repoName, string repoPath)
    {
        var normalizedPath = NormalizePath(repoPath);
        var normalizedName = NormalizeName(repoName);

        lock (_gate)
        {
            return _blacklistedNames.Contains(normalizedName) || _blacklistedPaths.Contains(normalizedPath);
        }
    }

    private void RemovePinnedPathsForName(string normalizedName)
    {
        var toRemove = new List<string>();
        foreach (var kv in _entriesByPath)
        {
            if (string.Equals(NormalizeName(kv.Value.RepoName), normalizedName, StringComparison.OrdinalIgnoreCase))
            {
                toRemove.Add(kv.Key);
            }
        }
        foreach (var key in toRemove)
        {
            _pinnedPaths.Remove(key);
        }
    }

    private void RemoveBlacklistedPathsForName(string normalizedName)
    {
        var toRemove = new List<string>();
        foreach (var kv in _entriesByPath)
        {
            if (string.Equals(NormalizeName(kv.Value.RepoName), normalizedName, StringComparison.OrdinalIgnoreCase))
            {
                toRemove.Add(kv.Key);
            }
        }
        foreach (var key in toRemove)
        {
            _blacklistedPaths.Remove(key);
        }
    }

    private bool IsBlacklistedInternal(string repoName, string repoPath)
    {
        var normalizedPath = NormalizePath(repoPath);
        var normalizedName = NormalizeName(repoName);
        return _blacklistedNames.Contains(normalizedName) || _blacklistedPaths.Contains(normalizedPath);
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        try
        {
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return path.Trim();
        }
    }

    private static string NormalizeName(string name) => (name ?? string.Empty).Trim();

    private static int SafeAdd(int a, int b)
    {
        var sum = (long)a + b;
        return sum >= int.MaxValue ? int.MaxValue : (int)sum;
    }

    private RepoUsageState CreateSnapshot()
    {
        var entries = _entriesByPath.Values
            .Select(e => e with { })
            .ToList();

        return new RepoUsageState
        {
            Entries = entries,
            PinnedPaths = _pinnedPaths.ToList(),
            PinnedNames = _pinnedNames.ToList(),
            BlacklistedPaths = _blacklistedPaths.ToList(),
            BlacklistedNames = _blacklistedNames.ToList()
        };
    }

    private void ScheduleSave()
    {
        RepoUsageState snapshot;
        lock (_gate)
        {
            snapshot = CreateSnapshot();
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await _store.WriteAsync(snapshot, CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // best-effort persistence; failures are non-fatal
            }
        });
    }

    private void RaiseChanged()
    {
        var handler = Changed;
        handler?.Invoke(this, EventArgs.Empty);
    }
}
