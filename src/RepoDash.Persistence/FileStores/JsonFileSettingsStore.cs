using RepoDash.Core.Abstractions;
using RepoDash.Persistence.Paths;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RepoDash.Persistence.FileStores;

public sealed class JsonFileSettingsStore<TSettings> : ISettingsStore<TSettings>, IDisposable
    where TSettings : class, new()
{
    private readonly object _gate = new();
    private readonly JsonSerializerOptions _json;
    private readonly string _settingsDir;
    private readonly string _filePath;
    private readonly FileSystemWatcher? _watcher;
    private readonly SynchronizationContext? _syncCtx;

    public TSettings Current { get; private set; }

    public event EventHandler? SettingsChanged;

    /// <param name="fileName">Defaults by TSettings type (e.g., GeneralSettings → general.json)</param>
    /// <param name="watchForExternalChanges">If true, will auto-reload when file changes.</param>
    /// <param name="jsonOptions">Optional custom JsonSerializerOptions; sensible defaults applied if null.</param>
    public JsonFileSettingsStore(
        string? fileName = null,
        bool watchForExternalChanges = true,
        JsonSerializerOptions? jsonOptions = null)
    {
        _syncCtx = SynchronizationContext.Current;

        _settingsDir = AppPaths.SettingsDir;

        Directory.CreateDirectory(_settingsDir);

        _filePath = Path.Combine(_settingsDir, fileName ?? GetDefaultFileName());

        _json = jsonOptions ?? CreateDefaultJsonOptions();

        Current = LoadOrCreate(_filePath, _json);

        if (watchForExternalChanges)
        {
            _watcher = new FileSystemWatcher(_settingsDir, Path.GetFileName(_filePath))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
            };
            _watcher.Changed += (_, __) => DebouncedReload();
            _watcher.Created += (_, __) => DebouncedReload();
            _watcher.Renamed += (_, __) => DebouncedReload();
            _watcher.EnableRaisingEvents = true;
        }
    }

    public async Task UpdateAsync(Action<TSettings>? mutate = null, CancellationToken ct = default)
    {
        lock (_gate)
        {
            mutate?.Invoke(Current);
        }

        await SaveAsync(ct).ConfigureAwait(false);
        RaiseChanged();
    }

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        var reloaded = await LoadFromDiskAsync(ct).ConfigureAwait(false);
        lock (_gate)
        {
            Current = reloaded;
        }
        RaiseChanged();
    }

    private static TSettings LoadOrCreate(string path, JsonSerializerOptions json)
    {
        try
        {
            if (File.Exists(path))
            {
                using var fs = File.OpenRead(path);
                var obj = JsonSerializer.Deserialize<TSettings>(fs, json);
                if (obj is not null) return obj;
            }
        }
        catch { /* fall back to defaults */ }

        var def = new TSettings();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(def, json), Encoding.UTF8);
        }
        catch { /* ignore */ }
        return def;
    }

    private async Task<TSettings> LoadFromDiskAsync(CancellationToken ct)
    {
        try
        {
            await using var fs = File.Open(_filePath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);
            var obj = await JsonSerializer.DeserializeAsync<TSettings>(fs, _json, ct).ConfigureAwait(false);
            return obj ?? new TSettings();
        }
        catch
        {
            return new TSettings();
        }
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        var tmp = _filePath + ".tmp";
        string json;
        lock (_gate)
        {
            json = JsonSerializer.Serialize(Current, _json);
        }
        await File.WriteAllTextAsync(tmp, json, Encoding.UTF8, ct).ConfigureAwait(false);

        try
        {
            if (File.Exists(_filePath))
                File.Replace(tmp, _filePath, null);
            else
                File.Move(tmp, _filePath);
        }
        catch
        {
            // Fallback if Replace not available
            if (File.Exists(_filePath)) File.Delete(_filePath);
            File.Move(tmp, _filePath);
        }
    }

    private static JsonSerializerOptions CreateDefaultJsonOptions()
    {
        var opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
        opts.Converters.Add(new JsonStringEnumConverter());
        return opts;
    }

    private static string GetDefaultFileName()
    {
        var type = typeof(TSettings).Name;
        // Map common names to nice filenames; fallback to lowercase typename
        return type.ToLowerInvariant() switch
        {
            "generalsettings" => "general.json",
            "repositoriessettings" => "repositories.json",
            "shortcutssettings" => "shortcuts.json",
            "colorsettings" => "colors.json",
            "externaltoolssettings" => "externaltools.json",
            _ => $"{type.ToLowerInvariant()}.json"
        };
    }

    private void RaiseChanged()
    {
        void Fire() => SettingsChanged?.Invoke(this, EventArgs.Empty);
        if (_syncCtx is not null) _syncCtx.Post(_ => Fire(), null);
        else Fire();
    }

    private int _reloading;
    private void DebouncedReload()
    {
        if (Interlocked.Exchange(ref _reloading, 1) == 1) return;
        _ = Task.Delay(250).ContinueWith(async _ =>
        {
            try { await ReloadAsync().ConfigureAwait(false); }
            finally { Interlocked.Exchange(ref _reloading, 0); }
        });
    }

    public void Dispose() => _watcher?.Dispose();

  
}