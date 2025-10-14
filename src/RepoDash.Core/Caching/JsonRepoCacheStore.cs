using RepoDash.Core.Abstractions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RepoDash.Core.Caching;

public sealed class JsonRepoCacheStore : IRepoCacheStore
{
    static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<RepoRootCache?> ReadAsync(string rootPath, CancellationToken ct)
    {
        var file = GetCacheFile(rootPath);
        if (!File.Exists(file)) return null;

        await using var fs = File.OpenRead(file);
        return await JsonSerializer.DeserializeAsync<RepoRootCache>(fs, _json, ct);
    }

    public async Task WriteAsync(string rootPath, RepoRootCache cache, CancellationToken ct)
    {
        var file = GetCacheFile(rootPath);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);

        await using var fs = File.Create(file);
        await JsonSerializer.SerializeAsync(fs, cache, _json, ct);
    }

    static string GetCacheFile(string rootPath)
    {
        var app = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(app, "RepoDash", "cache");
        var hash = Sha1(rootPath);
        return Path.Combine(dir, $"{hash}.json");
    }

    static string Sha1(string input)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}