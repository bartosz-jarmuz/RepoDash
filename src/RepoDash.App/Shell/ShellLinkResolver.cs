namespace RepoDash.App.Shell;

using System;
using System.Runtime.InteropServices;
using System.Text;

internal static class ShellLinkResolver
{
    public static bool TryResolve(string lnkPath, out string? targetPath, out string? iconPath, out int iconIndex)
    {
        targetPath = null;
        iconPath = null;
        iconIndex = 0;

        if (string.IsNullOrWhiteSpace(lnkPath)) return false;

        IShellLinkW? link = null;
        IPersistFile? file = null;
        try
        {
            link = (IShellLinkW)new CShellLink();
            file = (IPersistFile)link;
            file.Load(lnkPath, 0);

            var path = new StringBuilder(260);
            // pfd is unused => pass IntPtr.Zero; flags = 0 for resolved path
            link.GetPath(path, path.Capacity, IntPtr.Zero, 0);
            targetPath = path.Length > 0 ? path.ToString() : null;

            var icon = new StringBuilder(260);
            link.GetIconLocation(icon, icon.Capacity, out iconIndex);
            iconPath = icon.Length > 0 ? icon.ToString() : null;

            return targetPath is not null || iconPath is not null;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (file is not null) Marshal.ReleaseComObject(file);
            if (link is not null) Marshal.ReleaseComObject(link);
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class CShellLink { }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        // Order matters: methods must match the vtable layout
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hWnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        void IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }
}
