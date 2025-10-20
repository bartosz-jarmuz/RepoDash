using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using RepoDash.App.Abstractions;
using RepoDash.App.ViewModels;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Settings;
using RepoDash.Core.Usage;
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
            ["apps"] = layout.Apps.Select(path => MakeRepoItem(path)).ToList(),
            ["services"] = layout.Services.Select(path => MakeRepoItem(path)).ToList(),
            ["components"] = layout.Components.Select(path => MakeRepoItem(path)).ToList(),
            ["sql"] = layout.Sql.Select(path => MakeRepoItem(path)).ToList(),
            ["utilities"] = layout.Utilities.Select(path => MakeRepoItem(path)).ToList(),
            ["contracts"] = layout.Contracts.Select(path => MakeRepoItem(path)).ToList(),
            ["it"] = layout.IntegrationTests.Select(path => MakeRepoItem(path)).ToList(),
        };

        vm.Load(groups);

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
            ["apps"] = layout.Apps.Select(path => MakeRepoItem(path)).ToList(),
            ["services"] = layout.Services.Select(path => MakeRepoItem(path)).ToList(),
            ["components"] = layout.Components.Select(path => MakeRepoItem(path)).ToList(),
            ["sql"] = layout.Sql.Select(path => MakeRepoItem(path)).ToList(),
            ["utilities"] = layout.Utilities.Select(path => MakeRepoItem(path)).ToList(),
            ["contracts"] = layout.Contracts.Select(path => MakeRepoItem(path)).ToList(),
            ["it"] = layout.IntegrationTests.Select(path => MakeRepoItem(path)).ToList(),
        };

        vm.Load(groups);

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

    [Test]
    public void AutomaticGroupsRespectPinSetting()
    {
        var general = new GeneralSettings { PinningAppliesToAutomaticGroupings = false };
        var vm = MakeGroupsVm(general);

        var pinned = MakeRepoItem(@"C:\dev\repo-pinned", pinned: true);
        pinned.Name = "Pinned";
        var regular = MakeRepoItem(@"C:\dev\repo-regular");
        regular.Name = "Regular";

        vm.Load(new Dictionary<string, List<RepoItemViewModel>>
        {
            ["apps"] = new() { regular, pinned }
        });

        var group = vm.Groups.Single(g => string.Equals(g.InternalKey, "apps", StringComparison.OrdinalIgnoreCase));
        Assert.That(group.AllowPinning, Is.False);
        Assert.That(group.Items.Select(i => i.Name), Is.EqualTo(new[] { "Regular", "Pinned" }));

        general.PinningAppliesToAutomaticGroupings = true;
        vm.RefreshPinningSettings();

        Assert.That(group.AllowPinning, Is.True);
        Assert.That(group.Items.Select(i => i.Name), Is.EqualTo(new[] { "Pinned", "Regular" }));
    }

    [Test]
    public void ChangingPinningApplicability_DoesNotClearPinState()
    {
        var store = new InMemoryUsageStore();
        var usage = new RepoUsageService(store);
        var general = new GeneralSettings { PinningAppliesToAutomaticGroupings = true };

        var repoItem = MakeRepoItem(@"C:\dev\apps\RepoA", usageOverride: usage);
        repoItem.TogglePinCommand.Execute(null);
        SpinWait.SpinUntil(() => store.WriteCount > 0, TimeSpan.FromSeconds(1));
        Assert.That(repoItem.IsPinned, Is.True, "Sanity: repo is pinned before settings change.");

        var vm = MakeGroupsVm(general);
        vm.Load(new Dictionary<string, List<RepoItemViewModel>> { ["apps"] = new() { repoItem } });
        Assert.That(repoItem.IsPinned, Is.True, "Load should not alter pin state.");

        general.PinningAppliesToAutomaticGroupings = false;
        vm.RefreshPinningSettings();

        Assert.That(repoItem.IsPinned, Is.True, "Changing pin visibility must not clear pin state.");
        Assert.That(store.GetPinnedPaths(), Contains.Item(Path.GetFullPath(repoItem.Path)));
    }

    

