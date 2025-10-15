using System;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RepoDash.App.ViewModels;

public partial class SearchBarViewModel : ObservableObject
{
    [ObservableProperty] 
    private string _searchText = string.Empty;

    public Action<string>? OnFilterChanged { get; set; }

    private readonly DispatcherTimer _debounceTimer;

    public SearchBarViewModel()
    {
        _debounceTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        _debounceTimer.Tick += (_, __) =>
        {
            _debounceTimer.Stop();
            OnFilterChanged?.Invoke(_searchText ?? string.Empty);
        };
    }

    partial void OnSearchTextChanged(string value)
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    [RelayCommand]
    private void Clear()
    {
        // Clear immediately and propagate the change without waiting for debounce
        _debounceTimer.Stop();
        SearchText = string.Empty;
        OnFilterChanged?.Invoke(string.Empty);
    }
}