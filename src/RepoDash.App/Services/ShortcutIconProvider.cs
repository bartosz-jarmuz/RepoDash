namespace RepoDash.App.Services;

using RepoDash.App.Abstractions;
using RepoDash.App.Shell;
using RepoDash.Core.Settings;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;

public sealed class ShortcutIconProvider : IShortcutIconProvider
{
    public ImageSource? GetIcon(ShortcutEntry entry)
    {
        if (entry is null) return null;

        // 1) Custom icon path (absolute or relative to target dir / app dir)
        var resolvedIcon = ResolveIconPath(entry);
        if (resolvedIcon is not null && File.Exists(resolvedIcon))
        {
            var img = LoadImageOrExtractIcon(resolvedIcon, 0);
            if (img is not null) return img;
        }

        // 2) Target path analysis
        var target = ExpandPath(entry.Target.Trim('"', ' '));
        if (IsHttpUrl(target)) return null;

        // 2a) .lnk: read icon location + index, then try; fall back to resolved target’s associated icon
        if (IsLnk(target) && File.Exists(target))
        {
            if (ShellLinkResolver.TryResolve(target, out var linkTarget, out var iconPath, out var iconIndex))
            {
                if (!string.IsNullOrWhiteSpace(iconPath))
                {
                    var iconAbs = MakeAbsolute(iconPath!, baseDir: Path.GetDirectoryName(target));
                    if (File.Exists(iconAbs))
                    {
                        var fromLnkIcon = LoadImageOrExtractIcon(iconAbs, iconIndex);
                        if (fromLnkIcon is not null) return fromLnkIcon;
                    }
                }

                if (!string.IsNullOrWhiteSpace(linkTarget) && File.Exists(linkTarget))
                {
                    var fromTargetAssoc = ExtractAssociatedIcon(linkTarget!);
                    if (fromTargetAssoc is not null) return fromTargetAssoc;
                }
            }

            // Last resort: associated icon for the .lnk itself
            return ExtractAssociatedIcon(target);
        }

        // 2b) Regular file: associated icon
        if (!string.IsNullOrWhiteSpace(target) && File.Exists(target))
        {
            return ExtractAssociatedIcon(target);
        }

        return null;
    }

    private static string? ResolveIconPath(ShortcutEntry e)
    {
        if (string.IsNullOrWhiteSpace(e.IconPath)) return null;
        var icon = ExpandPath(e.IconPath.Trim('"', ' '));

        if (Path.IsPathRooted(icon)) return icon;

        var target = ExpandPath(e.Target.Trim('"', ' '));
        if (!IsHttpUrl(target))
        {
            try
            {
                var baseDir = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(baseDir))
                {
                    var combined = Path.GetFullPath(Path.Combine(baseDir, icon));
                    if (File.Exists(combined)) return combined;
                }
            }
            catch { }
        }

        try
        {
            var appBase = AppContext.BaseDirectory;
            var fallback = Path.GetFullPath(Path.Combine(appBase, icon));
            if (File.Exists(fallback)) return fallback;
        }
        catch { }

        return icon;
    }

    private static string ExpandPath(string path)
        => Environment.ExpandEnvironmentVariables(path);

    private static bool IsHttpUrl(string s)
        => Uri.TryCreate(s, UriKind.Absolute, out var u) &&
           (u.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
            u.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase));

    private static bool IsLnk(string path)
        => path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase);

    private static string MakeAbsolute(string candidate, string? baseDir)
    {
        candidate = ExpandPath(candidate);
        if (Path.IsPathRooted(candidate)) return candidate;
        if (!string.IsNullOrEmpty(baseDir))
        {
            try { return Path.GetFullPath(Path.Combine(baseDir, candidate)); }
            catch { }
        }
        try { return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, candidate)); }
        catch { return candidate; }
    }

    // Tries to load as bitmap (png/jpg/ico). If that fails, extracts by index (EXE/DLL/ICO with index).
    private static ImageSource? LoadImageOrExtractIcon(string absPath, int index)
    {
        // Bitmap load path
        try
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.UriSource = new Uri(absPath, UriKind.Absolute);
            bi.EndInit();
            bi.Freeze();
            return bi;
        }
        catch
        {
            // Fall through
        }

        // Resource-indexed icon extraction (EXE/DLL/ICO with index)
        var byIndex = ExtractIconByIndex(absPath, index, large: true) ?? ExtractIconByIndex(absPath, index, large: false);
        if (byIndex is not null) return byIndex;

        // Last chance: associated icon
        return ExtractAssociatedIcon(absPath);
    }

    private static ImageSource? ExtractAssociatedIcon(string path)
    {
        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon is null) return null;
            var hIcon = icon.Handle;
            var bs = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bs.Freeze();
            return bs;
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource? ExtractIconByIndex(string file, int index, bool large)
    {
        try
        {
            var largeArr = large ? new IntPtr[1] : null;
            var smallArr = large ? null : new IntPtr[1];

            var count = ExtractIconEx(file, index, largeArr, smallArr, 1);
            if (count == 0) return null;

            var hIcon = large ? largeArr![0] : smallArr![0];
            if (hIcon == IntPtr.Zero) return null;

            try
            {
                var bs = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bs.Freeze();
                return bs;
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }
        catch
        {
            return null;
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, IntPtr[]? phiconLarge, IntPtr[]? phiconSmall, uint nIcons);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
