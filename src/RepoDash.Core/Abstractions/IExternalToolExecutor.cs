using System.Threading;
using System.Threading.Tasks;
using RepoDash.Core.Models;
using RepoDash.Core.Settings;

namespace RepoDash.Core.Abstractions;

public interface IExternalToolExecutor
{
    Task ExecuteAsync(ExternalToolEntry tool, RepoSnapshot snapshot, CancellationToken cancellationToken = default);
}
