using System.Text.Json;
using System.Text.Json.Serialization;

namespace RepoDash.Persistence.FileStores;

public static class JsonStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public static async Task<T> LoadOrDefaultAsync<T>(string path, Func<T> @default, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (!File.Exists(path)) return @default();
        await using var fs = File.OpenRead(path);
        return (await JsonSerializer.DeserializeAsync<T>(fs, Options, ct)) ?? @default();
    }

    public static async Task SaveAsync<T>(string path, T value, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var fs = File.Create(path);
        await JsonSerializer.SerializeAsync(fs, value, Options, ct);
    }
}