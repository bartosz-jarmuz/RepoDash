namespace RepoDash.App.Abstractions;

public interface ISettingsWindowService
{
    bool? ShowGeneral();
    bool? ShowRepositories();
    bool? ShowShortcuts();
    bool? ShowColors();
    bool? ShowExternalTools();
    bool? ShowBlacklistedItems();
}
