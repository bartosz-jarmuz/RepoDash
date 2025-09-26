using RepoDash.Core.Abstractions;

namespace RepoDash.Core.NullObjects;

public sealed class NullRemoteLinkProvider : IRemoteLinkProvider
{
    public bool TryGetProjectLinks(string remoteUrl, out Uri? repoPage, out Uri? pipelinesPage)
    { repoPage = pipelinesPage = null; return false; }
}