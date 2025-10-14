namespace RepoDash.Core.Abstractions;

/// <summary>
/// Provides the current Git branch for a given repository path.
/// Implementations should be lightweight, cache results, and avoid opening
/// the git object database (use HEAD + refs).
/// </summary>
public interface IBranchProvider
{
    /// <summary>
    /// Returns the current branch name (e.g., "master"), "(detached)" if detached, or null if unknown.
    /// Should be fast and non-throwing; failures should return null.
    /// </summary>
    Task<string?> GetCurrentBranchAsync(string repoPath, CancellationToken ct);

    /// <summary>
    /// Returns a cached branch value if available, or null.
    /// </summary>
    string? TryGetCached(string repoPath);

    /// <summary>
    /// Notifies when a repo's branch likely changed (e.g., HEAD modified).
    /// The event arg is the repoPath.
    /// </summary>
    event EventHandler<string>? BranchChanged;
}