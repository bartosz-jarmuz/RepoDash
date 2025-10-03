using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RepoDash.App.ViewModels;

public partial class RepoRootViewModel : ObservableObject
{
    [ObservableProperty] private string _repoRootInput = string.Empty;

    // Callbacks provided by MainViewModel
    public Func<Task>? OnLoad { get; set; }
    public Action? OnBrowse { get; set; }

    [RelayCommand] private void Browse() => OnBrowse?.Invoke();
    [RelayCommand] private Task Load() => OnLoad?.Invoke() ?? Task.CompletedTask;
}