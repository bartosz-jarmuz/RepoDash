using System.Threading;
using System.Threading.Tasks;
using RepoDash.Core.Models;

namespace RepoDash.Core.Abstractions;

public interface IRepoLauncher
{
    Task LaunchAsync(RepoDescriptor descriptor, CancellationToken cancellationToken = default);
}
