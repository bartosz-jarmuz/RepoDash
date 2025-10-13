using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoDash.Core.Settings;

namespace RepoDash.App.ViewModels.Settings;

public partial class ShortcutsSettingsViewModel : ObservableObject
{
    public ObservableCollection<ShortcutEntry> Shortcuts { get; }

    public Action? OnSave { get; set; }
    public Action? OnCancel { get; set; }

    public ShortcutsSettingsViewModel(IEnumerable<ShortcutEntry> model) => Shortcuts = new ObservableCollection<ShortcutEntry>(model);

    [RelayCommand] private void Save() => OnSave?.Invoke();
    [RelayCommand] private void Cancel() => OnCancel?.Invoke();
}