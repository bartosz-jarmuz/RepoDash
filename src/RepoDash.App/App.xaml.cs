using Microsoft.Extensions.DependencyInjection;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Models;
using RepoDash.Core.NullObjects;
using RepoDash.Infrastructure.Git;
using RepoDash.Infrastructure.Processes;
using RepoDash.Infrastructure.Scanning;
using RepoDash.Persistence.FileStores;
using RepoDash.Persistence.Paths;
using System.IO;
using System.Windows;
using RepoDash.Infrastructure.Remote;
using Application = System.Windows.Application;

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

        var general = JsonStore.LoadOrDefaultAsync(
            AppPaths.GeneralSettingsPath,
            () => new GeneralSettings(),
            CancellationToken.None).GetAwaiter().GetResult();

        sc.AddSingleton(general);
        sc.AddSingleton<Func<GeneralSettings>>(_ => () => _.GetRequiredService<GeneralSettings>());

        sc.AddSingleton<IGitService, LibGit2SharpGitService>();
        sc.AddSingleton<IColorizer, NullColorizer>();
        sc.AddSingleton<IRemoteLinkProvider, GitRemoteLinkProvider>();
        sc.AddSingleton<ILauncher, Launcher>();
        sc.AddSingleton<IRepoScanner, FileSystemRepoScanner>();

        sc.AddSingleton<ViewModels.MainViewModel>();

        Services = sc.BuildServiceProvider();
    }
}