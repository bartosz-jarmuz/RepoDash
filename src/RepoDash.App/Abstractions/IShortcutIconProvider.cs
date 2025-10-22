namespace RepoDash.App.Abstractions;

using RepoDash.Core.Settings;
using System.Windows.Media;

public interface IShortcutIconProvider
{
    ImageSource? GetIcon(ShortcutEntry entry);
}