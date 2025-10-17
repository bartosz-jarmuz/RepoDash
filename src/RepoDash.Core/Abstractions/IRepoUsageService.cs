using RepoDash.Core.Usage;

namespace RepoDash.Core.Abstractions;

public interface IRepoUsageService
{
    event EventHandler? Changed;

    void RecordUsage(RepoUsageSnapshot snapshot);

    IReadOnlyList<RepoUsageEntry> GetRecent(int maxCount);

    IReadOnlyList<RepoUsageSummary> GetFrequent(int maxCount);

    IReadOnlyList<RepoBlacklistItem> GetBlacklistedItems();

    bool TogglePinned(string repoName, string repoPath);

    bool ToggleBlacklisted(string repoName, string repoPath);

    bool IsPinned(string repoName, string repoPath);

    bool IsBlacklisted(string repoName, string repoPath);
}
