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
/// Backward-compatible facade over <see cref="BrowserPolicyConfigurator"/>.
/// 
/// IMPORTANT:
/// These policies write to HKLM\SOFTWARE\Policies\Google\Chrome and affect Google Chrome for
/// EVERY user on the machine, including teacher browsing. This is strictly opt-in per PRD §8.3.
/// </summary>
public static class ChromePolicyConfigurator
{
    public const string StandardChromePolicyKey = BrowserPolicyConfigurator.StandardChromePolicyKey;
    public const string WarningNoticeEn = BrowserPolicyConfigurator.WarningNoticeEn;
    public const string WarningNoticeBm = BrowserPolicyConfigurator.WarningNoticeBm;

    public static readonly string[] DefaultAllowlist = BrowserPolicyConfigurator.DefaultAllowlist;
    public static readonly string[] DefaultBlocklist = BrowserPolicyConfigurator.DefaultBlocklist;

    public static ChromePolicyStatus CheckPolicyStatus(
        string? customBaseKey = null,
        RegistryHive hive = RegistryHive.LocalMachine)
    {
        var raw = BrowserPolicyConfigurator.CheckPolicyStatus(BrowserKind.Chrome, customBaseKey, hive);
        return new ChromePolicyStatus
        {
            IsFullyApplied = raw.IsFullyApplied,
            PasswordManagerDisabled = raw.PasswordManagerDisabled,
            DevToolsDisabled = raw.DevToolsDisabled,
            IncognitoDisabled = raw.PrivateModeDisabled,
            BrowserSigninDisabled = raw.BrowserSigninDisabled,
            UrlAllowlistConfigured = raw.UrlAllowlistConfigured,
            UrlBlocklistConfigured = raw.UrlBlocklistConfigured,
            Summary = raw.Summary
        };
    }

    public static ChromePolicyResult ApplyPolicies(
        string? customBaseKey = null,
        RegistryHive hive = RegistryHive.LocalMachine,
        IEnumerable<string>? customAllowlist = null,
        IEnumerable<string>? customBlocklist = null)
    {
        var raw = BrowserPolicyConfigurator.ApplyPolicies(BrowserKind.Chrome, customBaseKey, hive, customAllowlist, customBlocklist);
        return new ChromePolicyResult
        {
            Success = raw.Success,
            ErrorMessage = raw.ErrorMessage,
            RequiresElevation = raw.RequiresElevation
        };
    }

    public static ChromePolicyResult RemovePolicies(
        string? customBaseKey = null,
        RegistryHive hive = RegistryHive.LocalMachine)
    {
        var raw = BrowserPolicyConfigurator.RemovePolicies(BrowserKind.Chrome, customBaseKey, hive);
        return new ChromePolicyResult
        {
            Success = raw.Success,
            ErrorMessage = raw.ErrorMessage,
            RequiresElevation = raw.RequiresElevation
        };
    }
}
