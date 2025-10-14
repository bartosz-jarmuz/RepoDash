namespace RepoDash.Core.Caching;

public sealed class CachedRepo
{
    public string RepoName { get; init; } = string.Empty;
    public string RepoPath { get; init; } = string.Empty;
    public bool HasGit { get; init; }
    public bool HasSolution { get; init; }
    public string? SolutionPath { get; init; }
    public string GroupKey { get; init; } = string.Empty;

    public string Signature { get; init; } = string.Empty;           // change detector (HEAD + .sln times)
    public DateTimeOffset LastSeenUtc { get; init; }
}