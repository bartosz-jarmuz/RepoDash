namespace RepoDash.Core.Models;

public sealed record RepoSnapshot(RepoDescriptor Descriptor, RepoStatus Status, RepoUsageEntry Usage)
{
    public RepoIdentifier Identifier => Descriptor.Identifier;
}
