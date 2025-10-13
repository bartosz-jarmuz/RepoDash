using System.Diagnostics;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Settings;

namespace RepoDash.Infrastructure.Processes;

public sealed class Launcher : ILauncher
{
    private readonly ISettingsStore<GeneralSettings> _settings;
    public Launcher(ISettingsStore<GeneralSettings> settings) => _settings = settings;

    public void OpenSolution(string solutionPath)
        => Process.Start(new ProcessStartInfo(solutionPath) { UseShellExecute = true });

    public void OpenFolder(string folderPath)
    {
        var tc = _settings.Current.TotalCommanderPath;
        if (!string.IsNullOrWhiteSpace(tc) && File.Exists(tc))
            Process.Start(new ProcessStartInfo(tc, $"/O /T /L=\"{folderPath}\"") { UseShellExecute = true });
        else
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folderPath}\"") { UseShellExecute = true });
    }

    public void OpenUrl(Uri url)
        => Process.Start(new ProcessStartInfo(url.ToString()) { UseShellExecute = true });

    public void OpenGitUi(string repoPath)
    {
        var tool = _settings.Current.GitUiPath;
        if (!string.IsNullOrWhiteSpace(tool) && File.Exists(tool))
            Process.Start(new ProcessStartInfo(tool, $"\"{repoPath}\"") { UseShellExecute = true });
        else
            Process.Start(new ProcessStartInfo("bash.exe", $"--login -i -c \"cd \\\"{repoPath}\\\"; exec bash\"") { UseShellExecute = true });
    }
}