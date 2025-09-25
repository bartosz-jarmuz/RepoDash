using System;
using System.IO;

namespace RepoDash.Persistence.Paths;

public static class AppPaths
{
    private const string RootFolderName = "RepoDash";
    private static readonly Lazy<string> DocumentsRootLazy = new(() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), RootFolderName));

    public static string Root => DocumentsRootLazy.Value;
    public static string Settings => EnsureAndCombine("Settings");
    public static string Cache => EnsureAndCombine("Cache");
    public static string Usage => EnsureAndCombine("Usage");
    public static string Logs => EnsureAndCombine("Logs");

    public static string GetCacheFile(string normalizedRoot) => Path.Combine(Cache, $"cache.{normalizedRoot}.json");
    public static string GetSettingsFile(string name) => Path.Combine(Settings, $"settings.{name}.json");
    public static string UsageFile => Path.Combine(Usage, "usage.json");

    private static string EnsureAndCombine(string folderName)
    {
        var path = Path.Combine(Root, folderName);
        Directory.CreateDirectory(path);
        return path;
    }
}
