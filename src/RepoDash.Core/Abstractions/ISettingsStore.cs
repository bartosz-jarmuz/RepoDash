using System.Threading;
using System.Threading.Tasks;

namespace RepoDash.Core.Abstractions;

public interface ISettingsStore<T>
{
    Task<T> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(T settings, CancellationToken cancellationToken = default);
}
