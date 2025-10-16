using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Settings;

namespace RepoDash.Infrastructure.Scanning;

public sealed class FileSystemRepoScanner : IRepoScanner
{
    private readonly ISettingsStore<RepositoriesSettings> _reposSettings;

    public FileSystemRepoScanner(ISettingsStore<RepositoriesSettings> reposSettings)
        => _reposSettings = reposSettings;

    public async IAsyncEnumerable<RepoInfo> ScanAsync(string rootPath, int groupingSegment, [EnumeratorCancellation] CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            yield break;

        // Snapshot settings once
        var settings = _reposSettings.Current;
        var excluded = new HashSet<string>(settings.ExcludedPathParts.Select(NormalizePart), StringComparer.OrdinalIgnoreCase);
        var overrides = settings.CategoryOverrides.ToList();

        // Work queue of directories to inspect
        var toVisit = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });

        // ensure normalized root is first
        var root = NormalizePath(rootPath);
        _ = toVisit.Writer.TryWrite(root);

        // pending counter to know when traversal is done
        var pending = 1;
        var visited = new ConcurrentDictionary<string, byte?>(StringComparer.OrdinalIgnoreCase);
        var results = new ConcurrentBag<RepoInfo>();

        // degree of parallelism (bounded to avoid hammering disk)
        var dop = Math.Clamp(Environment.ProcessorCount, 2, 8);
        var workers = new List<Task>(capacity: dop);

        for (var w = 0; w < dop; w++)
        {
            workers.Add(Task.Run(async () =>
            {
                while (await toVisit.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
                {
                    while (toVisit.Reader.TryRead(out var current))
                    {
                        ct.ThrowIfCancellationRequested();

                        var dir = NormalizePath(current);
                        if (!visited.TryAdd(dir, null)) { DecrementPending(); continue; }

                        if (IsExcluded(dir, excluded)) { DecrementPending(); continue; }

                        // If it contains a .git folder, it's a repo → collect info
                        string? gitDir = null;
                        try
                        {
                            gitDir = Directory.EnumerateDirectories(dir, ".git", SearchOption.TopDirectoryOnly).FirstOrDefault();
                        }
                        catch
                        {
                        }

                        if (gitDir != null)
                        {
                            var sln = TryFindSolution(dir);
                            var name = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

                            var group = ComputeGroupKey(rootPath, dir, groupingSegment);
                            group = ApplyCategoryOverrides(group, dir, sln, overrides);

                            results.Add(new RepoInfo(
                                RepoName: name,
                                RepoPath: dir,
                                HasGit: true,
                                HasSolution: sln != null,
                                SolutionPath: sln,
                                GroupKey: group));

                            DecrementPending();
                            continue;
                        }

                        // Not a repo → enqueue subdirectories
                        IEnumerable<string> subs = Array.Empty<string>();
                        try
                        {
                            subs = Directory.EnumerateDirectories(dir);
                        }
                        catch
                        {
                        }

                        foreach (var d in subs)
                        {
                            Interlocked.Increment(ref pending);
                            _ = toVisit.Writer.TryWrite(d);
                        }

                        DecrementPending();
                    }
                }
            }, ct));
        }

        // Watcher to complete the channel when pending hits 0
        void DecrementPending()
        {
            if (Interlocked.Decrement(ref pending) == 0)
            {
                toVisit.Writer.TryComplete();
            }
        }

        try
        {
            await Task.WhenAll(workers).ConfigureAwait(false);
        }
        catch
        {
            // best-effort traversal; ensure channel completes
            toVisit.Writer.TryComplete();
            throw;
        }

        // Final ORDER: GroupKey, then RepoName (case-insensitive) – matches unit test expectations.
        foreach (var item in results
            .OrderBy(r => r.GroupKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.RepoName, StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            yield return item;
            await Task.Yield(); // keep UI responsive while enumerating sorted results
        }
    }

    static string NormalizePath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    static string NormalizePart(string s) => (s ?? string.Empty).Trim();

    static bool IsExcluded(string path, HashSet<string> excludedParts)
    {
        if (excludedParts.Count == 0) return false;
        foreach (var part in excludedParts)
        {
            if (string.IsNullOrWhiteSpace(part)) continue;
            if (path.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    static string? TryFindSolution(string repoPath)
    {
        try
        {
            var top = Directory.EnumerateFiles(repoPath, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (top != null) return top;

            var srcDir = Path.Combine(repoPath, "src");
            if (Directory.Exists(srcDir))
            {
                var nested = Directory.EnumerateFiles(srcDir, "*.sln", SearchOption.AllDirectories).FirstOrDefault();
                if (nested != null) return nested;
            }
        }
        catch
        {
        }
        return null;
    }

    static string ComputeGroupKey(string root, string repoPath, int groupingSegment)
    {
        var relative = Path.GetRelativePath(root, repoPath);
        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                            .Where(p => !string.IsNullOrWhiteSpace(p))
                            .ToArray();

        if (parts.Length == 0) return Path.GetFileName(repoPath);
        if (groupingSegment <= 0) return parts[0];

        var idxFromEnd = parts.Length - groupingSegment;
        if (idxFromEnd >= 0 && idxFromEnd < parts.Length)
            return parts[idxFromEnd];

        return parts.Length > 1 ? parts[^2] : parts[0];
    }

    static string ApplyCategoryOverrides(string currentGroupKey, string repoPath, string? solutionPath, List<CategoryOverride> rules)
    {
        if (rules.Count == 0) return currentGroupKey;

        var solName = solutionPath is null ? string.Empty : Path.GetFileNameWithoutExtension(solutionPath);

        foreach (var rule in rules)
        {
            var target = rule.Category?.Trim();
            if (string.IsNullOrEmpty(target)) continue;
            var frags = rule.Matches ?? new();
            if (ContainsAnyFragment(repoPath, frags) ||
                ContainsAnyFragment(solutionPath ?? string.Empty, frags) ||
                ContainsAnyFragment(solName, frags))
            {
                return target; // first match wins
            }
        }
        return currentGroupKey;
    }

    static bool ContainsAnyFragment(string haystack, IEnumerable<string> fragments)
    {
        foreach (var f in fragments)
        {
            var frag = f?.Trim();
            if (string.IsNullOrWhiteSpace(frag)) continue;
            if (haystack.IndexOf(frag, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }
}
