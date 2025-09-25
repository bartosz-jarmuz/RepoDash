using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Models;
using RepoDash.Persistence.Paths;

namespace RepoDash.Persistence.FileStores;

public sealed class RepoCacheFileStore : IRepoCacheService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public async Task<RepoCacheSnapshot?> LoadAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        var normalized = PathNormalizer.NormalizeRootName(rootPath);
        var filePath = AppPaths.GetCacheFile(normalized);
        if (!File.Exists(filePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<RepoCacheSnapshot>(stream, Options, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsync(RepoCacheSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var normalized = PathNormalizer.NormalizeRootName(snapshot.RootPath);
        var filePath = AppPaths.GetCacheFile(normalized);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, snapshot, Options, cancellationToken).ConfigureAwait(false);
    }
}
