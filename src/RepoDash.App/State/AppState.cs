using RepoDash.Core.Settings;

namespace RepoDash.App.State;

public sealed class AppState
{
    public GeneralSettings GeneralSettings { get; set; } = new();
    public RepositoriesSettings RepositoriesSettings { get; set; } = new();
    public ShortcutsSettings ShortcutsSettings { get; set; } = new();
    public ColorRulesSettings ColorRulesSettings { get; set; } = new();
    public ExternalToolSettings ExternalToolSettings { get; set; } = new();
    public StatusPollingSettings StatusPollingSettings { get; set; } = new();
}
