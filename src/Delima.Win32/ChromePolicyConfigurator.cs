using System.Security;
using Microsoft.Win32;

namespace Delima.Win32;

/// <summary>
/// Status of Chrome enterprise policies on this machine.
/// </summary>
public sealed class ChromePolicyStatus
{
    public bool IsFullyApplied { get; set; }
    public bool PasswordManagerDisabled { get; set; }
    public bool DevToolsDisabled { get; set; }
    public bool IncognitoDisabled { get; set; }
    public bool BrowserSigninDisabled { get; set; }
    public bool UrlAllowlistConfigured { get; set; }
    public bool UrlBlocklistConfigured { get; set; }
    public string Summary { get; set; } = "";
}

/// <summary>
/// Result of applying or removing Chrome enterprise policies.
/// </summary>
public sealed class ChromePolicyResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public bool RequiresElevation { get; set; }
}

/// <summary>
/// Manages Google Chrome Enterprise Policies via the Windows Registry per Technical Architecture §9.
/// 
/// IMPORTANT:
/// These policies write to HKLM\SOFTWARE\Policies\Google\Chrome and affect Google Chrome for
/// EVERY user on the machine, including teacher browsing. This is strictly opt-in per PRD §8.3.
/// </summary>
public static class ChromePolicyConfigurator
{
    public const string StandardChromePolicyKey = @"SOFTWARE\Policies\Google\Chrome";

    public const string WarningNoticeEn =
        "The Chrome enterprise policy writes to HKLM and changes Google Chrome for EVERY user on this machine, " +
        "including any teacher's personal browsing. It is strictly opt-in and should only be enabled on dedicated lab kiosk machines.";

    public const string WarningNoticeBm =
        "Dasar Chrome Perusahaan menulis ke HKLM dan menukar tetapan Google Chrome untuk SEMUA pengguna pada komputer ini, " +
        "termasuk pelayaran peribadi guru. Pilihan ini adalah pilihan sendiri (opt-in) dan hanya perlu diaktifkan pada PC makmal khusus.";

    public static readonly string[] DefaultAllowlist =
    [
        "accounts.google.com",
        "*.delima.edu.my",
        "classroom.google.com"
    ];

    public static readonly string[] DefaultBlocklist =
    [
        "*"
    ];

