using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using RepoDash.Core.Abstractions;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace RepoDash.App.Converters;

public enum StoryColorMode
{
    Background,
    Foreground
}

public sealed class StoryReferenceToBrushConverter : IValueConverter
{
    public StoryColorMode Mode { get; set; } = StoryColorMode.Background;

    public IColorizer? Colorizer { get; set; }

    public object? Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        var story = (value as string)?.Trim();
        if (string.IsNullOrWhiteSpace(story))
            return Mode == StoryColorMode.Background ? Brushes.Transparent : Brushes.Black;

        var colorizer = ResolveColorizer();
        if (colorizer is null)
            return Mode == StoryColorMode.Background ? Brushes.Transparent : Brushes.Black;

        uint? argb = Mode == StoryColorMode.Background
            ? colorizer.GetBackgroundColorFor(story)
            : colorizer.GetForegroundColorFor(story);

        if (argb is null)
            return Mode == StoryColorMode.Background ? Brushes.Transparent : Brushes.Black;

        var color = Color.FromArgb(
            (byte)((argb.Value >> 24) & 0xFF),
            (byte)((argb.Value >> 16) & 0xFF),
            (byte)((argb.Value >> 8) & 0xFF),
            (byte)(argb.Value & 0xFF));

        var brush = new SolidColorBrush(color);
        if (brush.CanFreeze) brush.Freeze();
        return brush;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private IColorizer? ResolveColorizer()
    {
        if (Colorizer is not null) return Colorizer;

        try
        {
            return App.Services.GetRequiredService<IColorizer>();
        }
        catch
        {
            return null;
        }
    }
}
