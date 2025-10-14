using Microsoft.Extensions.DependencyInjection;
using RepoDash.App.Abstractions;
using RepoDash.App.Services;
using RepoDash.App.ViewModels.Settings;
using RepoDash.Core.Abstractions;
using RepoDash.Core.NullObjects;
using RepoDash.Core.Settings;
using RepoDash.Infrastructure.Git;
using RepoDash.Infrastructure.Processes;
using RepoDash.Infrastructure.Remote;
using RepoDash.Infrastructure.Scanning;
using RepoDash.Persistence.FileStores;
using RepoDash.Persistence.Paths;
using System.IO;
using System.Windows;
using RepoDash.App.Windowing;
using RepoDash.Core.Caching;
using Application = System.Windows.Application;
using JsonRepoCacheStore = RepoDash.Persistence.FileStores.JsonRepoCacheStore;

namespace RepoDash.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = default!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Directory.CreateDirectory(AppPaths.SettingsDir);
        Directory.CreateDirectory(AppPaths.CacheDir);

        var sc = new ServiceCollection();

        sc.AddSingleton<IGitService, LibGit2SharpGitService>();
        sc.AddSingleton<IBranchProvider, LightweightBranchProvider>();
        sc.AddSingleton<IColorizer, NullColorizer>();
        sc.AddSingleton<IRemoteLinkProvider, GitRemoteLinkProvider>();
        sc.AddSingleton<ILauncher, Launcher>();
        sc.AddSingleton<IRepoScanner, FileSystemRepoScanner>();
        sc.AddSingleton<IUiDispatcher>(_ => new WpfUiDispatcher(Current.Dispatcher));


        sc.AddSingleton<IRepoCacheStore, JsonRepoCacheStore>();
        sc.AddSingleton<RepoCacheService>();

        sc.AddSingleton<ViewModels.MainViewModel>();

        // open-generic settings VM
        sc.AddSingleton(typeof(IReadOnlySettingsSource<>), typeof(SettingsSource<>));
        sc.AddTransient(typeof(ViewModels.SettingsMenuViewModel));
        sc.AddTransient(typeof(SettingsViewModel<>));

        // per-file stores (as we agreed earlier)
        sc.AddSingleton<ISettingsStore<GeneralSettings>, JsonFileSettingsStore<GeneralSettings>>();
        sc.AddSingleton<ISettingsStore<RepositoriesSettings>, JsonFileSettingsStore<RepositoriesSettings>>();
        sc.AddSingleton<ISettingsStore<ShortcutsSettings>, JsonFileSettingsStore<ShortcutsSettings>>();
        sc.AddSingleton<ISettingsStore<ColorSettings>, JsonFileSettingsStore<ColorSettings>>();
        sc.AddSingleton<ISettingsStore<ExternalToolsSettings>, JsonFileSettingsStore<ExternalToolsSettings>>();

        // window service
        sc.AddSingleton<ISettingsWindowService, SettingsWindowService>();
        sc.AddSingleton<IWindowPlacementCache, JsonWindowPlacementCache>();


        Services = sc.BuildServiceProvider();

        Resources["RepoDash_WindowCache"] = Services.GetRequiredService<IWindowPlacementCache>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            // Persist General settings once on exit (top-bar changes etc.)
            var generalStore = Services.GetRequiredService<ISettingsStore<GeneralSettings>>();
            generalStore.UpdateAsync().GetAwaiter().GetResult();
        }
        catch { /* best-effort */ }

        base.OnExit(e);
    }
}