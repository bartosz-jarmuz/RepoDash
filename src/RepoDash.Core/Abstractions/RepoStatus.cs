namespace RepoDash.Core.Abstractions;

public sealed record RepoStatus(string CurrentBranch, BranchSyncState SyncState, bool IsDirty);