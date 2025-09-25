using System;
using System.Text.RegularExpressions;
using RepoDash.Core.Abstractions;

namespace RepoDash.Infrastructure.Remote;

public sealed class GitRemoteNavigator : IRemoteNavigator
{
    public Uri? BuildRepositoryUri(string? remoteUrl)
    {
        var normalized = Normalize(remoteUrl);
        return normalized is null ? null : new Uri(normalized);
    }

    public Uri? BuildPipelinesUri(string? remoteUrl)
    {
        var normalized = Normalize(remoteUrl);
        if (normalized is null)
        {
            return null;
        }

        if (normalized.Contains("github", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(normalized + "/actions");
        }

        return new Uri(normalized.TrimEnd('/') + "/-/pipelines");
    }

    private static string? Normalize(string? remoteUrl)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl))
        {
            return null;
        }

        if (remoteUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return TrimGitSuffix(remoteUrl);
        }

        // ssh format git@host:group/project.git
        var match = Regex.Match(remoteUrl, "git@(?<host>[^:]+):(?<path>.+)");
        if (!match.Success)
        {
            return null;
        }

        var host = match.Groups["host"].Value;
        var path = match.Groups["path"].Value;
        return $"https://{host}/{TrimGitSuffix(path)}";
    }

    private static string TrimGitSuffix(string value)
    {
        return value.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? value[..^4]
            : value;
    }
}
