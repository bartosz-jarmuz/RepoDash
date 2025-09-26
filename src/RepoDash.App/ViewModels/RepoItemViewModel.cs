using CommunityToolkit.Mvvm.ComponentModel;

namespace RepoDash.App.ViewModels;

public partial class RepoItemViewModel : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _path = string.Empty;
    [ObservableProperty] private bool _hasSolution;
    [ObservableProperty] private string? _solutionPath;
    [ObservableProperty] private bool _hasGit;
}