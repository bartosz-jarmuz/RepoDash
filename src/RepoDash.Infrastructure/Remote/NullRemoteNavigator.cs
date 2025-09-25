using System;
using RepoDash.Core.Abstractions;

namespace RepoDash.Infrastructure.Remote;

public sealed class NullRemoteNavigator : IRemoteNavigator
{
    public Uri? BuildRepositoryUri(string? remoteUrl) => null;
    public Uri? BuildPipelinesUri(string? remoteUrl) => null;
}
