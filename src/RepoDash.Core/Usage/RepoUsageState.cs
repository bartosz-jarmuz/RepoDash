namespace RepoDash.Core.Usage;

public sealed record RepoUsageState
{
    public List<RepoUsageEntry> Entries { get; set; } = [];
    public List<string> PinnedPaths { get; set; } = [];
    public List<string> PinnedNames { get; set; } = [];
    public List<string> BlacklistedPaths { get; set; } = [];
    public List<string> BlacklistedNames { get; set; } = [];
    public List<RepoBlacklistItem> BlacklistedItems { get; set; } = [];
};
