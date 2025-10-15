using System.IO;
using System.IO.Pipes;
using Application = System.Windows.Application;

namespace RepoDash.App.Services;

public sealed class SingleInstanceService : IDisposable
{
    private readonly string _mutexName;
    private readonly string _pipeName;
    private readonly Action _onActivate;
    private readonly CancellationTokenSource _cts = new();
    private Mutex? _mutex;

    public SingleInstanceService(string key, Action onActivate)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key must be provided.", nameof(key));
        _mutexName = $"RepoDash_{key}_Mutex";
        _pipeName = $"RepoDash_{key}_Pipe";
        _onActivate = onActivate ?? throw new ArgumentNullException(nameof(onActivate));
    }

    public bool TryAcquirePrimary()
    {
        _mutex = new Mutex(true, _mutexName, out var createdNew);
        if (!createdNew) return false;

        _ = RunServerAsync(_cts.Token);
        return true;
    }

    public void SignalActivateExisting()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            client.Connect(250);
            using var writer = new StreamWriter(client);
            writer.WriteLine("ACTIVATE");
            writer.Flush();
        }
        catch
        {
            // best-effort
        }
    }

    private async Task RunServerAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);

                try
                {
                    using var reader = new StreamReader(server);
                    _ = await reader.ReadLineAsync().ConfigureAwait(false);
                }
                catch { }

                Application.Current?.Dispatcher.BeginInvoke(new Action(_onActivate));

                try { server.Disconnect(); } catch { }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(200, ct).ConfigureAwait(false);
            }
        }
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        try { _mutex?.ReleaseMutex(); } catch { }
        try { _mutex?.Dispose(); } catch { }
    }
}
