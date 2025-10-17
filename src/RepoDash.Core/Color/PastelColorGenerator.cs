namespace RepoDash.Core.Color;

using System;
using System.Globalization;

public static class PastelColorGenerator
{
    public static uint FromString(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return 0xFFEFEFEF; // safe light gray

        unchecked
        {
            uint hash = 2166136261;
            foreach (var ch in key)
            {
                hash ^= ch;
                hash *= 16777619;
            }

            // Hue from 0..360, Saturation low (0.20..0.35), Lightness high (0.78..0.88)
            double h = (hash % 360u);
            double s = 0.20 + (hash % 15u) / 100.0; // 0.20 .. 0.35
            double l = 0.78 + (hash % 10u) / 100.0; // 0.78 .. 0.88

            var (r, g, b) = HslToRgb(h / 360.0, s, l);
            byte a = 0xFF;
            return ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
        }
    }

    public static bool TryParseHex(string hex, out uint argb)
    {
        argb = 0;
        if (string.IsNullOrWhiteSpace(hex)) return false;

        var s = hex.Trim();
        if (s.StartsWith("#", StringComparison.Ordinal)) s = s[1..];

        if (s.Length == 6)
        {
            // Assume opaque RGB
            if (uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            {
                argb = 0xFF000000 | rgb;
                return true;
            }
            return false;
        }

        if (s.Length == 8)
            return uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out argb);

        return false;
    }

    public static string ToHex(uint argb) => $"#{argb:X8}";

    private static (byte r, byte g, byte b) HslToRgb(double h, double s, double l)
    {
        if (s == 0)
        {
            var v = (byte)Math.Round(l * 255.0);
            return (v, v, v);
        }

        double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        double p = 2 * l - q;

        byte r = (byte)Math.Round(HueToRgb(p, q, h + 1.0 / 3.0) * 255.0);
        byte g = (byte)Math.Round(HueToRgb(p, q, h) * 255.0);
        byte b = (byte)Math.Round(HueToRgb(p, q, h - 1.0 / 3.0) * 255.0);
        return (r, g, b);
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2.0) return q;
        if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
        return p;
    }
}
