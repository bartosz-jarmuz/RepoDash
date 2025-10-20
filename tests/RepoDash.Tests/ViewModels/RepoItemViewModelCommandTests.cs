using System.Threading;
using Moq;
using NUnit.Framework;
using RepoDash.App.Abstractions;
using RepoDash.App.ViewModels;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Settings;
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

    [Test]
    public void StoryLink_Populated_WhenRegexMatchesBranch()
    {
        var launcher = new Mock<ILauncher>();
        var git = new Mock<IGitService>();
        var links = new Mock<IRemoteLinkProvider>();
        var branch = new Mock<IBranchProvider>();
        var usage = new Mock<IRepoUsageService>();
        usage.Setup(u => u.IsPinned(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        usage.Setup(u => u.IsBlacklisted(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var general = new GeneralSettings { JiraBaseUrl = "https://jira.example.com/browse/" };
        general.StoryReferenceRegularExpressions.Clear();
        general.StoryReferenceRegularExpressions.Add("(?<story>[A-Z]+-\\d+)");

        var settings = new Mock<IReadOnlySettingsSource<GeneralSettings>>();
        settings.SetupGet(s => s.Current).Returns(general);

        var vm = new RepoItemViewModel(launcher.Object, git.Object, links.Object, branch.Object, usage.Object, settings.Object)
        {
            HasGit = true
        };

        vm.CurrentBranch = "feature/ABC-123-awesome";

        Assert.That(vm.HasStoryLink, Is.True);
        Assert.That(vm.StoryReference, Is.EqualTo("ABC-123"));
        Assert.That(vm.OpenStoryCommand.CanExecute(null), Is.True);

        vm.OpenStoryCommand.Execute(null);

        launcher.Verify(l => l.OpenUrl(It.Is<Uri>(u => u.ToString() == "https://jira.example.com/browse/ABC-123")), Times.Once);
    }

    [Test]
    public void StoryLink_Hidden_WhenBaseUrlMissing()
    {
        var launcher = new Mock<ILauncher>();
        var git = new Mock<IGitService>();
        var links = new Mock<IRemoteLinkProvider>();
        var branch = new Mock<IBranchProvider>();
        var usage = new Mock<IRepoUsageService>();
        usage.Setup(u => u.IsPinned(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        usage.Setup(u => u.IsBlacklisted(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var general = new GeneralSettings { JiraBaseUrl = string.Empty };
        general.StoryReferenceRegularExpressions.Clear();
        general.StoryReferenceRegularExpressions.Add("(?<story>[A-Z]+-\\d+)");

        var settings = new Mock<IReadOnlySettingsSource<GeneralSettings>>();
        settings.SetupGet(s => s.Current).Returns(general);

        var vm = new RepoItemViewModel(launcher.Object, git.Object, links.Object, branch.Object, usage.Object, settings.Object)
        {
            HasGit = true
        };

        vm.CurrentBranch = "feature/ABC-123-awesome";

        Assert.That(vm.HasStoryLink, Is.False);
        Assert.That(vm.OpenStoryCommand.CanExecute(null), Is.False);
        launcher.Verify(l => l.OpenUrl(It.IsAny<Uri>()), Times.Never);
    }

    private static RepoItemViewModel MakeItem(IRepoUsageService usage)
    {
        var launcher = new Mock<ILauncher>().Object;
        var git = new Mock<IGitService>().Object;
        var links = new Mock<IRemoteLinkProvider>().Object;
        var branch = new Mock<IBranchProvider>();
        branch.Setup(b => b.GetCurrentBranchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync("main");
        var settings = new Mock<IReadOnlySettingsSource<GeneralSettings>>();
        settings.SetupGet(s => s.Current).Returns(new GeneralSettings());

        return new RepoItemViewModel(launcher, git, links, branch.Object, usage, settings.Object)
        {
            Name = "Repo",
            Path = "C:/dev/repo",
            HasGit = true
        };
    }
}
