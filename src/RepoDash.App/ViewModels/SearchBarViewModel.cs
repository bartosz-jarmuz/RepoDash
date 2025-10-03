using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RepoDash.App.ViewModels;

public partial class SearchBarViewModel : ObservableObject
{
    [ObservableProperty] private string _searchText = string.Empty;

    public Action<string>? OnFilterChanged { get; set; }

    partial void OnSearchTextChanged(string value) => OnFilterChanged?.Invoke(value ?? string.Empty);

    [RelayCommand]
    private void Clear() => SearchText = string.Empty;
}