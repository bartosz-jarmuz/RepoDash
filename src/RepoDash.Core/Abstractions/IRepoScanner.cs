using RepoDash.Core.Abstractions;

public interface IRepoScanner
{
    /// <summary>
    /// Streams repositories discovered under <paramref name="rootPath"/>.
    /// Items are yielded as soon as they are found. The <paramref name="groupingSegment"/>
    /// behavior remains identical to previous implementation and affects GroupKey.
    /// </summary>
    IAsyncEnumerable<RepoInfo> ScanAsync(string rootPath, int groupingSegment, CancellationToken ct);
}