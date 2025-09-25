using System;
using System.Linq;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Models;
using RepoDash.Core.Settings;

namespace RepoDash.Infrastructure.Color;

public sealed class NameToBrushColorizer : IColorizer
{
    private readonly Func<ColorRulesSettings> _settingsAccessor;

    public NameToBrushColorizer(Func<ColorRulesSettings> settingsAccessor)
    {
        _settingsAccessor = settingsAccessor;
    }

    public bool TryGetColor(string input, out ColorSwatch swatch)
    {
        var settings = _settingsAccessor();
        foreach (var group in settings.Groups)
        {
            if (group.Keywords.Any(keyword =>
                    input.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                if (!string.IsNullOrWhiteSpace(group.HexColor))
                {
                    swatch = ColorSwatch.FromHex(group.HexColor);
                    return true;
                }

                var derived = DeriveFromKeyword(group.Keywords.FirstOrDefault() ?? input);
                swatch = derived;
                return true;
            }
        }

        swatch = default;
        return false;
    }

    private static ColorSwatch DeriveFromKeyword(string keyword)
    {
        var hash = keyword.Aggregate(17, (current, ch) => current * 31 + ch);
        var r = (byte)((hash >> 16) & 0xFF);
        var g = (byte)((hash >> 8) & 0xFF);
        var b = (byte)(hash & 0xFF);
        return new ColorSwatch(r, g, b);
    }
}
