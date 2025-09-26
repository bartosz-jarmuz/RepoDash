using System.Diagnostics;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Models;

namespace RepoDash.Infrastructure.Processes;

public sealed class Launcher : ILauncher
{
    private readonly Func<GeneralSettings> _settings;
    public Launcher(Func<GeneralSettings> settingsAccessor) => _settings = settingsAccessor;

    public void OpenSolution(string solutionPath)
        => Process.Start(new ProcessStartInfo(solutionPath) { UseShellExecute = true });

    public void OpenFolder(string folderPath)
    {
        var tc = _settings().TotalCommanderPath;
        if (!string.IsNullOrWhiteSpace(tc) && File.Exists(tc))
            Process.Start(new ProcessStartInfo(tc, $"/O /T /L=\"{folderPath}\"") { UseShellExecute = true });
        else
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folderPath}\"") { UseShellExecute = true });
    }

    public void OpenUrl(Uri url)
        => Process.Start(new ProcessStartInfo(url.ToString()) { UseShellExecute = true });

    public void OpenGitUi(string repoPath)
    {
        var tool = _settings().GitUiPath;
        if (!string.IsNullOrWhiteSpace(tool) && File.Exists(tool))
            Process.Start(new ProcessStartInfo(tool, $"\"{repoPath}\"") { UseShellExecute = true });
        else
            Process.Start(new ProcessStartInfo("bash.exe", $"--login -i -c \"cd \\\"{repoPath}\\\"; exec bash\"") { UseShellExecute = true });
    }
}