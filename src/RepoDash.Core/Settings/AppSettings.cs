namespace RepoDash.Core.Settings;

public sealed class AppSettings
{
    public GeneralSettings General { get; set; } = new();
    public RepositoriesSettings Repositories { get; set; } = new();
    public ShortcutsSettings Shortcuts { get; set; } = new();
    public ColorSettings Colors { get; set; } = new();
    public ExternalToolsSettings ExternalTools { get; set; } = new();
}