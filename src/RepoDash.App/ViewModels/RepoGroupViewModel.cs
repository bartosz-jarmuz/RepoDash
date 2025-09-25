using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RepoDash.App.ViewModels;

public partial class RepoGroupViewModel : ObservableObject
{
    [ObservableProperty]
    private string _header = string.Empty;

    [ObservableProperty]
    private double _columnWidth;

    public ObservableCollection<RepoItemViewModel> Items { get; } = new();

    public RepoGroupViewModel(string header, double columnWidth)
    {
        _header = header;
        _columnWidth = columnWidth;
    }
}
