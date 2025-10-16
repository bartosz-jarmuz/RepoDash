using System;
using System.IO;

namespace RepoDash.Persistence.Paths;

public static class AppPaths
{
    public static string Root =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RepoDash");

    public static string SettingsDir => Path.Combine(Root, "settings");

    public static string CacheDir => Path.Combine(Root, "cache");

    public static string CachePathForRoot(string normalizedRoot) => Path.Combine(CacheDir, $"cache.{normalizedRoot}.json");

    public static string UsageDir => Path.Combine(Root, "usage");

    public static string UsageFile => Path.Combine(UsageDir, "repo-usage.json");
}