    /// <summary>
    /// Checks the current state of Chrome enterprise policies in the specified registry hive.
    /// </summary>
    public static ChromePolicyStatus CheckPolicyStatus(
        string? customBaseKey = null,
        RegistryHive hive = RegistryHive.LocalMachine)
    {
        string baseKeyPath = customBaseKey ?? StandardChromePolicyKey;
        var status = new ChromePolicyStatus();

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(baseKeyPath, writable: false);

            if (key == null)
            {
                status.Summary = "Tiada dasar Chrome dikonfigurasikan.";
                return status;
            }

            // 1. PasswordManagerEnabled == 0
            if (key.GetValue("PasswordManagerEnabled") is int pwm && pwm == 0)
            {
                status.PasswordManagerDisabled = true;
            }

            // 2. DeveloperToolsAvailability == 2
            if (key.GetValue("DeveloperToolsAvailability") is int dt && dt == 2)
            {
                status.DevToolsDisabled = true;
            }

            // 3. IncognitoModeAvailability == 1
            if (key.GetValue("IncognitoModeAvailability") is int inc && inc == 1)
            {
                status.IncognitoDisabled = true;
            }

            // 4. BrowserSignin == 0
            if (key.GetValue("BrowserSignin") is int bs && bs == 0)
            {
                status.BrowserSigninDisabled = true;
            }

            // 5. URLAllowlist
            using (var allowKey = key.OpenSubKey("URLAllowlist"))
            {
                if (allowKey != null && allowKey.ValueCount >= DefaultAllowlist.Length)
                {
                    status.UrlAllowlistConfigured = true;
                }
            }

            // 6. URLBlocklist
            using (var blockKey = key.OpenSubKey("URLBlocklist"))
            {
                if (blockKey != null && blockKey.ValueCount >= 1)
                {
                    status.UrlBlocklistConfigured = true;
                }
            }

            status.IsFullyApplied = status.PasswordManagerDisabled &&
                                    status.DevToolsDisabled &&
                                    status.IncognitoDisabled &&
                                    status.BrowserSigninDisabled &&
                                    status.UrlAllowlistConfigured &&
                                    status.UrlBlocklistConfigured;

            status.Summary = status.IsFullyApplied
                ? "Dasar Chrome Perusahaan aktif dan lengkap."
                : "Dasar Chrome Perusahaan separa aktif.";

            return status;
        }
        catch (Exception ex)
        {
            status.Summary = $"Ralat memeriksa dasar: {ex.Message}";
            return status;
        }
    }

    /// <summary>
    /// Applies the 6 hardening policies to Google Chrome.
    /// Requires Administrator privileges when targeting HKLM.
    /// </summary>
    public static ChromePolicyResult ApplyPolicies(
        string? customBaseKey = null,
        RegistryHive hive = RegistryHive.LocalMachine,
        IEnumerable<string>? customAllowlist = null,
        IEnumerable<string>? customBlocklist = null)
    {
        string baseKeyPath = customBaseKey ?? StandardChromePolicyKey;
        var allowlist = (customAllowlist ?? DefaultAllowlist).ToArray();
        var blocklist = (customBlocklist ?? DefaultBlocklist).ToArray();

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.CreateSubKey(baseKeyPath, writable: true);

            if (key == null)
            {
                return new ChromePolicyResult
                {
                    Success = false,
                    ErrorMessage = $"Gagal membuka atau mencipta kunci pendaftaran '{baseKeyPath}'.",
                    RequiresElevation = hive == RegistryHive.LocalMachine
                };
            }

            // §9 Table: Hardening policies
            key.SetValue("PasswordManagerEnabled", 0, RegistryValueKind.DWord);
            key.SetValue("DeveloperToolsAvailability", 2, RegistryValueKind.DWord);
            key.SetValue("IncognitoModeAvailability", 1, RegistryValueKind.DWord);
            key.SetValue("BrowserSignin", 0, RegistryValueKind.DWord);

            // URLAllowlist subkey
            using (var allowKey = key.CreateSubKey("URLAllowlist", writable: true))
            {
                if (allowKey != null)
                {
                    // Clear existing numbered entries
                    foreach (var v in allowKey.GetValueNames())
                    {
                        allowKey.DeleteValue(v, throwOnMissingValue: false);
                    }

                    for (int i = 0; i < allowlist.Length; i++)
                    {
                        allowKey.SetValue((i + 1).ToString(), allowlist[i], RegistryValueKind.String);
                    }
                }
            }

            // URLBlocklist subkey
            using (var blockKey = key.CreateSubKey("URLBlocklist", writable: true))
            {
                if (blockKey != null)
                {
                    foreach (var v in blockKey.GetValueNames())
                    {
                        blockKey.DeleteValue(v, throwOnMissingValue: false);
                    }

                    for (int i = 0; i < blocklist.Length; i++)
                    {
                        blockKey.SetValue((i + 1).ToString(), blocklist[i], RegistryValueKind.String);
                    }
                }
            }

            return new ChromePolicyResult { Success = true };
        }
        catch (SecurityException ex)
        {
            return new ChromePolicyResult
            {
                Success = false,
                ErrorMessage = $"Kebenaran Pentadbir (Administrator) diperlukan untuk menulis ke HKLM: {ex.Message}",
                RequiresElevation = true
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            return new ChromePolicyResult
            {
                Success = false,
                ErrorMessage = $"Akses ditolak semasa menulis dasar Chrome: {ex.Message}",
                RequiresElevation = true
            };
        }
        catch (Exception ex)
        {
            return new ChromePolicyResult
            {
                Success = false,
                ErrorMessage = $"Ralat memasang dasar Chrome: {ex.Message}",
                RequiresElevation = false
            };
        }
    }

    /// <summary>
    /// Removes the Chrome enterprise hardening policies and subkeys.
    /// </summary>
    public static ChromePolicyResult RemovePolicies(
        string? customBaseKey = null,
        RegistryHive hive = RegistryHive.LocalMachine)
    {
        string baseKeyPath = customBaseKey ?? StandardChromePolicyKey;

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(baseKeyPath, writable: true);

            if (key == null)
            {
                return new ChromePolicyResult { Success = true }; // Already clean
            }

            key.DeleteValue("PasswordManagerEnabled", throwOnMissingValue: false);
            key.DeleteValue("DeveloperToolsAvailability", throwOnMissingValue: false);
            key.DeleteValue("IncognitoModeAvailability", throwOnMissingValue: false);
            key.DeleteValue("BrowserSignin", throwOnMissingValue: false);

            key.DeleteSubKeyTree("URLAllowlist", throwOnMissingSubKey: false);
            key.DeleteSubKeyTree("URLBlocklist", throwOnMissingSubKey: false);

            return new ChromePolicyResult { Success = true };
        }
        catch (SecurityException ex)
        {
            return new ChromePolicyResult
            {
                Success = false,
                ErrorMessage = $"Kebenaran Pentadbir diperlukan untuk membuang dasar Chrome: {ex.Message}",
                RequiresElevation = true
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            return new ChromePolicyResult
            {
                Success = false,
                ErrorMessage = $"Akses ditolak semasa membuang dasar Chrome: {ex.Message}",
                RequiresElevation = true
            };
        }
        catch (Exception ex)
        {
            return new ChromePolicyResult
            {
                Success = false,
                ErrorMessage = $"Ralat membuang dasar Chrome: {ex.Message}"
            };
        }
    }
}
