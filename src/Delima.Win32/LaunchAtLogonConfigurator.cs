using System.IO;
using Microsoft.Win32;

namespace Delima.Win32;

/// <summary>
/// Manages Windows launch-at-logon (kiosk auto-start) registration via the Windows Registry.
/// Supports both per-user (HKCU) and machine-wide (HKLM) Run keys per PRD §8.3.
/// </summary>
public static class LaunchAtLogonConfigurator
{
    public const string DefaultAppName = "DELIMa Smart Launcher";
    public const string StandardRunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// Checks whether launch-at-logon is configured for the specified app name.
    /// </summary>
    /// <param name="appName">Application entry name.</param>
    /// <param name="machineWide">If true, checks HKLM; otherwise checks HKCU.</param>
    /// <param name="customSubKey">Optional custom subkey path for testing.</param>
    /// <returns>True if configured; otherwise false.</returns>
    public static bool IsEnabled(
        string appName = DefaultAppName,
        bool machineWide = false,
        string? customSubKey = null)
    {
        string subKey = customSubKey ?? StandardRunSubKey;
        var hive = machineWide ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(subKey, writable: false);
            var val = key?.GetValue(appName) as string;
            return !string.IsNullOrWhiteSpace(val);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Retrieves the command line configured for launch-at-logon.
    /// </summary>
    public static string? GetCommandLine(
        string appName = DefaultAppName,
        bool machineWide = false,
        string? customSubKey = null)
    {
        string subKey = customSubKey ?? StandardRunSubKey;
        var hive = machineWide ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(subKey, writable: false);
            return key?.GetValue(appName) as string;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Enables launch-at-logon for the specified executable path.
    /// </summary>
    /// <param name="executablePath">Path to executable (e.g. Delima.Launcher.exe).</param>
    /// <param name="arguments">Optional launch arguments (e.g. --kiosk).</param>
    /// <param name="appName">Application entry name.</param>
    /// <param name="machineWide">If true, writes to HKLM (requires admin); otherwise writes to HKCU.</param>
    /// <param name="customSubKey">Optional custom subkey path for testing.</param>
    /// <returns>True if successfully written; otherwise false.</returns>
    public static bool Enable(
        string executablePath,
        string? arguments = null,
        string appName = DefaultAppName,
        bool machineWide = false,
        string? customSubKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        string subKey = customSubKey ?? StandardRunSubKey;
        var hive = machineWide ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;

        string formattedCommand = executablePath.Contains(' ')
            ? $"\"{executablePath}\""
            : executablePath;

        if (!string.IsNullOrWhiteSpace(arguments))
        {
            formattedCommand += $" {arguments.Trim()}";
        }

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.CreateSubKey(subKey, writable: true);
            if (key == null) return false;

            key.SetValue(appName, formattedCommand, RegistryValueKind.String);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Disables launch-at-logon by removing the registry value.
    /// </summary>
    /// <param name="appName">Application entry name.</param>
    /// <param name="machineWide">If true, modifies HKLM; otherwise modifies HKCU.</param>
    /// <param name="customSubKey">Optional custom subkey path for testing.</param>
    /// <returns>True if value was deleted or did not exist; false if deletion failed.</returns>
    public static bool Disable(
        string appName = DefaultAppName,
        bool machineWide = false,
        string? customSubKey = null)
    {
        string subKey = customSubKey ?? StandardRunSubKey;
        var hive = machineWide ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(subKey, writable: true);
            if (key == null) return true;

            if (key.GetValue(appName) != null)
            {
                key.DeleteValue(appName, throwOnMissingValue: false);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
