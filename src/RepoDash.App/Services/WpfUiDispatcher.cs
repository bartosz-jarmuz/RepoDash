using RepoDash.App.Abstractions;
using System.Windows.Threading;

namespace RepoDash.App.Services;

public sealed class WpfUiDispatcher : IUiDispatcher
{
    private readonly Dispatcher _dispatcher;

    public WpfUiDispatcher(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public bool CheckAccess() => _dispatcher.CheckAccess();

    public void Invoke(Action action)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));
        if (CheckAccess()) action();
        else _dispatcher.Invoke(action);
    }
}