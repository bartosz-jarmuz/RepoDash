using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RepoDash.Core.Models;

namespace RepoDash.Core.Abstractions;

public interface IUsageTracker
{
    Task<IReadOnlyDictionary<RepoIdentifier, RepoUsageEntry>> LoadAsync(CancellationToken cancellationToken = default);
    Task<RepoUsageEntry> IncrementLaunchAsync(RepoIdentifier identifier, CancellationToken cancellationToken = default);
    Task UpdateAsync(RepoUsageEntry entry, CancellationToken cancellationToken = default);
}
