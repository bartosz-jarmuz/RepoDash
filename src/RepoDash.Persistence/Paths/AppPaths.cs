namespace RepoDash.Persistence.Paths;

public static class AppPaths
{
    public static string Root =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RepoDash");

    public static string SettingsDir => Path.Combine(Root, "settings");
    public static string CacheDir => Path.Combine(Root, "cache");

    public static string GeneralSettingsPath => Path.Combine(SettingsDir, "settings.general.json");
    public static string ReposSettingsPath => Path.Combine(SettingsDir, "settings.repositories.json");
    public static string UsagePath => Path.Combine(Root, "usage.json");
    public static string CachePathForRoot(string normalizedRoot) => Path.Combine(CacheDir, $"cache.{normalizedRoot}.json");
}