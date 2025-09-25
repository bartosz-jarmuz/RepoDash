using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Models;
using RepoDash.Core.Settings;

namespace RepoDash.Infrastructure.Discovery;

public sealed class RepoDiscoveryService : IRepoDiscoveryService
{
    private readonly Func<RepositoriesSettings> _repoSettingsAccessor;
    private readonly Func<GeneralSettings> _generalSettingsAccessor;

    public RepoDiscoveryService(
        Func<RepositoriesSettings> repoSettingsAccessor,
        Func<GeneralSettings> generalSettingsAccessor)
    {
        _repoSettingsAccessor = repoSettingsAccessor;
        _generalSettingsAccessor = generalSettingsAccessor;
    }

    public Task<IReadOnlyList<RepoDescriptor>> ScanAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<RepoDescriptor>>(() =>
        {
            var settings = _repoSettingsAccessor();
            var general = _generalSettingsAccessor();
            var results = new ConcurrentBag<RepoDescriptor>();
            if (!Directory.Exists(rootPath))
            {
                return (IReadOnlyList<RepoDescriptor>)Array.Empty<RepoDescriptor>();
            }

            Parallel.ForEach(Directory.GetDirectories(rootPath), new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            }, folder =>
            {
                if (IsExcluded(folder, settings))
                {
                    return;
                }

                if (!Directory.Exists(Path.Combine(folder, ".git")))
                {
                    return;
                }

                var descriptor = BuildDescriptor(folder, general.GroupingSegmentNumber, settings.CategoryOverrides);
                results.Add(descriptor);
            });

            return (IReadOnlyList<RepoDescriptor>)results.OrderBy(r => r.Identifier.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }, cancellationToken);
    }

    private static bool IsExcluded(string folder, RepositoriesSettings settings)
    {
        foreach (var fragment in settings.ExcludedFragments)
        {
            if (folder.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static RepoDescriptor BuildDescriptor(string folder, int groupingSegmentNumber, IReadOnlyList<CategoryOverride> overrides)
    {
        var repoName = Path.GetFileName(folder);
        var solutionPath = Directory.EnumerateFiles(folder, "*.sln", SearchOption.AllDirectories).OrderBy(p => p.Length).FirstOrDefault();
        var hasSolution = solutionPath is not null;
        var category = DetermineCategory(folder, overrides);
        var groupKey = ComputeGroupKey(solutionPath ?? folder, groupingSegmentNumber);

        return new RepoDescriptor(
            new RepoIdentifier(repoName ?? folder),
            folder,
            solutionPath,
            HasGit: true,
            hasSolution,
            groupKey,
            category,
            Array.Empty<string>(),
            RemoteUrl: null);
    }

    private static string DetermineCategory(string folder, IReadOnlyList<CategoryOverride> overrides)
    {
        foreach (var entry in overrides)
        {
            if (entry.Matches(folder))
            {
                return entry.TargetCategory;
            }
        }

        return Path.GetFileName(Path.GetDirectoryName(folder) ?? folder) ?? "General";
    }

    private static string ComputeGroupKey(string path, int segmentNumber)
    {
        var normalized = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var parts = normalized.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (segmentNumber <= 0 || segmentNumber >= parts.Length)
        {
            return parts.LastOrDefault() ?? path;
        }

        return parts[segmentNumber];
    }
}
