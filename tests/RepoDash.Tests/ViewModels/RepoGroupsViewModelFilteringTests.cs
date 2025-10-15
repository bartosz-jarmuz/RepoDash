using Moq;
using NUnit.Framework;
using RepoDash.App.Abstractions;
using RepoDash.App.ViewModels;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Settings;
using RepoDash.Tests.TestingUtilities;

namespace RepoDash.Tests.ViewModels;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public sealed class RepoGroupsViewModelFilteringTests
{
    [Test]
    public void Load_ThenApplyFilter_PerGroupFilteringAndSorting_IsCaseInsensitive()
    {
        using var sandbox = new TestSandbox();
        var layout = sandbox.CreateLargeSystem();

        var vm = MakeGroupsVm();

        var groups = new Dictionary<string, List<RepoItemViewModel>>(StringComparer.OrdinalIgnoreCase)
        {
            ["apps"] = layout.Apps.Select(MakeRepoItem).ToList(),
            ["services"] = layout.Services.Select(MakeRepoItem).ToList(),
            ["components"] = layout.Components.Select(MakeRepoItem).ToList(),
            ["sql"] = layout.Sql.Select(MakeRepoItem).ToList(),
            ["utilities"] = layout.Utilities.Select(MakeRepoItem).ToList(),
            ["contracts"] = layout.Contracts.Select(MakeRepoItem).ToList(),
            ["it"] = layout.IntegrationTests.Select(MakeRepoItem).ToList(),
        };

        vm.Load(groups);

        // Apply a term that appears in names and paths for many repos
        var term = "service";
        vm.ApplyFilter(term);

        foreach (var g in vm.Groups)
        {
            var source = groups[g.GroupKey];
            var expected = source
                .Where(r =>
                    r.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    r.Path.Contains(term, StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .Select(r => r.Path)
                .ToList();

            var actual = g.Items.Select(r => r.Path).ToList();
            Assert.That(actual, Is.EqualTo(expected), $"Group '{g.GroupKey}' mismatch.");
        }
    }

    [Test]
    public void ApplyFilter_EmptyThenTermThenClear_RestoresAllPerGroup()
    {
        using var sandbox = new TestSandbox();
        var layout = sandbox.CreateLargeSystem();

        var vm = MakeGroupsVm();

        var groups = new Dictionary<string, List<RepoItemViewModel>>(StringComparer.OrdinalIgnoreCase)
        {
            ["apps"] = layout.Apps.Select(MakeRepoItem).ToList(),
            ["services"] = layout.Services.Select(MakeRepoItem).ToList(),
            ["components"] = layout.Components.Select(MakeRepoItem).ToList(),
            ["sql"] = layout.Sql.Select(MakeRepoItem).ToList(),
            ["utilities"] = layout.Utilities.Select(MakeRepoItem).ToList(),
            ["contracts"] = layout.Contracts.Select(MakeRepoItem).ToList(),
            ["it"] = layout.IntegrationTests.Select(MakeRepoItem).ToList(),
        };

        vm.Load(groups);

        // Baseline (empty filter) should include all, sorted by Name
        vm.ApplyFilter(string.Empty);
        foreach (var g in vm.Groups)
        {
            var expectedAll = groups[g.GroupKey]
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .Select(r => r.Path)
                .ToList();
            var actualAll = g.Items.Select(r => r.Path).ToList();
            Assert.That(actualAll, Is.EqualTo(expectedAll), $"Group '{g.GroupKey}' baseline mismatch.");
        }

        // Apply a specific term
        var term = "portal";
        vm.ApplyFilter(term);
        foreach (var g in vm.Groups)
        {
            var expectedFiltered = groups[g.GroupKey]
                .Where(r =>
                    r.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    r.Path.Contains(term, StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .Select(r => r.Path)
                .ToList();
            var actualFiltered = g.Items.Select(r => r.Path).ToList();
            Assert.That(actualFiltered, Is.EqualTo(expectedFiltered), $"Group '{g.GroupKey}' filtered mismatch.");
        }

        // Clear again — should restore baseline
        vm.ApplyFilter(string.Empty);
        foreach (var g in vm.Groups)
        {
            var expectedAll = groups[g.GroupKey]
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .Select(r => r.Path)
                .ToList();
            var actualAll = g.Items.Select(r => r.Path).ToList();
            Assert.That(actualAll, Is.EqualTo(expectedAll), $"Group '{g.GroupKey}' clear mismatch.");
        }
    }

    // ----- helpers --------------------------------------------------------

    private static RepoGroupsViewModel MakeGroupsVm()
    {
        var settings = new Mock<IReadOnlySettingsSource<GeneralSettings>>();
        settings.SetupGet(s => s.Current).Returns(new GeneralSettings());
        return new RepoGroupsViewModel(settings.Object);
    }

    private static RepoItemViewModel MakeRepoItem(string repoPath)
    {
        var launcher = new Mock<ILauncher>().Object;
        var git = new Mock<IGitService>().Object;
        var links = new Mock<IRemoteLinkProvider>().Object;
        var branch = new Mock<IBranchProvider>();

        var vm = new RepoItemViewModel(launcher, git, links, branch.Object)
        {
            Name = Path.GetFileName(repoPath),
            Path = repoPath
        };
        return vm;
    }
}