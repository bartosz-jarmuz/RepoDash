using System;

namespace RepoDash.Core.Abstractions;

public interface IRemoteNavigator
{
    Uri? BuildRepositoryUri(string? remoteUrl);
    Uri? BuildPipelinesUri(string? remoteUrl);
}
