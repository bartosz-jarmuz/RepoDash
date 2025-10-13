namespace RepoDash.Core.Abstractions;

public interface ISettingsStore<TSettings>
{
    /// <summary>Current in-memory instance (mutable via data binding). Use UpdateAsync to persist.</summary>
    TSettings Current { get; }

    /// <summary>Persists the current instance to disk atomically. Optional mutate callback before save.</summary>
    Task UpdateAsync(Action<TSettings>? mutate = null, CancellationToken ct = default);

    /// <summary>Reloads from disk, replacing Current and raising SettingsChanged.</summary>
    Task ReloadAsync(CancellationToken ct = default);

    /// <summary>Raised after Current changes due to UpdateAsync or ReloadAsync.</summary>
    event EventHandler? SettingsChanged;
}