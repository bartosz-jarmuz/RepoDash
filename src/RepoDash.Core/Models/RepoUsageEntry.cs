using System;

namespace RepoDash.Core.Models;

public sealed record RepoUsageEntry(
    RepoIdentifier Identifier,
    int LaunchCount,
    DateTimeOffset? LastLaunchedAt,
    bool IsPinned)
{
    public static RepoUsageEntry Empty(RepoIdentifier id) => new(id, 0, null, false);
}
