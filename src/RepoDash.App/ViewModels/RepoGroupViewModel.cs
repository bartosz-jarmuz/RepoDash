using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RepoDash.App.Abstractions;
using RepoDash.Core.Settings;

namespace RepoDash.App.ViewModels;

public partial class RepoGroupViewModel : ObservableObject
{
    public RepoGroupViewModel(IReadOnlySettingsSource<GeneralSettings> settings)
    {
        _settings = settings;
        _settings.PropertyChanged += (_, __) => OnPropertyChanged(nameof(Settings));
    }

    [ObservableProperty] private string _groupKey = string.Empty;
    [ObservableProperty] private ObservableCollection<RepoItemViewModel> _items = [];
    private readonly List<RepoItemViewModel> _allItems = [];
    private readonly IReadOnlySettingsSource<GeneralSettings> _settings;

    public GeneralSettings Settings => _settings.Current;

    public void SetItems(IEnumerable<RepoItemViewModel> items)
    {
        _allItems.Clear();
        _allItems.AddRange(items);

        Items.Clear();
        foreach (var r in _allItems.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
            Items.Add(r);
    }

    public void ApplyFilter(string term)
    {
        Items.Clear();
        IEnumerable<RepoItemViewModel> query = _allItems;

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = _allItems.Where(r =>
                r.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.Path.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var r in query.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
            Items.Add(r);
    }

    public void Upsert(RepoItemViewModel item)
    {
        var existing = _allItems.FirstOrDefault(i => string.Equals(i.Path, item.Path, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            _allItems.Add(item);
        }
        else
        {
            existing.Name = item.Name;
            existing.HasGit = item.HasGit;
            existing.HasSolution = item.HasSolution;
            existing.SolutionPath = item.SolutionPath;
        }
    }

    public bool RemoveByPath(string repoPath)
    {
        var existing = _allItems.FirstOrDefault(i => string.Equals(i.Path, repoPath, StringComparison.OrdinalIgnoreCase));
        if (existing is null) return false;
        _allItems.Remove(existing);
        Items.Remove(existing);
        return true;
    }
}
