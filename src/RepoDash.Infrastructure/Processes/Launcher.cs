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
        {
            Process.Start(new ProcessStartInfo(tc, $"/O /T /L=\"{folderPath}\"") { UseShellExecute = true });
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folderPath}\"") { UseShellExecute = true });
    }

    public void OpenUrl(Uri url)
        => Process.Start(new ProcessStartInfo(url.ToString()) { UseShellExecute = true });

    public void OpenGitUi(string repoPath)
    {
        if (string.IsNullOrWhiteSpace(repoPath)) return;

        var tool = _settings.Current.GitUiPath;
        if (!string.IsNullOrWhiteSpace(tool) && File.Exists(tool) && TryLaunchCustomTool(tool, repoPath))
        {
            return;
        }

        LaunchDefaultShell(repoPath);
    }

    public void OpenGitCommandLine(string repoPath)
    {
        if (string.IsNullOrWhiteSpace(repoPath)) return;

        var tool = _settings.Current.GitCliPath;
        if (!string.IsNullOrWhiteSpace(tool) && File.Exists(tool) && TryLaunchCustomTool(tool, repoPath))
        {
            return;
        }

        LaunchDefaultShell(repoPath);
    }

    public void OpenNonSlnRepo(string repoPath)
    {
        if (string.IsNullOrWhiteSpace(repoPath)) return;

        var editor = _settings.Current.NonSlnRepoEditorPath;
        if (!string.IsNullOrWhiteSpace(editor) && File.Exists(editor) && TryLaunchCustomTool(editor, repoPath))
        {
            return;
        }

        OpenFolder(repoPath);
    }

    public void OpenTarget(string targetPathOrUrl, string? arguments)
    {
        if (string.IsNullOrWhiteSpace(targetPathOrUrl)) return;

        if (Uri.TryCreate(targetPathOrUrl, UriKind.Absolute, out var uri) &&
            (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
             uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)))
        {
            OpenUrl(uri);
            return;
        }

        try
        {
            var psi = new ProcessStartInfo(targetPathOrUrl, arguments ?? string.Empty)
            {
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch
        {
            // Swallow to avoid UI disruption from user-provided invalid paths
        }
    }

    private static bool TryLaunchCustomTool(string executablePath, string repoPath)
    {
        try
        {
            var name = Path.GetFileName(executablePath);
            ProcessStartInfo psi;

            if (IsGitBash(name))
            {
                psi = new ProcessStartInfo(executablePath)
                {
                    UseShellExecute = false,
                    WorkingDirectory = repoPath,
                    Arguments = $"--cd=\"{repoPath}\""
                };
            }
            else if (IsPlainBash(name))
            {
                psi = new ProcessStartInfo(executablePath)
                {
                    UseShellExecute = false,
                    WorkingDirectory = repoPath,
                    Arguments = "--login -i"
                };
            }
            else
            {
                psi = new ProcessStartInfo(executablePath)
                {
                    UseShellExecute = true,
                    WorkingDirectory = repoPath,
                    Arguments = $"\"{repoPath}\""
                };
            }

            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void LaunchDefaultShell(string repoPath)
    {
        try
        {
            Process.Start(new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = true,
                WorkingDirectory = repoPath,
                Arguments = $"/K \"cd /d \"{repoPath}\"\""
            });
        }
        catch
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{repoPath}\"") { UseShellExecute = true });
        }
    }

    private static bool IsGitBash(string? fileName)
        => string.Equals(fileName, "git-bash.exe", StringComparison.OrdinalIgnoreCase);

    private static bool IsPlainBash(string? fileName)
        => string.Equals(fileName, "bash.exe", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(fileName, "bash", StringComparison.OrdinalIgnoreCase);
}
