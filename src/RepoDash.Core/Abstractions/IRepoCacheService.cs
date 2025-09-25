using System.Threading;
using System.Threading.Tasks;
using RepoDash.Core.Models;

namespace RepoDash.Core.Abstractions;

public interface IRepoCacheService
{
    Task<RepoCacheSnapshot?> LoadAsync(string rootPath, CancellationToken cancellationToken = default);
    Task SaveAsync(RepoCacheSnapshot snapshot, CancellationToken cancellationToken = default);
}
