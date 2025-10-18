namespace RepoDash.Core.Color;

using System;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Settings;

public sealed class PastelColorizer : IColorizer
{
    private readonly ISettingsStore<ColorSettings> _store;

    // Curated base palette (Hue°, Saturation, Lightness) before per-key jitter.
    private static readonly (double HueDeg, double Saturation, double Lightness)[] BaseColors = new (double HueDeg, double Saturation, double Lightness)[]
    {
        (HueDeg: 352.2033898305, Saturation: 0.6996047431, Lightness: 0.5260784314), // Crimson
        (HueDeg: 24.5814977974, Saturation: 0.9497907950, Lightness: 0.5613725490),  // Vermilion
        (HueDeg: 42.0,             Saturation: 0.9134615385, Lightness: 0.6221568627),  // Amber
        (HueDeg: 76.7213114754, Saturation: 0.5041322314, Lightness: 0.5554901961),  // Chartreuse
        (HueDeg: 153.4426229508, Saturation: 0.4039735099, Lightness: 0.3260784314), // Forest
        (HueDeg: 187.0754716981, Saturation: 1.0,          Lightness: 0.6143137255), // Seafoam
        (HueDeg: 199.9038461538, Saturation: 0.8455284553, Lightness: 0.5476470588), // Cerulean
        (HueDeg: 216.0,           Saturation: 0.625,        Lightness: 0.4064705882), // Cobalt
        (HueDeg: 262.4137931034, Saturation: 0.696,        Lightness: 0.5398039216), // Royal Purple
        (HueDeg: 330.1840490798, Saturation: 0.6653061224, Lightness: 0.5496078431), // Magenta
        (HueDeg: 22.6229508197,  Saturation: 0.5041322314, Lightness: 0.5045098039), // Copper
        (HueDeg: 240.0,          Saturation: 0.0533333333, Lightness: 0.7358823529)  // Silver
    };

    // Hash buckets (0..15) remapped to the 12 curated base colors.
    private static readonly int[] BaseIndexMap =
    {
        9,  // bucket 0  -> Magenta
        1,  // bucket 1  -> Vermilion
        2,  // bucket 2  -> Amber
        0,  // bucket 3  -> Crimson
        4,  // bucket 4  -> Forest
        5,  // bucket 5  -> Seafoam
        3,  // bucket 6  -> Chartreuse
        6,  // bucket 7  -> Cerulean
        8,  // bucket 8  -> Royal Purple
        7,  // bucket 9  -> Cobalt
        10, // bucket 10 -> Copper
        11, // bucket 11 -> Silver
        5,  // bucket 12 -> Seafoam (dup)
        9,  // bucket 13 -> Magenta (dup)
        6,  // bucket 14 -> Cerulean (dup)
        10  // bucket 15 -> Copper (dup)
    };

    // Per-string gentle variation so items in the same base hue don't look identical
    private const double HueJitterDegrees = 14.0;  // ±14°
    private const double SatJitter = 0.18;         // ±18%
    private const double LightJitter = 0.18;       // ±18%

    private const double BackgroundBiasBase = 0.12;
    private const double BackgroundBiasRange = 0.12;
    private const double ForegroundBiasBase = -0.12;
    private const double ForegroundBiasRange = -0.12;

    public PastelColorizer(ISettingsStore<ColorSettings> store)
    {
        _store = store;
    }

    public uint? GetBackgroundColorFor(string key)
    {
        var cfg = _store.Current;
        var (h, s, l, a) = GenerateHsl(key, cfg);
        double normalized = (cfg.BackgroundLightnessPercent - 50) / 50.0;
        double bias = BackgroundBiasBase + BackgroundBiasRange * (1.0 + normalized);
        return ComposeFromHsl(h, s, Clamp01(l + bias), a);
    }

    public uint? GetForegroundColorFor(string key)
    {
        var cfg = _store.Current;
        var (h, s, l, a) = GenerateHsl(key, cfg);
        double normalized = (cfg.ForegroundDarknessPercent - 50) / 50.0;
        double bias = ForegroundBiasBase + ForegroundBiasRange * (1.0 + normalized);
        return ComposeFromHsl(h, s, Clamp01(l + bias), a);
    }

    // === Internals exposed to tests (no UI usage) ===

    internal static int BaseHueCount => BaseColors.Length;

    // Stable base-hue index in [0, BaseHueCount).
    // Hashes into 16 evenly spaced buckets, then remaps into the curated 12-color palette to keep batches balanced.
    internal static int GetBaseHueIndex(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return 0;

        uint h = Fmix32(Fnv1A32(key));
        const uint fib = 2654435761u; // 2^32 / φ (Fibonacci hashing)
        uint scaled = h * fib;

        int bucket = (int)(scaled >> 28); // 0..15
        return BaseIndexMap[bucket];
    }

    // --- helpers ---

    private static uint Fnv1A32(string s)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var ch in s)
            {
                hash ^= ch;
                hash *= 16777619;
            }
            return hash;
        }
    }

    private static uint Fmix32(uint h)
    {
        unchecked
        {
            h ^= h >> 16;
            h *= 0x85ebca6b;
            h ^= h >> 13;
            h *= 0xc2b2ae35;
            h ^= h >> 16;
            return h;
        }
    }

    private static (byte r, byte g, byte b) HslToRgb(double h, double s, double l)
    {
        if (s <= 0)
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

    private static byte ToAlpha(int opacityPercent)
    {
        if (opacityPercent <= 0) return 0;
        if (opacityPercent >= 100) return 255;
        return (byte)Math.Round(opacityPercent / 100.0 * 255.0);
    }

    private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);

    private static uint Compose(byte a, byte r, byte g, byte b)
        => ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;

    private static uint ComposeFromHsl(double h, double s, double l, byte a)
    {
        var (r, g, b) = HslToRgb(h, s, l);
        return Compose(a, r, g, b);
    }

    private (double Hue, double Saturation, double Lightness, byte Alpha) GenerateHsl(string key, ColorSettings cfg)
    {
        byte alpha = ToAlpha(cfg.AutomaticGroupOpacityPercent);

        if (string.IsNullOrWhiteSpace(key))
        {
            // Neutral light gray for unnamed groups.
            return (0.0, 0.0, 0.9372549019607843, alpha); // #EFEFEF
        }

        uint h = Fmix32(Fnv1A32(key));

        int baseIndex = GetBaseHueIndex(key);
        var baseColor = BaseColors[baseIndex];
        double hueBase = baseColor.HueDeg / 360.0;

        // per-string bounded tweaks (don’t drift far from the base)
        double hueJ = (((int)((h >> 11) & 0x3FF) / 1023.0) - 0.5) * 2.0 * (HueJitterDegrees / 360.0);
        double sJ = (((int)((h >> 7) & 0xFF) / 255.0) - 0.5) * 2.0 * SatJitter;
        double lJ = (((int)((h >> 17) & 0xFF) / 255.0) - 0.5) * 2.0 * LightJitter;

        double s = Clamp01(baseColor.Saturation + sJ);
        double l = Clamp01(baseColor.Lightness + lJ);
        double hFinal = hueBase + hueJ;
        hFinal = hFinal - Math.Floor(hFinal); // wrap 0..1

        return (hFinal, s, l, alpha);
    }
}
