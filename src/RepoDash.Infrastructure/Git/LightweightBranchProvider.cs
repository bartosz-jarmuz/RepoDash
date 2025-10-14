using RepoDash.Core.Abstractions;
using System.Collections.Concurrent;

namespace RepoDash.Infrastructure.Git
{
    /// <summary>
    /// Fast branch provider: reads .git/HEAD (and handles worktree-style .git file)
    /// and caches results keyed by repo path. Sets a file watcher on HEAD to auto-invalidate.
    /// </summary>
    public sealed class LightweightBranchProvider : IBranchProvider
    {
        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

        public event EventHandler<string>? BranchChanged;

        public string? TryGetCached(string repoPath)
        {
            if (_cache.TryGetValue(repoPath, out var entry))
                return entry.Branch;
            return null;
        }

        public async Task<string?> GetCurrentBranchAsync(string repoPath, CancellationToken ct)
        {
            // Fast path: if we have a cache and HEAD mtime matches, return it.
            if (_cache.TryGetValue(repoPath, out var existing))
            {
                var headPath = ResolveHeadPath(repoPath);
                if (headPath != null)
                {
                    var mtime = TryGetWriteTimeUtc(headPath);
                    if (mtime == existing.HeadWriteTimeUtc)
                        return existing.Branch;
                }
            }

            // Re-read HEAD (no heavy I/O, tiny file)
            var branch = await Task.Run(() => ReadBranch(repoPath), ct).ConfigureAwait(false);

            // Cache and ensure watcher
            var head = ResolveHeadPath(repoPath);
            var writeTime = head != null ? TryGetWriteTimeUtc(head) : default;
            var entry = new CacheEntry(branch, head, writeTime);

            _cache[repoPath] = entry;
            EnsureWatcher(repoPath, entry);

            return branch;
        }

        private static string? ReadBranch(string repoRoot)
        {
            try
            {
                var gitDir = ResolveGitDir(repoRoot);
                if (gitDir is null) return null;

                var headPath = Path.Combine(gitDir, "HEAD");
                if (!File.Exists(headPath)) return null;

                var head = File.ReadAllText(headPath).Trim();

                const string refPrefix = "ref: ";
                if (head.StartsWith(refPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var refPath = head.Substring(refPrefix.Length).Trim();
                    var lastSep = refPath.LastIndexOfAny(new[] { '\\', '/' });
                    var branch = lastSep >= 0 ? refPath[(lastSep + 1)..] : refPath;
                    return string.IsNullOrWhiteSpace(branch) ? null : branch;
                }

                // Detached HEAD (HEAD contains a SHA-ish string)
                if (IsLikelySha(head)) return "(detached)";

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static string? ResolveGitDir(string repoRoot)
        {
            var gitEntry = Path.Combine(repoRoot, ".git");

            if (Directory.Exists(gitEntry)) return gitEntry;

            if (File.Exists(gitEntry))
            {
                var text = File.ReadAllText(gitEntry).Trim();
                const string gitdirPrefix = "gitdir:";
                if (text.StartsWith(gitdirPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var target = text.Substring(gitdirPrefix.Length).Trim();
                    var combined = Path.GetFullPath(Path.IsPathRooted(target) ? target : Path.Combine(repoRoot, target));
                    if (Directory.Exists(combined)) return combined;
                }
            }

            return null;
        }

        private static string? ResolveHeadPath(string repoRoot)
        {
            var gitDir = ResolveGitDir(repoRoot);
            if (gitDir is null) return null;
            var head = Path.Combine(gitDir, "HEAD");
            return File.Exists(head) ? head : null;
        }

        private void EnsureWatcher(string repoPath, CacheEntry entry)
        {
            if (entry.HeadPath == null) return;

            // If we already have a watcher for this path, keep it.
            if (entry.Watcher != null) return;

            try
            {
                var dir = Path.GetDirectoryName(entry.HeadPath)!;
                var file = Path.GetFileName(entry.HeadPath)!;

                var fsw = new FileSystemWatcher(dir, file)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
                };

                fsw.Changed += (_, __) => OnHeadChanged(repoPath);
                fsw.Renamed += (_, __) => OnHeadChanged(repoPath);
                fsw.Deleted += (_, __) => OnHeadChanged(repoPath);

                fsw.EnableRaisingEvents = true;

                // Update the stored watcher
                _cache.AddOrUpdate(repoPath,
                    _ => entry with { Watcher = fsw },
                    (_, old) =>
                    {
                        old.Watcher?.Dispose();
                        return entry with { Watcher = fsw };
                    });
            }
            catch
            {
                // Best effort; if watcher fails (permissions), cache still works with timestamp check.
            }
        }

        private void OnHeadChanged(string repoPath)
        {
            // Invalidate timestamp so next Get will re-read
            if (_cache.TryGetValue(repoPath, out var entry))
            {
                _cache[repoPath] = entry with { HeadWriteTimeUtc = DateTime.MinValue };
            }
            BranchChanged?.Invoke(this, repoPath);
        }

        private static DateTime TryGetWriteTimeUtc(string path)
        {
            try { return File.GetLastWriteTimeUtc(path); }
            catch { return DateTime.MinValue; }
        }

        private static bool IsLikelySha(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            var t = s.Trim();
            if (t.Length < 7 || t.Length > 64) return false;
            foreach (var c in t)
            {
                var hex = (c >= '0' && c <= '9') ||
                          (c >= 'a' && c <= 'f') ||
                          (c >= 'A' && c <= 'F');
                if (!hex) return false;
            }
            return true;
        }

        private sealed record CacheEntry(string? Branch, string? HeadPath, DateTime HeadWriteTimeUtc)
        {
            public FileSystemWatcher? Watcher { get; init; }
        }
    }
}
