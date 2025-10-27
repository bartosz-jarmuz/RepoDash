using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoDash.Core.Abstractions;
using System.Reflection;

namespace RepoDash.App.ViewModels;

public partial class AboutViewModel : ObservableObject
{
    private readonly ILauncher _launcher;
    private static readonly Uri RepositoryUri = new("https://github.com/EntainGroup/RepoDash");

    public AboutViewModel(ILauncher launcher)
    {
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        ApplicationVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "unknown";
    }

    public string ApplicationName => "RepoDash";
    public string Description => "RepoDash helps you browse, group, and operate on the repositories you care about."
        + " Quickly filter, launch tools, and coordinate Git tasks across your workspace.";
    public string GitHubUrl => RepositoryUri.ToString();
    public string ApplicationVersion { get; }

    public Action? RequestClose { get; set; }

    [RelayCommand]
    private void OpenRepository() => _launcher.OpenUrl(RepositoryUri);

    [RelayCommand]
    private void Close() => RequestClose?.Invoke();
}
