using RepoDash.Core.Abstractions;
using RepoDash.Core.Settings;

namespace RepoDash.Infrastructure.Scanning;

public sealed class FileSystemRepoScanner : IRepoScanner
{
    private readonly ISettingsStore<RepositoriesSettings> _reposSettings;

    public FileSystemRepoScanner(ISettingsStore<RepositoriesSettings> reposSettings)
        => _reposSettings = reposSettings;

    public async Task<IReadOnlyList<RepoInfo>> ScanAsync(string rootPath, int groupingSegment, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            return Array.Empty<RepoInfo>();

        var repos = new List<RepoInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var settings = _reposSettings.Current ?? new RepositoriesSettings();
        var ignored = settings.ExcludedPathParts?
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToArray() ?? Array.Empty<string>();
        var overrides = settings.CategoryOverrides?.ToArray() ?? Array.Empty<CategoryOverride>();

        // Discover repos strictly by the presence of a ".git" directory (any depth).
        foreach (var gitDir in Directory.EnumerateDirectories(rootPath, ".git", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();

            var repoPath = Directory.GetParent(gitDir)!.FullName;
            if (!seen.Add(repoPath)) continue;

            // Optional: locate a solution (not required for discovery; used only for overrides/filtering)
            string? sln = Directory.EnumerateFiles(repoPath, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault()
                       ?? Directory.EnumerateFiles(repoPath, "*.sln", SearchOption.AllDirectories).FirstOrDefault();

            // Apply ignored fragments to repo path and (if found) solution path
            if (ContainsAnyFragment(repoPath, ignored) || ContainsAnyFragment(sln ?? string.Empty, ignored))
                continue;

            var name = Path.GetFileName(repoPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var groupKey = ComputeGroupKey(rootPath, repoPath, groupingSegment);

            // Apply category overrides (match on repo path, solution path, or solution file name)
            groupKey = ApplyCategoryOverrides(groupKey, repoPath, sln, overrides);

            repos.Add(new RepoInfo(
                RepoName: name,
                RepoPath: repoPath,
                HasGit: true,
                HasSolution: sln != null,
                SolutionPath: sln,
                GroupKey: groupKey));
        }

        return await Task.FromResult<IReadOnlyList<RepoInfo>>(
            repos.OrderBy(r => r.GroupKey, StringComparer.OrdinalIgnoreCase)
                 .ThenBy(r => r.RepoName, StringComparer.OrdinalIgnoreCase)
                 .ToList());
    }

    private static string ComputeGroupKey(string root, string repoPath, int groupingSegment)
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

    private static bool ContainsAnyFragment(string haystack, IEnumerable<string> fragments)
    {
        if (string.IsNullOrEmpty(haystack)) return false;
        foreach (var raw in fragments)
        {
            var f = raw?.Trim();
            if (string.IsNullOrEmpty(f)) continue;
            if (haystack.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    private static string ApplyCategoryOverrides(string currentGroupKey, string repoPath, string? solutionPath, IEnumerable<CategoryOverride> rules)
    {
        if (rules == null) return currentGroupKey;
        var solName = string.IsNullOrEmpty(solutionPath) ? string.Empty : Path.GetFileName(solutionPath);
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
}