using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoDash.App.Abstractions;
using RepoDash.Core.Abstractions;
using RepoDash.Persistence.Paths;

namespace RepoDash.App.ViewModels;

public partial class MainMenuViewModel : ObservableObject
{
    private readonly IApplicationLifetime _applicationLifetime;
    private readonly IAboutWindowService _aboutWindowService;
    private readonly ILauncher _launcher;

    public MainMenuViewModel(
        SettingsMenuViewModel settings,
        GlobalGitOperationsMenuViewModel gitOperations,
        IApplicationLifetime applicationLifetime,
        IAboutWindowService aboutWindowService,
        ILauncher launcher)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Git = gitOperations ?? throw new ArgumentNullException(nameof(gitOperations));
        _applicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
        _aboutWindowService = aboutWindowService ?? throw new ArgumentNullException(nameof(aboutWindowService));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
    }

    public SettingsMenuViewModel Settings { get; }
    public GlobalGitOperationsMenuViewModel Git { get; }

    [RelayCommand]
    private void Exit() => _applicationLifetime.Shutdown();

    [RelayCommand]
    private void ShowAbout() => _aboutWindowService.ShowAbout();

    [RelayCommand]
    private void OpenWorkingFolder() => _launcher.OpenFolder(AppPaths.Root);
}
