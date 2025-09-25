using System;
using System.Collections.Generic;

namespace RepoDash.Core.Models;

public sealed record RepoCacheSnapshot(
    string RootPath,
    IReadOnlyList<RepoDescriptor> Repositories,
    DateTimeOffset CachedAtUtc)
{
    public static RepoCacheSnapshot Empty(string rootPath) => new(rootPath, Array.Empty<RepoDescriptor>(), DateTimeOffset.MinValue);
}
