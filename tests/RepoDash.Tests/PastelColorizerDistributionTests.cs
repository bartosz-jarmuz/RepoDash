using NUnit.Framework;

namespace RepoDash.Tests;

using System.Linq;
using RepoDash.Core.Color;


[TestFixture]
public sealed class PastelColorizerDistributionTests
{
    [Test, Retry(3)]
    public void InBatchesOfTen_NoMoreThanTwoShareSameBaseHue()
    {
        var batches = new[]
        {
            new[] { "applications", "components", "contracts", "integration-tests", "misc", "sql", "utilities", "ops", "security", "docs" },
            new[] { "alpha", "beta", "gamma", "delta", "epsilon", "zeta", "eta", "theta", "iota", "kappa" },
            new[] { "red", "orange", "yellow", "green", "teal", "blue", "indigo", "violet", "pink", "magenta" },
            new[] { "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten" }
        };

        foreach (var batch in batches)
        {
            var counts = new int[PastelColorizer.BaseHueCount];

            foreach (var name in batch)
            {
                int idx = PastelColorizer.GetBaseHueIndex(name);
                counts[idx]++;
            }

            foreach (var c in counts)
            {
                Assert.That(c <= 2, $"A base hue was used {c} times in a batch of 10: [{string.Join(", ", batch)}]");
            }

            int distinct = counts.Count(x => x > 0);
            Assert.That(distinct >= 5, $"Expected at least 5 distinct base hues, got {distinct} for batch: [{string.Join(", ", batch)}]");
        }
    }
}