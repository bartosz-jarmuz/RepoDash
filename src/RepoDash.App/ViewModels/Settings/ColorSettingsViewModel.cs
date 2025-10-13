using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Settings;

namespace RepoDash.App.ViewModels.Settings;

public partial class ColorSettingsViewModel : ObservableObject
{
    private readonly ISettingsStore<ColorSettings> _store;

    public ColorSettings Model => _store.Current;

    public ColorSettingsViewModel(ISettingsStore<ColorSettings> store)
    {
        _store = store;
    }

    [RelayCommand] private async Task SaveAsync() => await _store.UpdateAsync();
    [RelayCommand] private async Task CancelAsync() => await _store.ReloadAsync();
}