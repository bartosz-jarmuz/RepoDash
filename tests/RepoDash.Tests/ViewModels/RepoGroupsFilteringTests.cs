using Moq;
using NUnit.Framework;
using RepoDash.App.Abstractions;
using RepoDash.App.ViewModels;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Settings;
using RepoDash.Tests.TestingUtilities;
using System.Threading;
using System.Threading.Tasks;

namespace RepoDash.Tests.ViewModels;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public sealed class RepoGroupsFilteringTests
{
    [Test]
    public void ApplyFilter_EmptyTerm_ReturnsAllSortedByName()
    {
        using var sandbox = new TestSandbox();
        var layout = sandbox.CreateLargeSystem();

        var group = MakeGroupVm();
        var items = layout.AllRepos.Select(MakeRepoItem).ToList();
        group.SetItems(items);

        group.ApplyFilter(string.Empty);

        var expected = items
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.That(group.Items.Count, Is.EqualTo(expected.Count));
        Assert.That(group.Items.Select(i => i.Name), Is.EqualTo(expected.Select(i => i.Name)));
    }

    [Test]
    public void ApplyFilter_TermMatchesName_IsCaseInsensitive()
    {
        using var sandbox = new TestSandbox();
        var layout = sandbox.CreateLargeSystem();

        var group = MakeGroupVm();
        var items = layout.AllRepos.Select(MakeRepoItem).ToList();
        group.SetItems(items);

        var term = "service"; // should match e.g., SearchService, SessionService, etc.
        group.ApplyFilter(term);

        var expected = items
            .Where(r =>
                r.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.Path.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.That(group.Items.Select(i => i.Path), Is.EqualTo(expected.Select(i => i.Path)));
    }

    [Test]
    public void ApplyFilter_TermMatchesPath_IsCaseInsensitive()
    {
        using var sandbox = new TestSandbox();
        var layout = sandbox.CreateLargeSystem();

        var group = MakeGroupVm();
        var items = layout.AllRepos.Select(MakeRepoItem).ToList();
        group.SetItems(items);

        var term = "services"; // substring present in many repo paths
        group.ApplyFilter(term);

        var expected = items
            .Where(r =>
                r.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.Path.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.That(group.Items.Select(i => i.Path), Is.EqualTo(expected.Select(i => i.Path)));
    }

    [Test]
    public void ApplyFilter_SubsequentCalls_UpdateItemsCorrectly()
    {
        using var sandbox = new TestSandbox();
        var layout = sandbox.CreateLargeSystem();

        var group = MakeGroupVm();
        var items = layout.AllRepos.Select(MakeRepoItem).ToList();
        group.SetItems(items);

        group.ApplyFilter("portal"); // first filter
        var afterPortal = group.Items.Select(i => i.Path).ToList();

        group.ApplyFilter("messaging"); // change to another term
        var afterMessaging = group.Items.Select(i => i.Path).ToList();

        group.ApplyFilter(string.Empty); // clear filter
        var afterClear = group.Items.Select(i => i.Path).ToList();

        // Expectations recomputed with the same predicate/sort as the VM
        var expectedPortal = items
            .Where(r =>
                r.Name.Contains("portal", StringComparison.OrdinalIgnoreCase) ||
                r.Path.Contains("portal", StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(i => i.Path)
            .ToList();

        var expectedMessaging = items
            .Where(r =>
                r.Name.Contains("messaging", StringComparison.OrdinalIgnoreCase) ||
                r.Path.Contains("messaging", StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(i => i.Path)
            .ToList();

        var expectedAll = items
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(i => i.Path)
            .ToList();

        Assert.That(afterPortal, Is.EqualTo(expectedPortal));
        Assert.That(afterMessaging, Is.EqualTo(expectedMessaging));
        Assert.That(afterClear, Is.EqualTo(expectedAll));
    }

    // ---- helpers ---------------------------------------------------------

    private static RepoGroupViewModel MakeGroupVm()
    {
        var general = new GeneralSettings();
        var generalSource = new Mock<IReadOnlySettingsSource<GeneralSettings>>();
        generalSource.SetupGet(s => s.Current).Returns(general);

        var generalStore = new Mock<ISettingsStore<GeneralSettings>>();
        generalStore.SetupGet(s => s.Current).Returns(general);
        generalStore
            .Setup(s => s.UpdateAsync(It.IsAny<Action<GeneralSettings>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tools = new ToolsPanelSettings();
        var toolsSource = new Mock<IReadOnlySettingsSource<ToolsPanelSettings>>();
        toolsSource.SetupGet(s => s.Current).Returns(tools);

        var toolsStore = new Mock<ISettingsStore<ToolsPanelSettings>>();
        toolsStore.SetupGet(s => s.Current).Returns(tools);
        toolsStore
            .Setup(s => s.UpdateAsync(It.IsAny<Action<ToolsPanelSettings>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var colorSettings = new ColorSettings();
        var colorStore = new Mock<ISettingsStore<ColorSettings>>();
        colorStore.SetupGet(s => s.Current).Returns(colorSettings);
        colorStore
            .Setup(s => s.UpdateAsync(It.IsAny<Action<ColorSettings>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new RepoGroupViewModel(generalSource.Object, generalStore.Object, toolsSource.Object, toolsStore.Object, colorStore.Object)
        {
            GroupKey = "all"
        };
    }

    private static RepoItemViewModel MakeRepoItem(string repoPath)
    {
        var launcher = new Mock<ILauncher>().Object;
        var git = new Mock<IGitService>().Object;
        var links = new Mock<IRemoteLinkProvider>().Object;
        var branch = new Mock<IBranchProvider>();
        var usage = new Mock<IRepoUsageService>();
        usage.Setup(u => u.IsPinned(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        usage.Setup(u => u.IsBlacklisted(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        var settings = new Mock<IReadOnlySettingsSource<GeneralSettings>>();
        settings.SetupGet(s => s.Current).Returns(new GeneralSettings());

        var vm = new RepoItemViewModel(launcher, git, links, branch.Object, usage.Object, settings.Object)
        {
            Name = Path.GetFileName(repoPath),
            Path = repoPath
        };
        return vm;
    }
}
