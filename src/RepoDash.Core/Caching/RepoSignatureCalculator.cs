namespace RepoDash.Core.Caching;

public static class RepoSignatureCalculator
{
    public static string Compute(string repoPath, string? solutionPath)
    {
        var head = Path.Combine(repoPath, ".git", "HEAD");
        var headTicks = File.Exists(head) ? File.GetLastWriteTimeUtc(head).Ticks : 0L;

        var slnTicks = solutionPath != null && File.Exists(solutionPath)
            ? File.GetLastWriteTimeUtc(solutionPath).Ticks
            : 0L;

        unchecked
        {
            var x = 17;
            x = x * 31 + headTicks.GetHashCode();
            x = x * 31 + slnTicks.GetHashCode();
            return x.ToString();
        }
    }
}