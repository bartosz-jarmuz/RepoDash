using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using RepoDash.App.ViewModels;
using RepoDash.App.Abstractions;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Caching;
using RepoDash.Core.Settings;
using RepoDash.Core.Usage;
using RepoDash.Tests.TestingUtilities;

namespace RepoDash.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public sealed class MainViewModelFilteringSmokeTests
    {
        [Test]
        public void Load_ThenApplyFilter_PerGroupResultsMatchPredicateAndSort()
        {
            using var sandbox = new TestSandbox();
            var layout = sandbox.CreateLargeSystem();

            var vm = MakeMainVm();

            // Build groups from sandbox categories
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

            vm.RepoGroups.Load(groups);

            // Drive filtering at the app-VM level deterministically
            var term = "service";
            vm.RepoGroups.ApplyFilter(term);

            foreach (var g in vm.RepoGroups.Groups)
            {
                var src = groups[g.GroupKey];
                var expected = src
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
        public void ApplyFilter_Clear_RestoresAllPerGroup_SortedByName()
        {
            using var sandbox = new TestSandbox();
            var layout = sandbox.CreateLargeSystem();

            var vm = MakeMainVm();

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

            vm.RepoGroups.Load(groups);

            vm.RepoGroups.ApplyFilter("portal");     // filter
            vm.RepoGroups.ApplyFilter(string.Empty); // clear

            foreach (var g in vm.RepoGroups.Groups)
            {
                var expectedAll = groups[g.GroupKey]
                    .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(r => r.Path)
                    .ToList();

                var actualAll = g.Items.Select(r => r.Path).ToList();
                Assert.That(actualAll, Is.EqualTo(expectedAll), $"Group '{g.GroupKey}' clear mismatch.");
            }
        }

        [Test]
        public void SearchBar_OnFilterChanged_IsWired()
        {
            var vm = MakeMainVm();
            Assert.That(vm.SearchBar.OnFilterChanged, Is.Not.Null);
        }

        // ---- helpers ---------------------------------------------------------

        private static MainViewModel MakeMainVm()
        {
            var general = new GeneralSettings();
            var generalSource = new Mock<IReadOnlySettingsSource<GeneralSettings>>();
            generalSource.SetupGet(s => s.Current).Returns(general);

            var generalStore = new Mock<ISettingsStore<GeneralSettings>>();
            generalStore.SetupGet(s => s.Current).Returns(general);
            generalStore
                .Setup(s => s.UpdateAsync(It.IsAny<Action<GeneralSettings>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var colorStore = new Mock<ISettingsStore<ColorSettings>>();
            var colorSettings = new ColorSettings();
            colorStore.SetupGet(s => s.Current).Returns(colorSettings);
            colorStore
                .Setup(s => s.UpdateAsync(It.IsAny<Action<ColorSettings>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var scanner = new Mock<IRepoScanner>();
            var launcher = new Mock<ILauncher>();
            var git = new Mock<IGitService>();
            var branch = new Mock<IBranchProvider>();
            var links = new Mock<IRemoteLinkProvider>();
            var settingsWindows = new Mock<ISettingsWindowService>();
            var settingsMenuVm = new SettingsMenuViewModel(settingsWindows.Object);

            var cacheStore = new Mock<IRepoCacheStore>();
            var cache = new RepoCacheService(cacheStore.Object, scanner.Object, branch.Object);
            var usage = new Mock<IRepoUsageService>();
            usage.Setup(u => u.GetRecent(It.IsAny<int>())).Returns(Array.Empty<RepoUsageEntry>());
            usage.Setup(u => u.GetFrequent(It.IsAny<int>())).Returns(Array.Empty<RepoUsageSummary>());
            usage.Setup(u => u.IsPinned(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
            usage.Setup(u => u.IsBlacklisted(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

            return new MainViewModel(
                generalSource.Object,
                generalStore.Object,
                colorStore.Object,
                launcher.Object,
                git.Object,
                branch.Object,
                links.Object,
                settingsMenuVm,
                cache,
                usage.Object);
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

            return new RepoItemViewModel(launcher, git, links, branch.Object, usage.Object)
            {
                Name = Path.GetFileName(repoPath),
                Path = repoPath
            };
        }
    }
}
