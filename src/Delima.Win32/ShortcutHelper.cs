using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Delima.Win32;

/// <summary>
/// Provides utility methods to create and manage Windows shell shortcuts (.lnk) via COM IShellLink.
/// </summary>
public static class ShortcutHelper
{
    public const string DefaultShortcutName = "DELIMa Smart Launcher";

    /// <summary>
    /// Creates a shortcut (.lnk) at the specified path.
    /// </summary>
    /// <param name="shortcutPath">Full path of the .lnk file to create.</param>
    /// <param name="targetPath">Target executable or file path.</param>
    /// <param name="arguments">Optional command line arguments.</param>
    /// <param name="workingDirectory">Optional working directory. Defaults to target directory.</param>
    /// <param name="description">Optional description/tooltip.</param>
    /// <param name="iconPath">Optional path to icon (.ico or .exe). Defaults to target path.</param>
    public static void CreateShortcut(
        string shortcutPath,
        string targetPath,
        string? arguments = null,
        string? workingDirectory = null,
        string? description = null,
        string? iconPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shortcutPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        string? dir = Path.GetDirectoryName(shortcutPath);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var link = (IShellLinkW)new ShellLink();
        link.SetPath(targetPath);

        if (!string.IsNullOrWhiteSpace(arguments))
        {
            link.SetArguments(arguments);
        }

        string workDir = workingDirectory ?? Path.GetDirectoryName(targetPath) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(workDir))
        {
            link.SetWorkingDirectory(workDir);
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            link.SetDescription(description);
        }

        string ico = iconPath ?? targetPath;
        if (!string.IsNullOrWhiteSpace(ico) && File.Exists(ico))
        {
            link.SetIconLocation(ico, 0);
        }

        var file = (IPersistFile)link;
        file.Save(shortcutPath, true);
    }

    /// <summary>
    /// Creates a shortcut on the Windows Desktop.
    /// </summary>
    /// <param name="targetPath">Target executable path (e.g. Delima.Launcher.exe).</param>
    /// <param name="shortcutName">Name of the shortcut without extension.</param>
    /// <param name="publicDesktop">If true, creates on Public Desktop (all users); otherwise user desktop.</param>
    /// <param name="arguments">Optional arguments.</param>
    /// <param name="iconPath">Optional custom icon path.</param>
    /// <returns>Path to the created shortcut file.</returns>
    public static string CreateDesktopShortcut(
        string targetPath,
        string shortcutName = DefaultShortcutName,
        bool publicDesktop = true,
        string? arguments = null,
        string? iconPath = null)
    {
        string desktopFolder = publicDesktop
            ? Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
            : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        if (string.IsNullOrWhiteSpace(desktopFolder) || !Directory.Exists(desktopFolder))
        {
            desktopFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }

        string shortcutPath = Path.Combine(desktopFolder, $"{shortcutName}.lnk");
        CreateShortcut(shortcutPath, targetPath, arguments: arguments, description: "Pelancar Pintar DELIMa untuk Murid", iconPath: iconPath);
        return shortcutPath;
    }

    /// <summary>
    /// Creates a shortcut in the Windows Startup folder (for launch at logon).
    /// </summary>
    /// <param name="targetPath">Target executable path.</param>
    /// <param name="shortcutName">Name of the shortcut.</param>
    /// <param name="publicStartup">If true, creates in Common Startup (all users); otherwise user startup.</param>
    /// <param name="arguments">Optional arguments (e.g. --kiosk).</param>
    /// <returns>Path to the created shortcut file.</returns>
    public static string CreateStartupShortcut(
        string targetPath,
        string shortcutName = DefaultShortcutName,
        bool publicStartup = true,
        string? arguments = null)
    {
        string startupFolder = publicStartup
            ? Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)
            : Environment.GetFolderPath(Environment.SpecialFolder.Startup);

        string shortcutPath = Path.Combine(startupFolder, $"{shortcutName}.lnk");
        CreateShortcut(shortcutPath, targetPath, arguments: arguments, description: "Autostart Pelancar Pintar DELIMa", iconPath: null);
        return shortcutPath;
    }

    /// <summary>
    /// Creates a shortcut in the Windows Start Menu Programs folder.
    /// </summary>
    public static string CreateStartMenuShortcut(
        string targetPath,
        string shortcutName = DefaultShortcutName,
        string? subFolder = "DELIMa Launcher",
        bool publicStartMenu = true)
    {
        string programsFolder = publicStartMenu
            ? Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)
            : Environment.GetFolderPath(Environment.SpecialFolder.Programs);

        string targetFolder = !string.IsNullOrWhiteSpace(subFolder)
            ? Path.Combine(programsFolder, subFolder)
            : programsFolder;

        string shortcutPath = Path.Combine(targetFolder, $"{shortcutName}.lnk");
        CreateShortcut(shortcutPath, targetPath, description: "Pelancar Pintar DELIMa", iconPath: null);
        return shortcutPath;
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
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
        void Save([MarshalAs(UnmanagedType.LPWStr)] string? pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder ppszFileName);
    }
}
