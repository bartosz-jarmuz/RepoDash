using System;

namespace RepoDash.Core.Abstractions;

public interface ITrayIconService : IDisposable
{
    void Initialize(string tooltip, Action onLeftClick);
    void ShowNotification(string title, string message);
}
