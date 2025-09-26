namespace RepoDash.Core.Abstractions;

public sealed record RepoInfo(
    string RepoName,
    string RepoPath,
    bool HasGit,
    bool HasSolution,
    string? SolutionPath,
    string GroupKey);