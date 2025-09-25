using System.IO;
using System.Text;

namespace RepoDash.Persistence.Paths;

public static class PathNormalizer
{
    private static readonly char[] InvalidCharacters = Path.GetInvalidFileNameChars();

    public static string NormalizeRootName(string rootPath)
    {
        var builder = new StringBuilder(rootPath.Length);
        foreach (var ch in rootPath)
        {
            builder.Append(Array.IndexOf(InvalidCharacters, ch) >= 0 ? '_' : char.ToLowerInvariant(ch));
        }

        return builder.ToString();
    }
}
