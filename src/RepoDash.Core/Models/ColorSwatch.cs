using System;

namespace RepoDash.Core.Models;

public readonly record struct ColorSwatch(byte R, byte G, byte B)
{
    public static ColorSwatch FromHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex) || hex.Length is not (6 or 7))
        {
            throw new ArgumentException("Hex must be 6 or 7 characters", nameof(hex));
        }

        var span = hex.TrimStart('#');
        var r = Convert.ToByte(span.Substring(0, 2), 16);
        var g = Convert.ToByte(span.Substring(2, 2), 16);
        var b = Convert.ToByte(span.Substring(4, 2), 16);
        return new ColorSwatch(r, g, b);
    }
}
