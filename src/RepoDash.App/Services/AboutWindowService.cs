using System;
using Microsoft.Extensions.DependencyInjection;
using RepoDash.App.Abstractions;
using RepoDash.App.ViewModels;
using RepoDash.App.Views;

namespace RepoDash.App.Services;

public sealed class AboutWindowService : IAboutWindowService
{
    private readonly IServiceProvider _services;

    public AboutWindowService(IServiceProvider services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public bool? ShowAbout()
    {
        var vm = _services.GetRequiredService<AboutViewModel>();
        var window = new AboutWindow
        {
            Owner = App.Current.MainWindow,
            DataContext = vm
        };

        vm.RequestClose = () => window.DialogResult = true;
        var result = window.ShowDialog();
        vm.RequestClose = null;
        return result;
    }
}
