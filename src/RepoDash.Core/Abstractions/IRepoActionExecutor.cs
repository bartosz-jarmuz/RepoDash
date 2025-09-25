using System.Threading;
using System.Threading.Tasks;
using RepoDash.Core.Models;

namespace RepoDash.Core.Abstractions;

public interface IRepoActionExecutor
{
    Task ExecuteAsync(RepoActionDescriptor descriptor, RepoSnapshot snapshot, CancellationToken cancellationToken = default);
}
