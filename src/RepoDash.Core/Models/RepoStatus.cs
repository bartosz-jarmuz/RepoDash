using System;

namespace RepoDash.Core.Models;

public enum RepoBranchRelation
{
    Unknown = 0,
    UpToDate,
    Ahead,
    Behind,
    Diverged
}

public sealed record RepoStatus(
    string? BranchName,
    RepoBranchRelation Relation,
    bool IsDirty,
    DateTimeOffset RetrievedAt)
{
    public static RepoStatus Unknown { get; } = new(null, RepoBranchRelation.Unknown, false, DateTimeOffset.MinValue);
}
