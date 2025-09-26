namespace RepoDash.Core.Abstractions;

public interface IRemoteLinkProvider
{
    bool TryGetProjectLinks(string remoteUrl, out Uri? repoPage, out Uri? pipelinesPage);
}
