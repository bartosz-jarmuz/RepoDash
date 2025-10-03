using Microsoft.Extensions.DependencyInjection;

namespace RepoDash.App.ViewModels;

public static class ViewModelLocator
{
    public static MainViewModel Main => App.Services.GetRequiredService<MainViewModel>();
}