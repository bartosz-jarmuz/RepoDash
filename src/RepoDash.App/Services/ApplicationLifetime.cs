using RepoDash.App.Abstractions;

namespace RepoDash.App.Services;

public sealed class ApplicationLifetime : IApplicationLifetime
{
    public void Shutdown() => App.Current.Shutdown();
}
