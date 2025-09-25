using RepoDash.Core.Abstractions;

namespace RepoDash.Infrastructure.Hotkeys;

public sealed class NullGlobalHotkeyService : IGlobalHotkeyService
{
    public void Register(string hotkeyGesture, Action callback)
    {
        // No-op placeholder.
    }

    public void UnregisterAll()
    {
    }

    public void Dispose()
    {
    }
}
