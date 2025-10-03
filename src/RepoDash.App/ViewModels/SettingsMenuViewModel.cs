using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RepoDash.App.ViewModels;

public partial class SettingsMenuViewModel : ObservableObject
{
    // Provided by MainVM
    public Func<IReadOnlyList<RepoItemViewModel>>? ResolveGitRepos { get; set; }
    public Func<IReadOnlyList<RepoItemViewModel>, Task>? OnFetchAll { get; set; }
    public Func<IReadOnlyList<RepoItemViewModel>, bool, Task>? OnPullAll { get; set; }

    [RelayCommand]
    private async Task GitFetchAllAsync()
    {
        var repos = ResolveGitRepos?.Invoke() ?? Array.Empty<RepoItemViewModel>();
        if (OnFetchAll is not null) await OnFetchAll(repos);
    }

    [RelayCommand]
    private async Task GitPullAllAsync() => await Pull(rebase: false);

    [RelayCommand]
    private async Task GitPullRebaseAllAsync() => await Pull(rebase: true);

    private async Task Pull(bool rebase)
    {
        var repos = ResolveGitRepos?.Invoke() ?? Array.Empty<RepoItemViewModel>();
        if (OnPullAll is not null) await OnPullAll(repos, rebase);
    }
}