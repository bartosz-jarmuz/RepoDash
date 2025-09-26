using RepoDash.Core.Abstractions;

namespace RepoDash.Infrastructure.Remote;

public sealed class GitRemoteLinkProvider : IRemoteLinkProvider
{
    public bool TryGetProjectLinks(string remoteUrl, out Uri? repoPage, out Uri? pipelinesPage)
    {
        repoPage = null; pipelinesPage = null;
        if (string.IsNullOrWhiteSpace(remoteUrl)) return false;

        // Normalize: strip .git, convert ssh → https, drop credentials
        var url = Normalize(remoteUrl);
        if (url is null) return false;

        if (url.Host.Contains("gitlab", StringComparison.OrdinalIgnoreCase))
        {
            repoPage = url;
            pipelinesPage = new Uri(url, "/-/pipelines");
            return true;
        }

        if (url.Host.Contains("github", StringComparison.OrdinalIgnoreCase))
        {
            repoPage = url;
            pipelinesPage = new Uri(url, "/actions");
            return true;
        }

        return false;
    }

    private static Uri? Normalize(string remote)
    {
        // ssh: git@host:group/repo.git -> https://host/group/repo
        if (remote.Contains('@') && remote.Contains(':') && !remote.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            var at = remote.IndexOf('@');
            var colon = remote.IndexOf(':', at + 1);
            if (colon > at)
            {
                var host = remote.Substring(at + 1, colon - at - 1);
                var path = remote[(colon + 1)..].TrimStart('/');
                if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) path = path[..^4];
                return new Uri($"https://{host}/{path}");
            }
        }

        // http(s)
        if (Uri.TryCreate(remote, UriKind.Absolute, out var uri))
        {
            var builder = new UriBuilder(uri) { UserName = string.Empty, Password = string.Empty };
            var path = builder.Path;
            if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                builder.Path = path[..^4];
            return builder.Uri;
        }

        return null;
    }
}
