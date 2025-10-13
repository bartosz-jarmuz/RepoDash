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

    public Task InvokeAsync(Func<Task> actionAsync)
    {
        if (actionAsync is null) throw new ArgumentNullException(nameof(actionAsync));
        if (CheckAccess()) return actionAsync();

        var tcs = new TaskCompletionSource<object?>();
        _dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                await actionAsync().ConfigureAwait(false);
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }));
        return tcs.Task;
    }
}