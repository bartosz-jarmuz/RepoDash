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
    }

    [ObservableProperty] private string _groupKey = string.Empty;

    // All items (unfiltered)
    private readonly List<RepoItemViewModel> _allItems = new();
    
    private IReadOnlySettingsSource<GeneralSettings> _settings;

    // UI-bound filtered collection
    public ObservableCollection<RepoItemViewModel> Items { get; } = new();
    public GeneralSettings Settings => _settings.Current;

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