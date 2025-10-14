using System.Text.Json;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Caching;
using RepoDash.Persistence.Paths;

namespace RepoDash.Persistence.FileStores;

public sealed class JsonRepoCacheStore : IRepoCacheStore
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<RepoRootCache?> ReadAsync(string normalizedRootPath, CancellationToken ct)
    {
        var file = AppPaths.CachePathForRoot(normalizedRootPath);
        if (!File.Exists(file)) return null;

        await using var fs = File.OpenRead(file);
        return await System.Text.Json.JsonSerializer.DeserializeAsync<RepoRootCache>(fs, Json, ct);
    }

    public async Task WriteAsync(string normalizedRootPath, RepoRootCache cache, CancellationToken ct)
    {
        Directory.CreateDirectory(AppPaths.CacheDir);
        var file = AppPaths.CachePathForRoot(normalizedRootPath);
        await using var fs = File.Create(file);
        await System.Text.Json.JsonSerializer.SerializeAsync(fs, cache, Json, ct);
    }
}