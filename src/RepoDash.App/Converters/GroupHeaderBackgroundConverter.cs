namespace RepoDash.App.Converters;

using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using RepoDash.App.ViewModels;
using RepoDash.Core.Color;
using RepoDash.Core.Settings;
using RepoDash.Core.Abstractions;

public sealed class GroupHeaderBackgroundConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        // values[0] = RepoGroupViewModel
        // values[1] = SettingsChangeNotifier.Version (int) — unused, only to trigger re-eval
        if (values.Length == 0 || values[0] is not RepoGroupViewModel vm)
            return Brushes.Transparent;

        var sp = App.Services;
        var store = sp.GetRequiredService<ISettingsStore<ColorSettings>>();
        var cfg = store.Current;

        var key = vm.InternalKey ?? string.Empty;

        if (string.Equals(key, "__special_recent", StringComparison.OrdinalIgnoreCase))
        {
            if (!cfg.AddColorToRecentGroupBox) return Brushes.Transparent;
            return ToBrush(cfg.RecentGroupColor);
        }

        if (string.Equals(key, "__special_frequent", StringComparison.OrdinalIgnoreCase))
        {
            if (!cfg.AddColorToFrequentGroupBox) return Brushes.Transparent;
            return ToBrush(cfg.FrequentGroupColor);
        }

        if (!cfg.AddColorToAutomaticGroupBoxes) return Brushes.Transparent;

        var displayName = vm.GroupKey ?? string.Empty;

        var overrideHex = cfg.GroupColorOverrides
            .FirstOrDefault(o => string.Equals(o.GroupName, displayName, StringComparison.OrdinalIgnoreCase))
            ?.ColorCode;

        if (!string.IsNullOrWhiteSpace(overrideHex))
            return ToBrush(overrideHex!);

        var argb = PastelColorGenerator.FromString(displayName);
        return new SolidColorBrush(Color.FromArgb(
            (byte)((argb >> 24) & 0xFF),
            (byte)((argb >> 16) & 0xFF),
            (byte)((argb >> 8) & 0xFF),
            (byte)(argb & 0xFF)));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static SolidColorBrush ToBrush(string hex)
    {
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(hex)!;
            return new SolidColorBrush(c);
        }
        catch
        {
            return Brushes.Transparent;
        }
    }
}
