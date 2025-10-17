using System.Threading;
using Moq;
using NUnit.Framework;
using RepoDash.App.Abstractions;
using RepoDash.App.ViewModels;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Usage;

namespace RepoDash.Tests.ViewModels;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public sealed class RepoItemViewModelCommandTests
{
    [Test]
    public void TogglePinCommand_InvokesUsageService()
    {
        var usage = new Mock<IRepoUsageService>();
        usage.Setup(u => u.TogglePinned("Repo", "C:/dev/repo")).Returns(true);
        usage.Setup(u => u.IsPinned("Repo", "C:/dev/repo")).Returns(false);
        usage.Setup(u => u.IsBlacklisted("Repo", "C:/dev/repo")).Returns(false);

        var vm = MakeItem(usage.Object);

        vm.TogglePinCommand.Execute(null);

        usage.Verify(u => u.TogglePinned("Repo", "C:/dev/repo"), Times.Once);
        Assert.That(vm.IsPinned, Is.True);
    }

    [Test]
    public void ToggleBlacklistCommand_InvokesUsageService()
    {
        var usage = new Mock<IRepoUsageService>();
        usage.Setup(u => u.ToggleBlacklisted("Repo", "C:/dev/repo")).Returns(true);
        usage.Setup(u => u.IsPinned("Repo", "C:/dev/repo")).Returns(false);
        usage.Setup(u => u.IsBlacklisted("Repo", "C:/dev/repo")).Returns(false);

        var vm = MakeItem(usage.Object);

        vm.ToggleBlacklistCommand.Execute(null);

        usage.Verify(u => u.ToggleBlacklisted("Repo", "C:/dev/repo"), Times.Once);
        Assert.That(vm.IsBlacklisted, Is.True);
    }

    private static RepoItemViewModel MakeItem(IRepoUsageService usage)
    {
        var launcher = new Mock<ILauncher>().Object;
        var git = new Mock<IGitService>().Object;
        var links = new Mock<IRemoteLinkProvider>().Object;
        var branch = new Mock<IBranchProvider>();
        branch.Setup(b => b.GetCurrentBranchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync("main");

        return new RepoItemViewModel(launcher, git, links, branch.Object, usage)
        {
            Name = "Repo",
            Path = "C:/dev/repo",
            HasGit = true
        };
    }
}
