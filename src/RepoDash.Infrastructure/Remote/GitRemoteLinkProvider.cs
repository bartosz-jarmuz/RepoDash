using RepoDash.Core.Abstractions;
using RepoDash.Core.Settings;

namespace RepoDash.Infrastructure.Remote;

public sealed class GitRemoteLinkProvider : IRemoteLinkProvider
{
    private readonly ISettingsStore<GeneralSettings> _settings;

    public GitRemoteLinkProvider(ISettingsStore<GeneralSettings> settings)
    {
        _settings = settings;
    }

    public bool TryGetProjectLinks(string remoteUrl, out Uri? repoPage, out Uri? pipelinesPage)
    {
        repoPage = null; pipelinesPage = null;
        if (string.IsNullOrWhiteSpace(remoteUrl)) return false;

        // Normalize: strip .git, convert ssh → https, drop credentials
        var url = Normalize(remoteUrl);
        if (url is null) return false;

        // Build pipelines URL using the configurable setting rather than hostname heuristics
        var part = _settings.Current.RemotePipelinesUrlPart?.Trim();
        if (string.IsNullOrEmpty(part)) part = "/-/pipelines";
        if (!part.StartsWith("/")) part = "/" + part;

        repoPage = url;
        pipelinesPage = new Uri(url + part);
        return true;
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
