using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Models;
using RepoDash.Persistence.Paths;

namespace RepoDash.Persistence.FileStores;

public sealed class UsageStore : IUsageTracker
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _filePath = AppPaths.UsageFile;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public async Task<IReadOnlyDictionary<RepoIdentifier, RepoUsageEntry>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath))
            {
                return new Dictionary<RepoIdentifier, RepoUsageEntry>();
            }

            await using var stream = File.OpenRead(_filePath);
            var payload = await JsonSerializer.DeserializeAsync<UsagePayload>(stream, Options, cancellationToken).ConfigureAwait(false)
                          ?? new UsagePayload();

            var result = new Dictionary<RepoIdentifier, RepoUsageEntry>(payload.Entries.Count);
            foreach (var entry in payload.Entries)
            {
                var identifier = new RepoIdentifier(entry.Key);
                result[identifier] = new RepoUsageEntry(identifier, entry.Value.LaunchCount, entry.Value.LastLaunchedAt, entry.Value.IsPinned);
            }

            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RepoUsageEntry> IncrementLaunchAsync(RepoIdentifier identifier, CancellationToken cancellationToken = default)
    {
        var data = new ConcurrentDictionary<RepoIdentifier, RepoUsageEntry>(await LoadAsync(cancellationToken).ConfigureAwait(false));
        var updated = data.AddOrUpdate(identifier,
            _ => new RepoUsageEntry(identifier, 1, DateTimeOffset.UtcNow, false),
            (_, existing) => existing with { LaunchCount = existing.LaunchCount + 1, LastLaunchedAt = DateTimeOffset.UtcNow });

        await PersistAsync(data, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    public async Task UpdateAsync(RepoUsageEntry entry, CancellationToken cancellationToken = default)
    {
        var data = new ConcurrentDictionary<RepoIdentifier, RepoUsageEntry>(await LoadAsync(cancellationToken).ConfigureAwait(false))
        {
            [entry.Identifier] = entry
        };

        await PersistAsync(data, cancellationToken).ConfigureAwait(false);
    }

    private async Task PersistAsync(ConcurrentDictionary<RepoIdentifier, RepoUsageEntry> entries, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            await using var stream = File.Create(_filePath);
            var payload = new UsagePayload();
            foreach (var entry in entries)
            {
                payload.Entries[entry.Key.Name] = new UsageEntryPayload
                {
                    LaunchCount = entry.Value.LaunchCount,
                    LastLaunchedAt = entry.Value.LastLaunchedAt,
                    IsPinned = entry.Value.IsPinned
                };
            }

            await JsonSerializer.SerializeAsync(stream, payload, Options, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private sealed class UsagePayload
    {
        public Dictionary<string, UsageEntryPayload> Entries { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class UsageEntryPayload
    {
        public int LaunchCount { get; init; }
        public DateTimeOffset? LastLaunchedAt { get; init; }
        public bool IsPinned { get; init; }
    }
}
