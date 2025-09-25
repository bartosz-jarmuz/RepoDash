using RepoDash.Core.Abstractions;
using RepoDash.Core.Models;

namespace RepoDash.Infrastructure.Color;

public sealed class NullColorizer : IColorizer
{
    public bool TryGetColor(string input, out ColorSwatch swatch)
    {
        swatch = default;
        return false;
    }
}
