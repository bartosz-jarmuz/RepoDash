using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Usage;

namespace RepoDash.Tests.Usage;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public sealed class RepoUsageServiceTests
{
    [Test]
    public void RecordUsage_AddsEntryToRecent()
    {
        var service = CreateService(out _);

        service.RecordUsage(new RepoUsageSnapshot
        {
            RepoName = "Alpha",
            RepoPath = "C:/dev/alpha",
            HasGit = true,
            HasSolution = false
        });

        var recent = service.GetRecent(5);

        Assert.That(recent, Has.Count.EqualTo(1));
        Assert.That(recent[0].RepoName, Is.EqualTo("Alpha"));
        Assert.That(recent[0].UsageCount, Is.EqualTo(1));
    }

    [Test]
    public void RecordUsage_AggregatesFrequencyAcrossRoots()
    {
        var service = CreateService(out _);

        service.RecordUsage(new RepoUsageSnapshot
        {
            RepoName = "Shared",
            RepoPath = "C:/dev/root1/shared",
            HasGit = true,
            HasSolution = false
        });
        service.RecordUsage(new RepoUsageSnapshot
        {
            RepoName = "Shared",
            RepoPath = "D:/work/root2/shared",
            HasGit = true,
            HasSolution = false
        });

        var frequent = service.GetFrequent(5);

        Assert.That(frequent, Has.Count.EqualTo(1));
        Assert.That(frequent[0].RepoName, Is.EqualTo("Shared"));
        Assert.That(frequent[0].UsageCount, Is.EqualTo(2));
    }

    [Test]
    public void GetRecent_RespectsRequestedLimit()
    {
        var service = CreateService(out _);

        service.RecordUsage(new RepoUsageSnapshot { RepoName = "One", RepoPath = "c:/one" });
        service.RecordUsage(new RepoUsageSnapshot { RepoName = "Two", RepoPath = "c:/two" });
        service.RecordUsage(new RepoUsageSnapshot { RepoName = "Three", RepoPath = "c:/three" });

        var recent = service.GetRecent(2);

        Assert.That(recent, Has.Count.EqualTo(2));
    }

    [Test]
    public void GetFrequent_RespectsRequestedLimit()
    {
        var service = CreateService(out _);

        service.RecordUsage(new RepoUsageSnapshot { RepoName = "Alpha", RepoPath = "c:/a" });
        service.RecordUsage(new RepoUsageSnapshot { RepoName = "Beta", RepoPath = "c:/b" });
        service.RecordUsage(new RepoUsageSnapshot { RepoName = "Gamma", RepoPath = "c:/g" });

        var frequent = service.GetFrequent(2);

        Assert.That(frequent, Has.Count.EqualTo(2));
    }

    [Test]
    public void ToggleBlacklisted_AddsEntryToBlacklistedItems()
    {
        var service = CreateService(out _);

        var added = service.ToggleBlacklisted("Hidden", "C:/dev/hidden");
        Assert.That(added, Is.True);

        var items = service.GetBlacklistedItems();
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(items[0].RepoName, Is.EqualTo("Hidden"));
        Assert.That(items[0].RepoPath, Is.EqualTo("C:/dev/hidden"));
    }


    [Test]
    public void PinnedStatePersistsAcrossRestart()
    {
        var store = new InMemoryUsageStore();
        var usage1 = new RepoUsageService(store);

        Assert.That(usage1.TogglePinned("Repo", "C:/dev/repo"), Is.True);
        SpinWait.SpinUntil(() => store.WriteCount > 0, TimeSpan.FromSeconds(1));

        var usage2 = new RepoUsageService(store);
        Assert.That(usage2.IsPinned("Repo", "C:/dev/repo"), Is.True);
    }
    [Test]
    public void TogglePinned_TogglesStateByName()
    {
        var service = CreateService(out _);
        var snapshot = new RepoUsageSnapshot
        {
            RepoName = "Gamma",
            RepoPath = "C:/dev/gamma",
            HasGit = true
        };
        service.RecordUsage(snapshot);

        var pinned = service.TogglePinned(snapshot.RepoName, snapshot.RepoPath);
        Assert.That(pinned, Is.True);
        Assert.That(service.IsPinned(snapshot.RepoName, snapshot.RepoPath), Is.True);

        pinned = service.TogglePinned(snapshot.RepoName, snapshot.RepoPath);
        Assert.That(pinned, Is.False);
        Assert.That(service.IsPinned(snapshot.RepoName, snapshot.RepoPath), Is.False);
    }

    [Test]
    public void ToggleBlacklist_RemovesEntryFromRecentAndFrequent()
    {
        var service = CreateService(out _);
        var snapshot = new RepoUsageSnapshot
        {
            RepoName = "Delta",
            RepoPath = "C:/dev/delta",
            HasGit = true
        };

        service.RecordUsage(snapshot);
        Assert.That(service.GetRecent(5), Has.Count.EqualTo(1));
        Assert.That(service.GetBlacklistedItems(), Is.Empty);

        var blacklisted = service.ToggleBlacklisted(snapshot.RepoName, snapshot.RepoPath);
        Assert.That(blacklisted, Is.True);
        Assert.That(service.IsBlacklisted(snapshot.RepoName, snapshot.RepoPath), Is.True);

        Assert.That(service.GetRecent(5), Is.Empty);
        Assert.That(service.GetFrequent(5), Is.Empty);
        Assert.That(service.GetBlacklistedItems(), Has.Count.EqualTo(1));

        // Toggle back to confirm it reappears
        blacklisted = service.ToggleBlacklisted(snapshot.RepoName, snapshot.RepoPath);
        Assert.That(blacklisted, Is.False);
        Assert.That(service.GetRecent(5), Has.Count.EqualTo(1));
        Assert.That(service.GetBlacklistedItems(), Is.Empty);
    }

    [Test]
    public void RecordUsage_RaisesChangedEvent()
    {
        var service = CreateService(out _);
        var fired = 0;
        service.Changed += (_, __) => Interlocked.Increment(ref fired);

        service.RecordUsage(new RepoUsageSnapshot
        {
            RepoName = "Omega",
            RepoPath = "C:/dev/omega"
        });

        Assert.That(fired, Is.EqualTo(1));
    }

    private static RepoUsageService CreateService(out Mock<IRepoUsageStore> storeMock)
    {
        storeMock = new Mock<IRepoUsageStore>();
        storeMock
            .Setup(s => s.ReadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RepoUsageState());
        storeMock
            .Setup(s => s.WriteAsync(It.IsAny<RepoUsageState>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new RepoUsageService(storeMock.Object);
    }
}


internal sealed class InMemoryUsageStore : IRepoUsageStore
{
        private RepoUsageState _state = new();
        private int _writeCount;

        public int WriteCount => Volatile.Read(ref _writeCount);

        public Task<RepoUsageState> ReadAsync(CancellationToken ct) => Task.FromResult(Clone(_state));

        public Task WriteAsync(RepoUsageState state, CancellationToken ct)
        {
            _state = Clone(state);
            Interlocked.Increment(ref _writeCount);
            return Task.CompletedTask;
        }

        private static RepoUsageState Clone(RepoUsageState source) => new()
        {
            Entries = source.Entries.Select(e => e with { }).ToList(),
            PinnedPaths = new List<string>(source.PinnedPaths),
            PinnedNames = new List<string>(source.PinnedNames),
            BlacklistedPaths = new List<string>(source.BlacklistedPaths),
            BlacklistedNames = new List<string>(source.BlacklistedNames),
            BlacklistedItems = source.BlacklistedItems.Select(i => i with { }).ToList()
        };
    }
