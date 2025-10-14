using Moq;
using NUnit.Framework;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Caching;
using RepoDash.Tests.TestingUtilities;

namespace RepoDash.Tests;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public sealed class RepoCacheServiceTests
{
    [Test]
    public async Task LoadFromCache_ReturnsSnapshot_WithoutInvokingScanner()
    {
        var store = new InMemoryCacheStore();
        var scanner = new Mock<IRepoScanner>(MockBehavior.Strict); // must not be called
        var branches = new Mock<IBranchProvider>(MockBehavior.Loose);

        var svc = new RepoCacheService(store, scanner.Object, branches.Object);

        var cached = new RepoRootCache
        {
            NormalizedRoot = "C_dev",
            CachedAtUtc = DateTimeOffset.UtcNow,
            Repos = new()
            {
                new CachedRepo
                {
                    RepoName = "A",
                    RepoPath = @"C:\dev\A",
                    HasGit = true,
                    HasSolution = true,
                    SolutionPath = @"C:\dev\A\A.sln",
                    GroupKey = "apps",
                    Signature = "sig-a",
                    LastSeenUtc = DateTimeOffset.UtcNow
                }
            }
        };
        await store.WriteAsync("C_dev", cached, CancellationToken.None);

        var snapshot = await svc.LoadFromCacheAsync(@"C:\dev", CancellationToken.None);

        Assert.That(snapshot.Count, Is.EqualTo(1));
        Assert.That(snapshot[0].RepoName, Is.EqualTo("A"));
        scanner.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Refresh_UpsertsChangedAndNew_RemovesMissing_AndPersists()
    {
        using var sandbox = new TestSandbox();
        var layout = sandbox.CreateLargeSystem();

        // pick two repos from sandbox
        var repoChanged = layout.Services.First();
        var repoUnchanged = layout.Apps.First();

        var slnChanged = Directory.EnumerateFiles(repoChanged, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
        var slnUnchanged = Directory.EnumerateFiles(repoUnchanged, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();

        var existing = new CachedRepo
        {
            RepoName = Path.GetFileName(repoUnchanged),
            RepoPath = repoUnchanged,
            HasGit = true,
            HasSolution = slnUnchanged != null,
            SolutionPath = slnUnchanged,
            GroupKey = "apps",
            Signature = RepoSignatureCalculator.Compute(repoUnchanged, slnUnchanged),
            LastSeenUtc = DateTimeOffset.UtcNow.AddDays(-1)
        };

        // cache has one entry that will stay (unchanged) and one that will be removed later
        var store = new InMemoryCacheStore();
        await store.WriteAsync(NormalizeRoot(layout.Root), new RepoRootCache
        {
            NormalizedRoot = NormalizeRoot(layout.Root),
            CachedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            Repos = new() { existing, new CachedRepo {
                RepoName = "ToRemove",
                RepoPath = Path.Combine(layout.Root, "will", "be", "removed"),
                HasGit = true,
                HasSolution = false,
                GroupKey = "misc",
                Signature = "old",
                LastSeenUtc = DateTimeOffset.UtcNow.AddDays(-2)
            } }
        }, CancellationToken.None);

        // Change HEAD timestamp for repoChanged → signature must flip
        var headChanged = Path.Combine(repoChanged, ".git", "HEAD");
        File.SetLastWriteTimeUtc(headChanged, DateTime.UtcNow.AddMinutes(5));

        // New repo from sandbox (not present in cache)
        var repoNew = layout.Utilities.First();
        var slnNew = Directory.EnumerateFiles(repoNew, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();

        // Scanner stream = [unchanged, changed, new]
        var stream = new[]
        {
            new RepoInfo(Path.GetFileName(repoUnchanged), repoUnchanged, true, slnUnchanged != null, slnUnchanged, "apps"),
            new RepoInfo(Path.GetFileName(repoChanged), repoChanged, true, slnChanged != null, slnChanged, "services"),
            new RepoInfo(Path.GetFileName(repoNew), repoNew, true, slnNew != null, slnNew, "utilities")
        };
        var scanner = new FakeScanner(stream);

        var branches = new Mock<IBranchProvider>(MockBehavior.Loose);
        var svc = new RepoCacheService(store, scanner, branches.Object);

        var upserts = new List<CachedRepo>();
        var removed = new List<string>();

        await svc.RefreshAsync(
            rootPath: layout.Root,
            groupingSegment: 2,
            upsert: r => upserts.Add(r),
            removeByRepoPath: p => removed.Add(p),
            CancellationToken.None);

        // removed: the cache entry that scan didn't see
        Assert.That(removed, Does.Contain(Path.Combine(layout.Root, "will", "be", "removed")));

        // upserts: should include the changed repo (signature changed) and the new repo
        var changedSigNow = RepoSignatureCalculator.Compute(repoChanged, slnChanged);
        Assert.That(upserts.Any(u => PathsEqual(u.RepoPath, repoChanged) && u.Signature == changedSigNow), Is.True);
        Assert.That(upserts.Any(u => PathsEqual(u.RepoPath, repoNew)), Is.True);

        // unchanged should NOT be upserted
        Assert.That(upserts.Any(u => PathsEqual(u.RepoPath, repoUnchanged)), Is.False);

        // cache persisted with normalized root key, ordered by GroupKey then RepoName
        var persisted = await store.ReadAsync(NormalizeRoot(layout.Root), CancellationToken.None);
        Assert.That(persisted, Is.Not.Null);
        var ordered = persisted!.Repos
            .OrderBy(r => r.GroupKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.RepoName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        Assert.That(persisted.Repos.Select(r => (r.GroupKey, r.RepoName)).ToList(),
            Is.EqualTo(ordered.Select(r => (r.GroupKey, r.RepoName)).ToList()));
    }

    [Test]
    public void Refresh_RespectsCancellation()
    {
        using var sandbox = new TestSandbox();
        var layout = sandbox.CreateLargeSystem();

        var many = layout.AllRepos.Select(p =>
        {
            var sln = Directory.EnumerateFiles(p, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
            var gk = Path.GetFileName(Path.GetDirectoryName(p) ?? p) ?? "x";
            return new RepoInfo(Path.GetFileName(p), p, true, sln != null, sln, gk);
        }).ToArray();

        var store = new InMemoryCacheStore();
        var scanner = new FakeScanner(many, delayPerItemMs: 1);
        var branches = new Mock<IBranchProvider>(MockBehavior.Loose);
        var svc = new RepoCacheService(store, scanner, branches.Object);

        using var cts = new CancellationTokenSource();
        var task = svc.RefreshAsync(layout.Root, 2, _ => { cts.Cancel(); }, _ => { }, cts.Token);

        Assert.ThrowsAsync<TaskCanceledException>(async () => await task);
    }

    // ————— helpers —————

    static string NormalizeRoot(string path)
    {
        var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return full.Replace(':', '_').Replace('\\', '_').Replace('/', '_');
    }

    static bool PathsEqual(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    private sealed class InMemoryCacheStore : IRepoCacheStore
    {
        private readonly Dictionary<string, RepoRootCache> _byKey = new(StringComparer.OrdinalIgnoreCase);

        public Task<RepoRootCache?> ReadAsync(string normalizedRootPath, CancellationToken ct)
        {
            _byKey.TryGetValue(normalizedRootPath, out var cache);
            return Task.FromResult(cache);
        }

        public Task WriteAsync(string normalizedRootPath, RepoRootCache cache, CancellationToken ct)
        {
            _byKey[normalizedRootPath] = new RepoRootCache
            {
                NormalizedRoot = cache.NormalizedRoot,
                CachedAtUtc = cache.CachedAtUtc,
                Repos = cache.Repos.ToList()
            };
            return Task.CompletedTask;
        }
    }

    private sealed class FakeScanner : IRepoScanner
    {
        private readonly IReadOnlyList<RepoInfo> _items;
        private readonly int _delayMs;

        public FakeScanner(IReadOnlyList<RepoInfo> items, int delayPerItemMs = 0)
        {
            _items = items;
            _delayMs = delayPerItemMs;
        }

        public async IAsyncEnumerable<RepoInfo> ScanAsync(string rootPath, int groupingSegment, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            foreach (var r in _items)
            {
                ct.ThrowIfCancellationRequested();
                if (_delayMs > 0) await Task.Delay(_delayMs, ct);
                yield return r;
            }
        }
    }
}