[Test]
    public void PinnedStatePersistsAcrossUsageServiceRestart()
    {
        var store = new InMemoryUsageStore();
        var usage1 = new RepoUsageService(store);

        var original = MakeRepoItem(@"C:\dev\apps\RepoB", usageOverride: usage1);
        original.TogglePinCommand.Execute(null);
        SpinWait.SpinUntil(() => store.WriteCount > 0, TimeSpan.FromSeconds(1));
        Assert.That(original.IsPinned, Is.True, "Sanity: repo is pinned before restart.");

        var usage2 = new RepoUsageService(store);
        var rehydrated = MakeRepoItem(@"C:\dev\apps\RepoB", usageOverride: usage2);
        rehydrated.RefreshUsageFlags();

        Assert.That(rehydrated.IsPinned, Is.True, "Pinned state should survive usage-service restart.");
    }

    [Test]
    public void RecentAndFrequentRespectPinSettings()
    {
        var general = new GeneralSettings
        {
            PinningAppliesToRecent = false,
            PinningAppliesToFrequent = true
        };
        var vm = MakeGroupsVm(general);

        var now = DateTimeOffset.UtcNow;
        var recentPinned = MakeRepoItem(@"C:\dev\recent-pinned", pinned: true, usageCount: 10, lastUsed: now);
        var recentOther = MakeRepoItem(@"C:\dev\recent-other", usageCount: 5, lastUsed: now.AddMinutes(-5));

        vm.SetRecentItems(new[] { recentOther, recentPinned }, isVisible: true);
        var recentGroup = vm.Groups.Single(g => string.Equals(g.InternalKey, "__special_recent", StringComparison.OrdinalIgnoreCase));
        Assert.That(recentGroup.AllowPinning, Is.False);
        Assert.That(recentPinned.IsPinned, Is.True);
        Assert.That(recentGroup.Items.First().Path, Is.EqualTo(recentOther.Path));

        var frequentPinned = MakeRepoItem(@"C:\dev\freq-pinned", pinned: true, usageCount: 20);
        var frequentOther = MakeRepoItem(@"C:\dev\freq-other", usageCount: 5);

        vm.SetFrequentItems(new[] { frequentOther, frequentPinned }, isVisible: true);
        var frequentGroup = vm.Groups.Single(g => string.Equals(g.InternalKey, "__special_frequent", StringComparison.OrdinalIgnoreCase));
        Assert.That(frequentGroup.AllowPinning, Is.True);
        Assert.That(frequentGroup.Items.First().Path, Is.EqualTo(frequentPinned.Path));
    }

    private static RepoGroupsViewModel MakeGroupsVm(GeneralSettings? general = null)
    {
        general ??= new GeneralSettings();
        var settings = new Mock<IReadOnlySettingsSource<GeneralSettings>>();
        settings.SetupGet(s => s.Current).Returns(general);

        var generalStore = new Mock<ISettingsStore<GeneralSettings>>();
        generalStore.SetupGet(s => s.Current).Returns(general);
        generalStore
            .Setup(s => s.UpdateAsync(It.IsAny<Action<GeneralSettings>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var colorSettings = new ColorSettings();
        var colorStore = new Mock<ISettingsStore<ColorSettings>>();
        colorStore.SetupGet(s => s.Current).Returns(colorSettings);
        colorStore
            .Setup(s => s.UpdateAsync(It.IsAny<Action<ColorSettings>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new RepoGroupsViewModel(settings.Object, generalStore.Object, colorStore.Object);
    }

    private static RepoItemViewModel MakeRepoItem(
        string repoPath,
        bool pinned = false,
        bool blacklisted = false,
        int usageCount = 0,
        DateTimeOffset? lastUsed = null,
        IRepoUsageService? usageOverride = null)
    {
        var launcher = new Mock<ILauncher>().Object;
        var git = new Mock<IGitService>().Object;
        var links = new Mock<IRemoteLinkProvider>().Object;
        var branch = new Mock<IBranchProvider>();
        var settings = new Mock<IReadOnlySettingsSource<GeneralSettings>>();
        settings.SetupGet(s => s.Current).Returns(new GeneralSettings());

        IRepoUsageService usage;
        if (usageOverride is not null)
        {
            usage = usageOverride;
        }
        else
        {
            var usageMock = new Mock<IRepoUsageService>();
            var pinnedState = pinned;
            var blacklistedState = blacklisted;

            usageMock.Setup(u => u.IsPinned(It.IsAny<string>(), It.IsAny<string>())).Returns(() => pinnedState);
            usageMock.Setup(u => u.TogglePinned(It.IsAny<string>(), It.IsAny<string>())).Returns(() =>
            {
                pinnedState = !pinnedState;
                return pinnedState;
            });
            usageMock.Setup(u => u.IsBlacklisted(It.IsAny<string>(), It.IsAny<string>())).Returns(() => blacklistedState);
            usageMock.Setup(u => u.ToggleBlacklisted(It.IsAny<string>(), It.IsAny<string>())).Returns(() =>
            {
                blacklistedState = !blacklistedState;
                return blacklistedState;
            });

            usage = usageMock.Object;
        }

        var vm = new RepoItemViewModel(launcher, git, links, branch.Object, usage, settings.Object)
        {
            Name = Path.GetFileName(repoPath),
            Path = repoPath,
            HasGit = true
        };

        vm.ApplyUsageMetrics(lastUsed, usageCount);
        vm.RefreshUsageFlags();
        return vm;
    }

    private sealed class InMemoryUsageStore : IRepoUsageStore
    {
        private RepoUsageState _state = new();
        private readonly HashSet<string> _pinned = new(StringComparer.OrdinalIgnoreCase);
        private int _writeCount;

        public Task<RepoUsageState> ReadAsync(CancellationToken ct) => Task.FromResult(Clone(_state));

        public Task WriteAsync(RepoUsageState state, CancellationToken ct)
        {
            _state = Clone(state);
            _pinned.Clear();
            foreach (var path in state.PinnedPaths)
                _pinned.Add(NormalizePath(path));
            Interlocked.Increment(ref _writeCount);
            return Task.CompletedTask;
        }

        public IEnumerable<string> GetPinnedPaths() => _pinned;

        public int WriteCount => Volatile.Read(ref _writeCount);

        private static RepoUsageState Clone(RepoUsageState source) => new()
        {
            Entries = source.Entries.Select(e => e with { }).ToList(),
            PinnedPaths = new List<string>(source.PinnedPaths),
            PinnedNames = new List<string>(source.PinnedNames),
            BlacklistedPaths = new List<string>(source.BlacklistedPaths),
            BlacklistedNames = new List<string>(source.BlacklistedNames),
            BlacklistedItems = source.BlacklistedItems.Select(i => i with { }).ToList()
        };

        private static string NormalizePath(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
