using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RepoDash.App.Services.Settings;
using RepoDash.App.State;
using RepoDash.App.ViewModels;
using RepoDash.App.Views;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Settings;
using RepoDash.Infrastructure.Color;
using RepoDash.Infrastructure.Discovery;
using RepoDash.Infrastructure.Git;
using RepoDash.Infrastructure.Hotkeys;
using RepoDash.Infrastructure.Processes;
using RepoDash.Infrastructure.Remote;
using RepoDash.Infrastructure.Tray;
using RepoDash.Persistence.FileStores;
using RepoDash.Persistence.Paths;
using System;
using System.Windows;

namespace RepoDash.App;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureServices(ConfigureServices);

        _host = hostBuilder.Build();
        await _host.StartAsync().ConfigureAwait(false);

        var bootstrapper = _host.Services.GetRequiredService<SettingsBootstrapper>();
        var appState = _host.Services.GetRequiredService<AppState>();
        await bootstrapper.InitializeAsync(appState).ConfigureAwait(false);

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        _ = AppPaths.Root;

        services.AddSingleton<AppState>();
        services.AddSingleton<SettingsBootstrapper>();

        services.AddSingleton<ISettingsStore<GeneralSettings>>(sp => new JsonSettingsStore<GeneralSettings>(AppPaths.GetSettingsFile("general")));
        services.AddSingleton<ISettingsStore<RepositoriesSettings>>(sp => new JsonSettingsStore<RepositoriesSettings>(AppPaths.GetSettingsFile("repositories")));
        services.AddSingleton<ISettingsStore<ShortcutsSettings>>(sp => new JsonSettingsStore<ShortcutsSettings>(AppPaths.GetSettingsFile("shortcuts")));
        services.AddSingleton<ISettingsStore<ColorRulesSettings>>(sp => new JsonSettingsStore<ColorRulesSettings>(AppPaths.GetSettingsFile("colors")));
        services.AddSingleton<ISettingsStore<ExternalToolSettings>>(sp => new JsonSettingsStore<ExternalToolSettings>(AppPaths.GetSettingsFile("tools")));
        services.AddSingleton<ISettingsStore<StatusPollingSettings>>(sp => new JsonSettingsStore<StatusPollingSettings>(AppPaths.GetSettingsFile("status")));

        services.AddSingleton<IRepoCacheService, RepoCacheFileStore>();
        services.AddSingleton<IUsageTracker, UsageStore>();
        services.AddSingleton<IRepoDiscoveryService>(sp =>
            new RepoDiscoveryService(
                () => sp.GetRequiredService<AppState>().RepositoriesSettings,
                () => sp.GetRequiredService<AppState>().GeneralSettings));

        services.AddSingleton<IColorizer>(sp => new NameToBrushColorizer(() => sp.GetRequiredService<AppState>().ColorRulesSettings));
        services.AddSingleton<IGitProvider, LibGit2SharpGitProvider>();
        services.AddSingleton<IRepoLauncher, RepoLauncher>();
        services.AddSingleton<IRepoActionExecutor>(sp =>
            new RepoActionExecutor(
                sp.GetRequiredService<IRepoLauncher>(),
                sp.GetRequiredService<IRemoteNavigator>(),
                sp.GetRequiredService<IClipboardService>(),
                () => sp.GetRequiredService<AppState>().GeneralSettings));
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IRemoteNavigator, GitRemoteNavigator>();
        services.AddSingleton<ITrayIconService, NotifyIconService>();
        services.AddSingleton<IGlobalHotkeyService, NullGlobalHotkeyService>();

        services.AddSingleton<MainViewModel>();
        services.AddTransient<MainWindow>();
    }
}
