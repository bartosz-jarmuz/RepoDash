using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RepoDash.App.ViewModels;

public partial class RepoGroupViewModel : ObservableObject
{
    [ObservableProperty] private string _groupKey = string.Empty;

    // All items (unfiltered)
    private readonly List<RepoItemViewModel> _allItems = new();

    // UI-bound filtered collection
    public ObservableCollection<RepoItemViewModel> Items { get; } = new();

    public void SetItems(IEnumerable<RepoItemViewModel> items)
    {
        _allItems.Clear();
        _allItems.AddRange(items);
        ApplyFilter(string.Empty);
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

        foreach (var r in query)
            Items.Add(r);
    }
}