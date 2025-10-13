using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoDash.Core.Settings;

namespace RepoDash.App.ViewModels.Settings;

public partial class RepositoriesSettingsViewModel : ObservableObject
{
    public RepositoriesSettings Model { get; }

    public Action? OnSave { get; set; }
    public Action? OnCancel { get; set; }

    public RepositoriesSettingsViewModel(RepositoriesSettings model) => Model = model;

    [RelayCommand] private void Save() => OnSave?.Invoke();
    [RelayCommand] private void Cancel() => OnCancel?.Invoke();
}