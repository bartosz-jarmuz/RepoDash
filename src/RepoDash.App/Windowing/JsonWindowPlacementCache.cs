using System.IO;
using System.Text.Json;

namespace RepoDash.App.Windowing;

public sealed class JsonWindowPlacementCache : IWindowPlacementCache
{
    private readonly string _path;
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    public JsonWindowPlacementCache()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RepoDash", "Cache");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "window.json");
    }

    public WindowPlacementCache Load()
    {
        if (!File.Exists(_path))
        {
            return new WindowPlacementCache();
        }

        using var fs = File.OpenRead(_path);
        return JsonSerializer.Deserialize<WindowPlacementCache>(fs, _json) ?? new WindowPlacementCache();
    }

    public void Save(WindowPlacementCache cache)
    {
        using var fs = File.Create(_path);
        JsonSerializer.Serialize(fs, cache, _json);
    }
}