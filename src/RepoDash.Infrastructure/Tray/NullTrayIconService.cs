using RepoDash.Core.Abstractions;

namespace RepoDash.Infrastructure.Tray;

public sealed class NullTrayIconService : ITrayIconService
{
    public void Initialize(string tooltip, Action onLeftClick)
    {
    }

    public void ShowNotification(string title, string message)
    {
    }

    public void Dispose()
    {
    }
}
