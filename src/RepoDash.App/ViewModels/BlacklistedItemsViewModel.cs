using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoDash.App.Abstractions;
using RepoDash.Core.Abstractions;

namespace RepoDash.App.ViewModels;

public sealed partial class BlacklistedItemsViewModel : ObservableObject, IDisposable
{
    private readonly IRepoUsageService _usage;
    private readonly IUiDispatcher _ui;
    private readonly ObservableCollection<BlacklistedRepoViewModel> _items = new();

    public BlacklistedItemsViewModel(IRepoUsageService usage, IUiDispatcher ui)
    {
        _usage = usage ?? throw new ArgumentNullException(nameof(usage));
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));

        Items = new ReadOnlyObservableCollection<BlacklistedRepoViewModel>(_items);

        _usage.Changed += OnUsageChanged;
        Reload();
    }

    public ReadOnlyObservableCollection<BlacklistedRepoViewModel> Items { get; }

    [ObservableProperty]
    private bool _hasItems;

    public bool HasNoItems => !HasItems;

    partial void OnHasItemsChanged(bool value) => OnPropertyChanged(nameof(HasNoItems));

    private void OnUsageChanged(object? sender, EventArgs e)
    {
        if (_ui.CheckAccess())
        {
            Reload();
        }
        else
        {
            _ui.Invoke(Reload);
        }
    }

    private void Reload()
    {
        var snapshot = _usage.GetBlacklistedItems();

        _items.Clear();
        foreach (var item in snapshot)
        {
            _items.Add(new BlacklistedRepoViewModel(
                item.RepoName,
                item.RepoPath,
                this));
        }

        HasItems = _items.Count > 0;
    }

    internal void Restore(BlacklistedRepoViewModel item)
    {
        if (item is null) return;

        if (_usage.IsBlacklisted(item.Name, item.Path))
        {
            _usage.ToggleBlacklisted(item.Name, item.Path);
        }
    }

    public void Dispose()
    {
        _usage.Changed -= OnUsageChanged;
    }
}

public sealed partial class BlacklistedRepoViewModel : ObservableObject
{
    private readonly BlacklistedItemsViewModel _owner;

    public BlacklistedRepoViewModel(string name, string path, BlacklistedItemsViewModel owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _name = string.IsNullOrWhiteSpace(name) ? string.Empty : name;
        _path = string.IsNullOrWhiteSpace(path) ? string.Empty : path;
    }

    [ObservableProperty] private string _name;
    [ObservableProperty] private string _path;

    [RelayCommand]
    private void Restore() => _owner.Restore(this);
}
