using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RepoDash.Core.Models;

namespace RepoDash.Core.Abstractions;

public interface IRepoDiscoveryService
{
    Task<IReadOnlyList<RepoDescriptor>> ScanAsync(string rootPath, CancellationToken cancellationToken = default);
}
