namespace RepoDash.Core.Usage;

public sealed record RepoUsageSnapshot
{
    public required string RepoName { get; init; }
    public required string RepoPath { get; init; }
    public bool HasGit { get; init; }
    public bool HasSolution { get; init; }
    public string? SolutionPath { get; init; }
};