using RepoDash.Core.Abstractions;

namespace RepoDash.Core.NullObjects;

public sealed class NullColorizer : IColorizer
{
    public uint? GetColorFor(string key) => null;
}