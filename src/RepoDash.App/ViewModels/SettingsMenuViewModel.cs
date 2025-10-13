using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoDash.App.Abstractions;

namespace RepoDash.App.ViewModels;

public partial class SettingsMenuViewModel : ObservableObject
{

    private readonly ISettingsWindowService _windows;

    public SettingsMenuViewModel(ISettingsWindowService windows) => _windows = windows;

    [RelayCommand] private void OpenGeneralSettings() => _windows.ShowGeneral();
    [RelayCommand] private void OpenRepositoriesSettings() => _windows.ShowRepositories();
    [RelayCommand] private void OpenShortcutsSettings() => _windows.ShowShortcuts();
    [RelayCommand] private void OpenColorSettings() => _windows.ShowColors();
    [RelayCommand] private void OpenExternalToolsSettings() => _windows.ShowExternalTools();
}