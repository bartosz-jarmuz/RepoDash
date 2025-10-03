using CommunityToolkit.Mvvm.ComponentModel;
using RepoDash.Core.Models;
using System.Collections.ObjectModel;

namespace RepoDash.App.ViewModels;

public partial class RepoGroupsViewModel : ObservableObject
{
    private readonly GeneralSettings _settings;

    public RepoGroupsViewModel(GeneralSettings settings)
    {
        _settings = settings;
    }

    public GeneralSettings Settings => _settings; 

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

    public IReadOnlyList<RepoItemViewModel> GetAllRepoItems()
        => Groups.SelectMany(g => g.Items).Where(i => i.HasGit).ToList();
}