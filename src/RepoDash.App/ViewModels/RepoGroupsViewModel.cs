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

    [ObservableProperty] private ObservableCollection<RepoGroupViewModel> _groups = [];

    public GeneralSettings Settings => _settings.Current;

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

    public void Upsert(string groupKey, RepoItemViewModel item)
    {
        var group = Groups.FirstOrDefault(g => string.Equals(g.GroupKey, groupKey, StringComparison.OrdinalIgnoreCase));
        if (group is null)
        {
            group = new RepoGroupViewModel(_settings) { GroupKey = groupKey };
            Groups.Add(group);
        }
        group.Upsert(item);
        group.ApplyFilter(string.Empty);
    }

    public void RemoveByRepoPath(string repoPath)
    {
        foreach (var g in Groups)
        {
            if (g.RemoveByPath(repoPath))
                break;
        }
    }

    public IReadOnlyList<RepoItemViewModel> GetAllRepoItems() =>
        Groups
            .SelectMany(g => g.Items)
            .Where(i => i.HasGit)
            .ToList();
}