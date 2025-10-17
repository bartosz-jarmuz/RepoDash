namespace RepoDash.Core.Usage;

public sealed record RepoUsageEntry
{
    public required string RepoName { get; init; }
    public required string RepoPath { get; init; }
    public bool HasGit { get; init; }
    public bool HasSolution { get; init; }
    public string? SolutionPath { get; init; }
    public DateTimeOffset LastUsedUtc { get; init; }
    public int UsageCount { get; init; }
};