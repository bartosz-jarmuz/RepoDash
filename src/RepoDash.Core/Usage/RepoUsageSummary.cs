namespace RepoDash.Core.Usage;

public sealed record RepoUsageSummary
{
    public required string RepoName { get; init; }
    public int UsageCount { get; init; }
    public DateTimeOffset LastUsedUtc { get; init; }
};