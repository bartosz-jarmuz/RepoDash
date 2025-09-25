using System;
using System.Collections.Generic;

namespace RepoDash.Core.Models;

public sealed record RepoDescriptor(
    RepoIdentifier Identifier,
    string RepositoryPath,
    string? SolutionPath,
    bool HasGit,
    bool HasSolution,
    string GroupKey,
    string Category,
    IReadOnlyList<string> Tags,
    string? RemoteUrl)
{
    public bool MatchesSearch(string query) => string.IsNullOrWhiteSpace(query)
        || RepositoryPath.Contains(query, StringComparison.OrdinalIgnoreCase)
        || Identifier.Name.Contains(query, StringComparison.OrdinalIgnoreCase);
}
