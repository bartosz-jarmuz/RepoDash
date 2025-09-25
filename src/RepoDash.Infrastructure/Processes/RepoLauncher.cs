using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Models;

namespace RepoDash.Infrastructure.Processes;

public sealed class RepoLauncher : IRepoLauncher
{
    public Task LaunchAsync(RepoDescriptor descriptor, CancellationToken cancellationToken = default)
    {
        var target = descriptor.SolutionPath ?? descriptor.RepositoryPath;
        var info = new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true
        };

        Process.Start(info);
        return Task.CompletedTask;
    }
}
