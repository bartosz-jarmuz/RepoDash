using System;
using System.Collections.Generic;
using System.Linq;
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
public sealed class BlacklistedItemsViewModelTests
{
    [Test]
    public void Constructor_PopulatesItemsAndFlags()
    {
        var usage = new Mock<IRepoUsageService>();
        usage
            .Setup(u => u.GetBlacklistedItems())
            .Returns(new List<RepoBlacklistItem> { new() { RepoName = "Hidden", RepoPath = "C:/dev/hidden" } });

        var dispatcher = CreateDispatcher();

        using var vm = new BlacklistedItemsViewModel(usage.Object, dispatcher.Object);

        Assert.That(vm.Items, Has.Count.EqualTo(1));
        Assert.That(vm.HasItems, Is.True);
        Assert.That(vm.HasNoItems, Is.False);
    }

    [Test]
    public void Constructor_WithConcreteUsageServiceAfterBlacklist_ShowsItems()
    {
        var store = new DeferredRepoUsageStore();
        var usage = new RepoUsageService(store);
        var dispatcher = CreateDispatcher();

        using (var vm = new BlacklistedItemsViewModel(usage, dispatcher.Object))
        {
            var repoVm = new RepoItemViewModel(
                new Mock<ILauncher>().Object,
                new Mock<IGitService>().Object,
                new Mock<IRemoteLinkProvider>().Object,
                new Mock<IBranchProvider>().Object,
                usage) { Name = "Hidden", Path = "C:/dev/hidden", HasGit = true };

            repoVm.ToggleBlacklistCommand.Execute(null);

            Assert.That(vm.Items, Has.Count.EqualTo(1), "Blacklisted item should be visible in the management UI.");
        }

        SpinWait.SpinUntil(() => store.WriteCount > 0, TimeSpan.FromSeconds(1));
        var usageAfterRestart = new RepoUsageService(store);

        using var vmAfterRestart = new BlacklistedItemsViewModel(usageAfterRestart, dispatcher.Object);

        Assert.That(
            vmAfterRestart.Items,
            Has.Count.EqualTo(1),
            "Blacklisted item should be restored after restarting the usage service.");
    }

    [Test]
    public void RestoreCommand_TogglesBlacklistWhenItemPresent()
    {
        var usage = new Mock<IRepoUsageService>();
        usage
            .Setup(u => u.GetBlacklistedItems())
            .Returns(new List<RepoBlacklistItem> { new() { RepoName = "Hidden", RepoPath = "C:/dev/hidden" } });
        usage
            .Setup(u => u.IsBlacklisted("Hidden", "C:/dev/hidden"))
            .Returns(true);
        usage
            .Setup(u => u.ToggleBlacklisted("Hidden", "C:/dev/hidden"))
            .Returns(false);

        var dispatcher = CreateDispatcher();

        using var vm = new BlacklistedItemsViewModel(usage.Object, dispatcher.Object);

        var item = vm.Items[0];
        item.RestoreCommand.Execute(null);

        usage.Verify(u => u.ToggleBlacklisted("Hidden", "C:/dev/hidden"), Times.Once);
    }

    [Test]
    public void ChangedEvent_ReloadsItems()
    {
        var usage = new Mock<IRepoUsageService>();
        usage
            .SetupSequence(u => u.GetBlacklistedItems())
            .Returns(new List<RepoBlacklistItem> { new() { RepoName = "Hidden", RepoPath = "C:/dev/hidden" } })
            .Returns(new List<RepoBlacklistItem>());

        var dispatcher = CreateDispatcher();

        using var vm = new BlacklistedItemsViewModel(usage.Object, dispatcher.Object);

        usage.Raise(u => u.Changed += null, EventArgs.Empty);

        Assert.That(vm.Items, Is.Empty);
        Assert.That(vm.HasItems, Is.False);
        Assert.That(vm.HasNoItems, Is.True);
    }

    private static Mock<IUiDispatcher> CreateDispatcher()
    {
        var dispatcher = new Mock<IUiDispatcher>();
        dispatcher
            .Setup(d => d.CheckAccess())
            .Returns(true);
        dispatcher
            .Setup(d => d.Invoke(It.IsAny<Action>()))
            .Callback<Action>(a => a());
        return dispatcher;
    }

    private sealed class DeferredRepoUsageStore : IRepoUsageStore
    {
        private RepoUsageState _persisted = new();
        private int _writeCount;

        public Task<RepoUsageState> ReadAsync(CancellationToken ct) => Task.FromResult(Clone(_persisted));

        public Task WriteAsync(RepoUsageState state, CancellationToken ct)
        {
            _persisted = Clone(state);
            Interlocked.Increment(ref _writeCount);
            return Task.CompletedTask;
        }

        public int WriteCount => Volatile.Read(ref _writeCount);
        
        private static RepoUsageState
            Clone(RepoUsageState source) =>
            new()
            {
                Entries = source
                    .Entries
                    .Select(e => e with { })
                    .ToList(),
                PinnedPaths = new List<string>(source.PinnedPaths),
                PinnedNames = new List<string>(source.PinnedNames),
                BlacklistedPaths = new List<string>(source.BlacklistedPaths),
                BlacklistedNames = new List<string>(source.BlacklistedNames),
                BlacklistedItems = source
                    .BlacklistedItems
                    .Select(i => i with { })
                    .ToList()
            };
    }
}