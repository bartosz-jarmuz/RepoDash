using System;

namespace RepoDash.Core.Abstractions;

public interface IGlobalHotkeyService : IDisposable
{
    void Register(string hotkeyGesture, Action callback);
    void UnregisterAll();
}
