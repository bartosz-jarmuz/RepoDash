using System;
using System.Globalization;
using System.Windows.Data;

namespace RepoDash.App.Converters;

public sealed class ItemCountToHeightConverter : IValueConverter
{
    /// <summary>
    /// Approximate pixel height for one list item (adjust to match your ItemTemplate).
    /// </summary>
    public double ItemHeight { get; set; } = 32;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count && count > 0)
            return count * ItemHeight;
        return ItemHeight * 5; // fallback (e.g. show 5 rows if setting is invalid)
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}