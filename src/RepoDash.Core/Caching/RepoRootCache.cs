namespace RepoDash.Core.Caching;

public sealed class RepoRootCache
{
    public string NormalizedRoot { get; init; } = string.Empty;
    public DateTimeOffset CachedAtUtc { get; set; }
    public List<CachedRepo> Repos { get; set; } = [];
}