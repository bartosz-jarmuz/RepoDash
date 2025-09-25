using RepoDash.Core.Models;

namespace RepoDash.Core.Abstractions;

public interface IColorizer
{
    bool TryGetColor(string input, out ColorSwatch swatch);
}
