using NUnit.Framework;
using RepoDash.Core.Caching;
using RepoDash.Tests.TestingUtilities;
using NUnit.Framework;
using RepoDash.Core.Caching;
using RepoDash.Tests.TestingUtilities;

namespace RepoDash.Tests;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public sealed class RepoSignatureCalculatorTests
{
    [Test]
    public void Signature_Changes_When_HEAD_Timestamp_Changes()
    {
        using var sandbox = new TestSandbox();
        var layout = sandbox.CreateLargeSystem();

        var repoPath = layout.Apps.First();
        var head = Path.Combine(repoPath, ".git", "HEAD");
        Assert.That(File.Exists(head), Is.True);

        var sln = Directory.EnumerateFiles(repoPath, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();

        var s1 = RepoSignatureCalculator.Compute(repoPath, sln);

        // advance HEAD timestamp
        File.SetLastWriteTimeUtc(head, DateTime.UtcNow.AddMinutes(1));

        var s2 = RepoSignatureCalculator.Compute(repoPath, sln);
        Assert.That(s2, Is.Not.EqualTo(s1));
    }

    [Test]
    public void Signature_Changes_When_Solution_Timestamp_Changes()
    {
        using var sandbox = new TestSandbox();
        var layout = sandbox.CreateLargeSystem();

        var repoPath = layout.Services.First(); // services have .sln
        var sln = Directory.EnumerateFiles(repoPath, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
        Assert.That(sln, Is.Not.Null);

        var s1 = RepoSignatureCalculator.Compute(repoPath, sln);

        // advance .sln timestamp
        File.SetLastWriteTimeUtc(sln!, DateTime.UtcNow.AddMinutes(1));

        var s2 = RepoSignatureCalculator.Compute(repoPath, sln);
        Assert.That(s2, Is.Not.EqualTo(s1));
    }

    [Test]
    public void Signature_IsStable_When_Nothing_Changes()
    {
        using var sandbox = new TestSandbox();
        var layout = sandbox.CreateLargeSystem();

        var repoPath = layout.Components.First();
        var sln = Directory.EnumerateFiles(repoPath, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();

        var s1 = RepoSignatureCalculator.Compute(repoPath, sln);
        var s2 = RepoSignatureCalculator.Compute(repoPath, sln);

        Assert.That(s2, Is.EqualTo(s1));
    }
}