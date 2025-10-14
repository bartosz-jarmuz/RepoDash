using Moq;
using NUnit.Framework;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Settings;
using RepoDash.Infrastructure.Scanning;
using RepoDash.Tests.TestingUtilities;

namespace RepoDash.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public sealed class FileSystemRepoScannerTests
    {
        [Test]
        public void ScanAsync_LargeSystem_AllAndOnlyCreatedReposAreDiscovered()
        {
            using var sandbox = new TestSandbox();
            var layout = sandbox.CreateLargeSystem();

            var scanner = MakeScanner(new RepositoriesSettings());
            var repos = scanner.ScanAsync(layout.Root, groupingSegment: 2, CancellationToken.None).ToBlockingEnumerable();

            var discovered = repos.Select(r => r.RepoPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var expected = layout.AllRepos.ToHashSet(StringComparer.OrdinalIgnoreCase);

            Assert.That(discovered.Count, Is.EqualTo(expected.Count));
            Assert.That(discovered.SetEquals(expected), Is.True, "Discovered repos must exactly equal the sandbox repos (no more, no less).");

            // Non-repo container must NOT be included
            Assert.That(discovered.Contains(layout.NonRepoContainerPath), Is.False);
        }

        [Test]
        public void ScanAsync_SqlReposWithoutSolution_AreIncludedWithHasSolutionFalse()
        {
            using var sandbox = new TestSandbox();
            var layout = sandbox.CreateLargeSystem();

            var scanner = MakeScanner(new RepositoriesSettings());
            var repos = scanner.ScanAsync(layout.Root, groupingSegment: 2, CancellationToken.None).ToBlockingEnumerable();

            foreach (var sqlPath in layout.Sql)
            {
                var match = repos.Single(r => PathsEqual(r.RepoPath, sqlPath));
                Assert.That(match.HasSolution, Is.False);
                Assert.That(Directory.EnumerateFiles(sqlPath, "*.sln", SearchOption.TopDirectoryOnly).Any(), Is.False);
            }
        }

        [Test]
        public void ScanAsync_IgnoredFragments_ByRepoPath_ExactlyIgnoredReposAreExcluded()
        {
            using var sandbox = new TestSandbox();
            var layout = sandbox.CreateLargeSystem();

            var settings = new RepositoriesSettings();
            settings.ExcludedPathParts.Add(Path.DirectorySeparatorChar + "utilities" + Path.DirectorySeparatorChar);

            var scanner = MakeScanner(settings);
            var repos = scanner.ScanAsync(layout.Root, groupingSegment: 2, CancellationToken.None).ToBlockingEnumerable();

            var discovered = repos.Select(r => r.RepoPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var expected = layout.AllRepos.Except(layout.Utilities, StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);

            Assert.That(discovered.SetEquals(expected), Is.True);
            Assert.That(discovered.Intersect(layout.Utilities, StringComparer.OrdinalIgnoreCase).Any(), Is.False);
        }

        [Test]
        public void ScanAsync_IgnoredFragments_BySolutionName_OnlyMatchingReposAreExcluded()
        {
            using var sandbox = new TestSandbox();
            var layout = sandbox.CreateLargeSystem();

            var target = layout.Utilities.Single(p => p.EndsWith(Path.Combine("utilities", "security", "SecretRotator")));
            var settings = new RepositoriesSettings();
            settings.ExcludedPathParts.Add("  SecretRotator  "); // also tests trim + case-insensitive

            var scanner = MakeScanner(settings);
            var repos = scanner.ScanAsync(layout.Root, groupingSegment: 2, CancellationToken.None).ToBlockingEnumerable();

            var discovered = repos.Select(r => r.RepoPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var expected = layout.AllRepos
                                 .Where(p => !PathsEqual(p, target))
                                 .ToHashSet(StringComparer.OrdinalIgnoreCase);

            Assert.That(discovered.SetEquals(expected), Is.True);
            Assert.That(discovered.Contains(target), Is.False);
        }

        [Test]
        public void ScanAsync_CategoryOverride_FirstMatchWins_WhenMultipleRulesMatch()
        {
            using var sandbox = new TestSandbox();
            var layout = sandbox.CreateLargeSystem();

            var target = layout.Services.Single(p => p.EndsWith(Path.Combine("services", "search", "SearchService")));

            var settings = new RepositoriesSettings();
            // Both rules can match (path contains "search" and solution contains "Service")
            settings.CategoryOverrides.Add(new CategoryOverride { Category = "Edge", Matches = { "search" } });
            settings.CategoryOverrides.Add(new CategoryOverride { Category = "Platform", Matches = { "Service" } });

            var scanner = MakeScanner(settings);
            var repos = scanner.ScanAsync(layout.Root, groupingSegment: 2, CancellationToken.None).ToBlockingEnumerable();

            var hit = repos.Single(r => PathsEqual(r.RepoPath, target));
            Assert.That(hit.GroupKey, Is.EqualTo("Edge")); // first match wins
        }

        [Test]
        public void ScanAsync_CategoryOverride_BySolutionName_GroupKeyIsOverridden()
        {
            using var sandbox = new TestSandbox();
            var layout = sandbox.CreateLargeSystem();

            var target = layout.Services.Single(p => p.EndsWith(Path.Combine("services", "users", "UserProfileService")));

            var settings = new RepositoriesSettings();
            settings.CategoryOverrides.Add(new CategoryOverride { Category = "Contracts", Matches = { "userprofileservice" } }); // case-insensitive

            var scanner = MakeScanner(settings);
            var repos = scanner.ScanAsync(layout.Root, groupingSegment: 2, CancellationToken.None).ToBlockingEnumerable();

            var hit = repos.Single(r => PathsEqual(r.RepoPath, target));
            Assert.That(hit.GroupKey, Is.EqualTo("Contracts"));
        }

        [Test]
        public void ScanAsync_GroupingSegment_NthSegmentFromEnd_GroupKeyChangesAsExpected()
        {
            using var sandbox = new TestSandbox();
            var layout = sandbox.CreateLargeSystem();

            var target = layout.Services.Single(p => p.EndsWith(Path.Combine("services", "orders", "OrderService")));
            var scanner = MakeScanner(new RepositoriesSettings());

            var r1 = scanner.ScanAsync(layout.Root, groupingSegment: 1, CancellationToken.None).ToBlockingEnumerable();
            var s1 = r1.Single(r => PathsEqual(r.RepoPath, target));
            Assert.That(s1.GroupKey, Is.EqualTo("OrderService"));

            var r2 = scanner.ScanAsync(layout.Root, groupingSegment: 2, CancellationToken.None).ToBlockingEnumerable();
            var s2 = r2.Single(r => PathsEqual(r.RepoPath, target));
            Assert.That(s2.GroupKey, Is.EqualTo("orders"));

            var r3 = scanner.ScanAsync(layout.Root, groupingSegment: 3, CancellationToken.None).ToBlockingEnumerable();
            var s3 = r3.Single(r => PathsEqual(r.RepoPath, target));
            Assert.That(s3.GroupKey, Is.EqualTo("services"));
        }

        [Test]
        public void ScanAsync_SortedByGroupKeyThenRepoName_ResultsAreOrdered()
        {
            using var sandbox = new TestSandbox();
            var layout = sandbox.CreateLargeSystem();

            var scanner = MakeScanner(new RepositoriesSettings());
            var repos = scanner.ScanAsync(layout.Root, groupingSegment: 2, CancellationToken.None).ToBlockingEnumerable();

            var projection = repos.Select(r => (r.GroupKey, r.RepoName)).ToList();
            Assert.That(IsSortedByGroupThenName(projection), Is.True, "Repos should be ordered by GroupKey then RepoName (case-insensitive).");
        }

        [Test]
        public void ScanAsync_NonRepoContainerFolder_PresentUnderRoot_NotDiscovered()
        {
            using var sandbox = new TestSandbox();
            var layout = sandbox.CreateLargeSystem();

            var scanner = MakeScanner(new RepositoriesSettings());
            var repos = scanner.ScanAsync(layout.Root, groupingSegment: 1, CancellationToken.None).ToBlockingEnumerable();

            var paths = repos.Select(r => r.RepoPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.That(Directory.Exists(layout.NonRepoContainerPath), Is.True);
            Assert.That(paths.Contains(layout.NonRepoContainerPath), Is.False);
        }

        [Test]
        public void ScanAsync_NonExistentRoot_ReturnsEmpty()
        {
            var nonExisting = Path.Combine(Path.GetTempPath(), "RepoDash_NotThere", Path.GetRandomFileName());
            var scanner = MakeScanner(new RepositoriesSettings());
            var result = scanner.ScanAsync(nonExisting, groupingSegment: 1, CancellationToken.None).ToBlockingEnumerable();
            Assert.That(result, Is.Empty);
        }

        // ---- helpers ----

        private static FileSystemRepoScanner MakeScanner(RepositoriesSettings reposSettings)
        {
            var store = new Mock<ISettingsStore<RepositoriesSettings>>();
            store.SetupGet(s => s.Current).Returns(reposSettings);
            return new FileSystemRepoScanner(store.Object);
        }

        private static bool PathsEqual(string a, string b) =>
            string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar),
                          Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar),
                          StringComparison.OrdinalIgnoreCase);

        private static bool IsSortedByGroupThenName(IReadOnlyList<(string GroupKey, string RepoName)> items)
        {
            for (int i = 1; i < items.Count; i++)
            {
                var prev = items[i - 1];
                var curr = items[i];

                var groupCmp = string.Compare(prev.GroupKey, curr.GroupKey, StringComparison.OrdinalIgnoreCase);
                if (groupCmp > 0) return false;
                if (groupCmp == 0)
                {
                    var nameCmp = string.Compare(prev.RepoName, curr.RepoName, StringComparison.OrdinalIgnoreCase);
                    if (nameCmp > 0) return false;
                }
            }
            return true;
        }
    }
}
