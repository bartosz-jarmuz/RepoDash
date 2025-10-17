namespace RepoDash.Core.Usage;

public sealed record RepoBlacklistItem
{
    public required string RepoName { get; init; }
    public required string RepoPath { get; init; }
}
