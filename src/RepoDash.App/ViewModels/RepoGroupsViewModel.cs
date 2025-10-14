using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using RepoDash.App.Abstractions;
using RepoDash.Core.Settings;

namespace RepoDash.App.ViewModels;

public partial class RepoGroupsViewModel : ObservableObject
{
    private readonly IReadOnlySettingsSource<GeneralSettings> _settings;

    public RepoGroupsViewModel(IReadOnlySettingsSource<GeneralSettings> settings)
    {
        _settings = settings;
        _settings.PropertyChanged += (_, __) => OnPropertyChanged(nameof(Settings));
    }

    public GeneralSettings Settings => _settings.Current;

    public ObservableCollection<RepoGroupViewModel> Groups { get; } = new();

    public void Load(IDictionary<string, List<RepoItemViewModel>> itemsByGroup)
    {
        Groups.Clear();

        foreach (var kv in itemsByGroup)
        {
            var groupVm = new RepoGroupViewModel(_settings) { GroupKey = kv.Key };
            groupVm.SetItems(kv.Value);
            Groups.Add(groupVm);
        }
    }

    public void ApplyFilter(string term)
    {
        foreach (var g in Groups)
            g.ApplyFilter(term ?? string.Empty);
    }

    public IReadOnlyList<RepoItemViewModel> GetAllRepoItems() =>
        Groups
            .SelectMany(g => g.Items)
            .Where(i => i.HasGit)
            .ToList();
}