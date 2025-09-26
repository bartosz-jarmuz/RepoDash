using RepoDash.Core.Abstractions;

namespace RepoDash.Infrastructure.Scanning;

public sealed class FileSystemRepoScanner : IRepoScanner
{
    public async Task<IReadOnlyList<RepoInfo>> ScanAsync(string rootPath, int groupingSegment, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            return Array.Empty<RepoInfo>();

        var repos = new List<RepoInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Find all ".git" directories under root (recursively), each defines one repo at its parent folder.
        foreach (var gitDir in Directory.EnumerateDirectories(rootPath, ".git", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();

            var repoPath = Directory.GetParent(gitDir)!.FullName;
            if (!seen.Add(repoPath)) continue;

            var name = Path.GetFileName(repoPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            // First try a top-level .sln; if none, the first anywhere under the repo.
            string? sln = Directory.EnumerateFiles(repoPath, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault()
                       ?? Directory.EnumerateFiles(repoPath, "*.sln", SearchOption.AllDirectories).FirstOrDefault();

            var groupKey = ComputeGroupKey(rootPath, repoPath, groupingSegment);

            repos.Add(new RepoInfo(
                RepoName: name,
                RepoPath: repoPath,
                HasGit: true,
                HasSolution: sln != null,
                SolutionPath: sln,
                GroupKey: groupKey));
        }

        // Some folders may be useful even without .git (e.g., SQL repos) — include top-level non-git folders as “repos”.
        foreach (var dir in Directory.EnumerateDirectories(rootPath))
        {
            if (seen.Contains(dir)) continue;
            var name = Path.GetFileName(dir);
            var sln = Directory.EnumerateFiles(dir, "*.sln", SearchOption.AllDirectories).FirstOrDefault();
            var groupKey = ComputeGroupKey(rootPath, dir, groupingSegment);
            repos.Add(new RepoInfo(name, dir, HasGit: false, HasSolution: sln != null, sln, groupKey));
        }

        return await Task.FromResult<IReadOnlyList<RepoInfo>>(repos
            .OrderBy(r => r.GroupKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.RepoName, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    private static string ComputeGroupKey(string root, string repoPath, int groupingSegment)
    {
        // Compute based on path segments RELATIVE TO ROOT
        // Example: root = C:\dev\git2
        // repo   = C:\dev\git2\tv2-core\integration-tests\V2IntegrationTests
        // segments relative: [tv2-core, integration-tests, V2IntegrationTests]
        // groupingSegment=2 -> "integration-tests"
        var relative = Path.GetRelativePath(root, repoPath);
        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                            .Where(p => !string.IsNullOrWhiteSpace(p))
                            .ToArray();

        if (parts.Length == 0) return Path.GetFileName(repoPath);
        if (groupingSegment <= 0) return parts[0]; // default to first-level folder

        var idxFromEnd = parts.Length - groupingSegment;
        if (idxFromEnd >= 0 && idxFromEnd < parts.Length)
            return parts[idxFromEnd];

        // Fallback: last segment before repo
        return parts.Length > 1 ? parts[^2] : parts[0];
    }
}
