using CommunityToolkit.Mvvm.Input;

namespace RepoDash.App.ViewModels.Settings;

public interface ISettingsViewModel
{
    string Title { get; }
    object Model { get; }

    IAsyncRelayCommand SaveCommand { get; }
    IAsyncRelayCommand CancelCommand { get; }
}