namespace RepoDash.Core.Abstractions;

public interface ILauncher
{
    void OpenSolution(string solutionPath);
    void OpenFolder(string folderPath);
    void OpenUrl(Uri url);
    void OpenGitUi(string repoPath);
    void OpenGitCommandLine(string repoPath);
    void OpenNonSlnRepo(string repoPath);
    void OpenTarget(string targetPathOrUrl, string? arguments);
}