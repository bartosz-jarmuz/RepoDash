using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Models;
using RepoDash.Core.Settings;

namespace RepoDash.Infrastructure.Processes;

public sealed class RepoActionExecutor : IRepoActionExecutor
{
    private readonly IRepoLauncher _launcher;
    private readonly IRemoteNavigator _remoteNavigator;
    private readonly IClipboardService _clipboard;
    private readonly Func<GeneralSettings> _settingsAccessor;

    public RepoActionExecutor(
        IRepoLauncher launcher,
        IRemoteNavigator remoteNavigator,
        IClipboardService clipboard,
        Func<GeneralSettings> settingsAccessor)
    {
        _launcher = launcher;
        _remoteNavigator = remoteNavigator;
        _clipboard = clipboard;
        _settingsAccessor = settingsAccessor;
    }

    public async Task ExecuteAsync(RepoActionDescriptor descriptor, RepoSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        switch (descriptor.Kind)
        {
            case RepoActionKind.Launch:
                await _launcher.LaunchAsync(snapshot.Descriptor, cancellationToken).ConfigureAwait(false);
                break;
            case RepoActionKind.OpenFolder:
                OpenFolder(snapshot.Descriptor.RepositoryPath);
                break;
            case RepoActionKind.OpenRemote:
                OpenUri(_remoteNavigator.BuildRepositoryUri(snapshot.Descriptor.RemoteUrl));
                break;
            case RepoActionKind.OpenPipelines:
                OpenUri(_remoteNavigator.BuildPipelinesUri(snapshot.Descriptor.RemoteUrl));
                break;
            case RepoActionKind.OpenGitUi:
                OpenGitUi(snapshot.Descriptor.RepositoryPath);
                break;
            case RepoActionKind.CopyName:
                _clipboard.SetText(snapshot.Identifier.Name);
                break;
            case RepoActionKind.CopyPath:
                _clipboard.SetText(snapshot.Descriptor.RepositoryPath);
                break;
            default:
                throw new NotSupportedException($"Action {descriptor.Kind} is not supported yet.");
        }
    }

    private void OpenFolder(string path)
    {
        var settings = _settingsAccessor();
        if (settings.ExplorerPreference.Equals("TotalCommander", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(settings.GitUiToolPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = settings.GitUiToolPath,
                Arguments = path,
                UseShellExecute = true
            });
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private static void OpenUri(Uri? uri)
    {
        if (uri is null)
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = uri.ToString(),
            UseShellExecute = true
        });
    }

    private void OpenGitUi(string repositoryPath)
    {
        var settings = _settingsAccessor();
        if (string.IsNullOrWhiteSpace(settings.GitUiToolPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = settings.GitUiToolPath,
            Arguments = repositoryPath,
            UseShellExecute = true
        });
    }
}
