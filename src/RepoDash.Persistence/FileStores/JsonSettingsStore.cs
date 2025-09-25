using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RepoDash.Core.Abstractions;

namespace RepoDash.Persistence.FileStores;

public sealed class JsonSettingsStore<T> : ISettingsStore<T> where T : class, new()
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly string _filePath;

    public JsonSettingsStore(string filePath)
    {
        _filePath = filePath;
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
    }

    public async Task<T> LoadAsync(CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath))
            {
                return new T();
            }

            await using var stream = File.OpenRead(_filePath);
            var payload = await JsonSerializer.DeserializeAsync<T>(stream, Options, cancellationToken).ConfigureAwait(false);
            return payload ?? new T();
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task SaveAsync(T settings, CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var stream = File.Create(_filePath);
            await JsonSerializer.SerializeAsync(stream, settings, Options, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Gate.Release();
        }
    }
}
