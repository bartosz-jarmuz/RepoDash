namespace RepoDash.Core.Abstractions;

public interface IRepoScanner
{
    /// <summary>Scans immediate subfolders of <paramref name="rootPath"/> for repos (presence of ".git").</summary>
    Task<IReadOnlyList<RepoInfo>> ScanAsync(string rootPath, int groupingSegment, CancellationToken ct);
}
