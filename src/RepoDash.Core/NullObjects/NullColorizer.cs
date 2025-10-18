using RepoDash.Core.Abstractions;

namespace RepoDash.Core.NullObjects;

public sealed class NullColorizer : IColorizer
{
    public uint? GetBackgroundColorFor(string key) => null;
    public uint? GetForegroundColorFor(string key) => null;
}
