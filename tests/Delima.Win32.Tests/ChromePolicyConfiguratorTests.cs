using Delima.Win32;
using Microsoft.Win32;
using Xunit;

namespace Delima.Win32.Tests;

public class ChromePolicyConfiguratorTests : IDisposable
{
    private readonly string _testBaseKey = @"Software\DELIMa_UnitTest_ChromePolicy_" + Guid.NewGuid().ToString("N");

    public void Dispose()
    {
        // Cleanup test subkeys from HKCU
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
            baseKey.DeleteSubKeyTree(_testBaseKey, throwOnMissingSubKey: false);
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public void WarningNotices_AreExplicitAndInformative()
    {
        // Must mention HKLM and all users / teachers per PRD §8.3
        Assert.Contains("HKLM", ChromePolicyConfigurator.WarningNoticeEn);
        Assert.Contains("EVERY user", ChromePolicyConfigurator.WarningNoticeEn);
        Assert.Contains("opt-in", ChromePolicyConfigurator.WarningNoticeEn);

        Assert.Contains("HKLM", ChromePolicyConfigurator.WarningNoticeBm);
        Assert.Contains("SEMUA pengguna", ChromePolicyConfigurator.WarningNoticeBm);
        Assert.Contains("opt-in", ChromePolicyConfigurator.WarningNoticeBm);
    }

    [Fact]
    public void CheckPolicyStatus_ReturnsNotApplied_WhenKeyDoesNotExist()
    {
        var status = ChromePolicyConfigurator.CheckPolicyStatus(
            customBaseKey: _testBaseKey,
            hive: RegistryHive.CurrentUser);

        Assert.False(status.IsFullyApplied);
        Assert.False(status.PasswordManagerDisabled);
        Assert.False(status.DevToolsDisabled);
        Assert.False(status.IncognitoDisabled);
        Assert.False(status.BrowserSigninDisabled);
    }

    [Fact]
    public void ApplyPolicies_WritesAllSixHardeningPolicies_Correctly()
    {
        var result = ChromePolicyConfigurator.ApplyPolicies(
            customBaseKey: _testBaseKey,
            hive: RegistryHive.CurrentUser);

        Assert.True(result.Success, result.ErrorMessage);

        // Verify registry entries
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
        using var key = baseKey.OpenSubKey(_testBaseKey);
        Assert.NotNull(key);

        Assert.Equal(0, key.GetValue("PasswordManagerEnabled"));
        Assert.Equal(2, key.GetValue("DeveloperToolsAvailability"));
        Assert.Equal(1, key.GetValue("IncognitoModeAvailability"));
        Assert.Equal(0, key.GetValue("BrowserSignin"));

        // Check Allowlist
        using var allowKey = key.OpenSubKey("URLAllowlist");
        Assert.NotNull(allowKey);
        Assert.Equal("accounts.google.com", allowKey.GetValue("1"));
        Assert.Equal("*.delima.edu.my", allowKey.GetValue("2"));
        Assert.Equal("classroom.google.com", allowKey.GetValue("3"));

        // Check Blocklist
        using var blockKey = key.OpenSubKey("URLBlocklist");
        Assert.NotNull(blockKey);
        Assert.Equal("*", blockKey.GetValue("1"));

        // Verify CheckPolicyStatus reports fully applied
        var status = ChromePolicyConfigurator.CheckPolicyStatus(
            customBaseKey: _testBaseKey,
            hive: RegistryHive.CurrentUser);

        Assert.True(status.IsFullyApplied);
        Assert.True(status.PasswordManagerDisabled);
        Assert.True(status.DevToolsDisabled);
        Assert.True(status.IncognitoDisabled);
        Assert.True(status.BrowserSigninDisabled);
        Assert.True(status.UrlAllowlistConfigured);
        Assert.True(status.UrlBlocklistConfigured);
    }

    [Fact]
    public void RemovePolicies_DeletesValuesAndSubkeys_Correctly()
    {
        ChromePolicyConfigurator.ApplyPolicies(
            customBaseKey: _testBaseKey,
            hive: RegistryHive.CurrentUser);

        var statusBefore = ChromePolicyConfigurator.CheckPolicyStatus(
            customBaseKey: _testBaseKey,
            hive: RegistryHive.CurrentUser);
        Assert.True(statusBefore.IsFullyApplied);

        var removeResult = ChromePolicyConfigurator.RemovePolicies(
            customBaseKey: _testBaseKey,
            hive: RegistryHive.CurrentUser);
        Assert.True(removeResult.Success);

        var statusAfter = ChromePolicyConfigurator.CheckPolicyStatus(
            customBaseKey: _testBaseKey,
            hive: RegistryHive.CurrentUser);
        Assert.False(statusAfter.IsFullyApplied);
        Assert.False(statusAfter.PasswordManagerDisabled);
        Assert.False(statusAfter.UrlAllowlistConfigured);
    }
}
