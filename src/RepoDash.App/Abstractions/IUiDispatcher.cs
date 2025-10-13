namespace RepoDash.App.Abstractions;

public interface IUiDispatcher
{
    bool CheckAccess();
    void Invoke(System.Action action);
    System.Threading.Tasks.Task InvokeAsync(System.Func<System.Threading.Tasks.Task> actionAsync);
}