using RepoDash.App.Abstractions;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RepoDash.App.Services;

public sealed class RepoStatusRefreshService
{
    private readonly IReadOnlySettingsSource<GeneralSettings> _settings;
    private readonly ISettingsStore<GeneralSettings> _store;

    public RepoStatusRefreshService(
        IReadOnlySettingsSource<GeneralSettings> settings,
        ISettingsStore<GeneralSettings> store)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public bool ShouldRefresh(string? rootPath, bool force)
    {
        if (force) return true;

        var cooldownMinutes = Math.Max(0, _settings.Current.StatusRefreshCooldownMinutes);
        if (cooldownMinutes <= 0) return true;

        var last = GetLastRefresh(rootPath);
        if (last is null) return true;

        var nextAllowed = last.Value + TimeSpan.FromMinutes(cooldownMinutes);
        return nextAllowed <= DateTimeOffset.UtcNow;
    }

    public DateTimeOffset? GetLastRefresh(string? rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            return null;

        var history = _settings.Current.StatusRefreshHistory;
        if (history is null || history.Count == 0)
            return null;

        var key = Normalize(rootPath);
        return history.TryGetValue(key, out var stamp) ? stamp : null;
    }

    public async Task<DateTimeOffset> MarkRefreshedAsync(string? rootPath, CancellationToken ct = default)
    {
        var when = DateTimeOffset.UtcNow;
        if (string.IsNullOrWhiteSpace(rootPath))
            return when;

        var key = Normalize(rootPath);

        await _store.UpdateAsync(settings =>
        {
            settings.StatusRefreshHistory ??= new Dictionary<string, DateTimeOffset?>();
            settings.StatusRefreshHistory[key] = when;
        }, ct).ConfigureAwait(false);

        return when;
    }

    private static string Normalize(string path)
    {
        var trimmed = path.Trim();
        if (trimmed.Length == 0) return string.Empty;

        try
        {
            trimmed = Path.GetFullPath(trimmed);
        }
        catch
        {
            // best effort; fall back to trimmed input
        }

        return trimmed.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                      .ToLowerInvariant();
    }
}
