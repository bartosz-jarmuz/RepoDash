using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace RepoDash.App.Converters;

using RepoDash.App.ViewModels;
using RepoDash.Core.Abstractions;
using RepoDash.Core.Color;
using RepoDash.Core.Settings;
using System;
using System.Windows.Data;
using System.Windows.Media;

public sealed class HexStringToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = value as string;
        if (string.IsNullOrWhiteSpace(s)) return Colors.Transparent;

        try
        {
            return (Color)ColorConverter.ConvertFromString(s)!;
        }
        catch
        {
            return Colors.Transparent;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Color c)
            return $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
        return "#00000000";
    }
}