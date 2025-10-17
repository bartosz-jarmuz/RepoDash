using Microsoft.Extensions.DependencyInjection;
using RepoDash.App.Abstractions;
using RepoDash.App.ViewModels;
using RepoDash.App.ViewModels.Settings;
using RepoDash.App.Views.Settings;
using RepoDash.Core.Settings;
using System;
using System.Windows;

namespace RepoDash.App.Services;

public sealed class SettingsWindowService : ISettingsWindowService
{
    private readonly IServiceProvider _services;

    public SettingsWindowService(IServiceProvider services) => _services = services;

    public bool? ShowGeneral() => Show<GeneralSettings>("General Settings");
    public bool? ShowRepositories() => Show<RepositoriesSettings>("Repositories Settings");
    public bool? ShowShortcuts() => Show<ShortcutsSettings>("Extra Shortcuts");
    public bool? ShowColors() => Show<ColorSettings>("Color Rules");
    public bool? ShowExternalTools() => Show<ExternalToolsSettings>("External Tools");
    public bool? ShowBlacklistedItems()
    {
        var vm = _services.GetRequiredService<BlacklistedItemsViewModel>();
        var window = new Views.BlacklistedItemsWindow
        {
            Owner = App.Current.MainWindow,
            DataContext = vm
        };

        void OnClosed(object? sender, EventArgs e)
        {
            window.Closed -= OnClosed;
            vm.Dispose();
        }

        window.Closed += OnClosed;
        return window.ShowDialog();
    }

    private bool? Show<TSettings>(string title) where TSettings : class, new()
    {
        var titledVm = new SettingsViewModel<TSettings>(
            _services.GetRequiredService<Core.Abstractions.ISettingsStore<TSettings>>(),
            title);

        var window = new SettingsWindow
        {
            Owner = App.Current.MainWindow,
            DataContext = titledVm
        };
        titledVm.RequestClose = () => window.DialogResult = true;

        return window.ShowDialog();
    }
}