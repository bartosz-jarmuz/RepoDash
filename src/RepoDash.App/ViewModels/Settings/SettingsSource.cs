using RepoDash.App.Abstractions;
using RepoDash.Core.Abstractions;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace RepoDash.App.ViewModels.Settings;

/// <summary>
/// Composition helper that turns an ISettingsStore{T} into a bindable source.
/// - Exposes Current (the store's current instance)
/// - Raises PropertyChanged(nameof(Current)) whenever the store changes
/// - Marshals notifications to the UI thread
/// - Uses WeakEventManager to avoid leaks; no Dispose required
/// </summary>
public sealed class SettingsSource<TSettings> : INotifyPropertyChanged, IReadOnlySettingsSource<TSettings>
{
    private readonly ISettingsStore<TSettings> _store;
    private readonly IUiDispatcher _ui;

    public SettingsSource(ISettingsStore<TSettings> store, IUiDispatcher ui)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));

        WeakEventManager<ISettingsStore<TSettings>, EventArgs>
            .AddHandler(_store, nameof(_store.SettingsChanged), OnStoreSettingsChanged);
    }

    public TSettings Current => _store.Current;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnStoreSettingsChanged(object? sender, EventArgs e)
    {
        if (_ui.CheckAccess())
        {
            Raise(nameof(Current));
        }
        else
        {
            _ui.Invoke(() => Raise(nameof(Current)));
        }
    }

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}