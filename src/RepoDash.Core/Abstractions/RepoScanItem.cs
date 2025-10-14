namespace RepoDash.Core.Abstractions;

public sealed class RepoScanItem
{
    public string RepoName { get; init; } = string.Empty;
    public string RepoPath { get; init; } = string.Empty;
    public string? SolutionPath { get; init; }
    public string? RemoteUrl { get; init; }
}