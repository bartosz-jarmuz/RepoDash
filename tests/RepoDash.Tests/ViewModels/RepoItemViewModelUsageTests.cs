using Moq;
using NUnit.Framework;
using RepoDash.App.ViewModels;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Usage;

namespace RepoDash.Tests.ViewModels;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public sealed class RepoItemViewModelUsageTests
{
    [Test]
    public void LaunchCommand_RecordsUsage()
    {
        var launcher = new Mock<ILauncher>();
        var git = new Mock<IGitService>();
        var links = new Mock<IRemoteLinkProvider>();
        var branch = new Mock<IBranchProvider>();
        var usage = new Mock<IRepoUsageService>();

        var vm = new RepoItemViewModel(launcher.Object, git.Object, links.Object, branch.Object, usage.Object)
        {
            Name = "Sample",
            Path = @"C:\\dev\\sample",
            HasGit = true,
            HasSolution = false
        };

        vm.LaunchCommand.Execute(null);

        usage.Verify(u => u.RecordUsage(It.Is<RepoUsageSnapshot>(s =>
            s.RepoName == "Sample" &&
            s.RepoPath == @"C:\\dev\\sample")), Times.Once);
    }

    [Test]
    public void BrowseCommand_DoesNotRecordUsage()
    {
        var launcher = new Mock<ILauncher>();
        var git = new Mock<IGitService>();
        var links = new Mock<IRemoteLinkProvider>();
        var branch = new Mock<IBranchProvider>();
        var usage = new Mock<IRepoUsageService>();

        var vm = new RepoItemViewModel(launcher.Object, git.Object, links.Object, branch.Object, usage.Object)
        {
            Name = "Sample",
            Path = @"C:\\dev\\sample",
            HasGit = true,
            HasSolution = false
        };

        vm.BrowseCommand.Execute(null);

        usage.Verify(u => u.RecordUsage(It.IsAny<RepoUsageSnapshot>()), Times.Never);
    }
}
