using NUnit.Framework;

namespace RepoDash.Tests;

[TestFixture]
public sealed class SampleTests
{
    [Test]
    public void Sanity()
    {
        Assert.That(2 + 2, Is.EqualTo(4));
    }
}