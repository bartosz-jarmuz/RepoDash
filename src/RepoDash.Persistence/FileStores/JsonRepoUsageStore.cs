using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Usage;
using RepoDash.Persistence.Paths;

namespace RepoDash.Persistence.FileStores;

public sealed class JsonRepoUsageStore : IRepoUsageStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<RepoUsageState> ReadAsync(CancellationToken ct)
    {
        var file = AppPaths.UsageFile;
        if (!File.Exists(file)) return new RepoUsageState();

        await using var fs = File.OpenRead(file);
        var state = await System.Text.Json.JsonSerializer.DeserializeAsync<RepoUsageState>(fs, Json, ct)
            .ConfigureAwait(false);
        return state ?? new RepoUsageState();
    }

    public async Task WriteAsync(RepoUsageState state, CancellationToken ct)
    {
        Directory.CreateDirectory(AppPaths.UsageDir);
        var file = AppPaths.UsageFile;
        var tmp = file + ".tmp";

        await using (var fs = File.Create(tmp))
        {
            await System.Text.Json.JsonSerializer.SerializeAsync(fs, state, Json, ct).ConfigureAwait(false);
        }

        try
        {
            File.Move(tmp, file, overwrite: true);
        }
        catch
        {
            try
            {
                if (File.Exists(file)) File.Delete(file);
                File.Move(tmp, file);
            }
            catch
            {
                // best effort
            }
        }
    }
}
