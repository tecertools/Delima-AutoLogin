using Delima.Win32;
using Microsoft.Win32;
using Xunit;

namespace Delima.Win32.Tests;

public class BrowserPolicyConfiguratorTests : IDisposable
{
    private readonly string _edgeTestBaseKey = @"Software\DELIMa_UnitTest_EdgePolicy_" + Guid.NewGuid().ToString("N");
    private readonly string _chromeTestBaseKey = @"Software\DELIMa_UnitTest_ChromePolicy_" + Guid.NewGuid().ToString("N");

    public void Dispose()
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
            baseKey.DeleteSubKeyTree(_edgeTestBaseKey, throwOnMissingSubKey: false);
            baseKey.DeleteSubKeyTree(_chromeTestBaseKey, throwOnMissingSubKey: false);
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public void EdgePolicy_Uses_InPrivateModeAvailability_InsteadOf_IncognitoModeAvailability()
    {
        // §4.4.1 & Requirement 3: Edge uses InPrivateModeAvailability where Chrome uses IncognitoModeAvailability
        var result = BrowserPolicyConfigurator.ApplyPolicies(
            kind: BrowserKind.Edge,
            customBaseKey: _edgeTestBaseKey,
            hive: RegistryHive.CurrentUser);

        Assert.True(result.Success, result.ErrorMessage);

        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
        using var key = baseKey.OpenSubKey(_edgeTestBaseKey);
        Assert.NotNull(key);

        Assert.Equal(0, key.GetValue("PasswordManagerEnabled"));
        Assert.Equal(2, key.GetValue("DeveloperToolsAvailability"));
        Assert.Equal(1, key.GetValue("InPrivateModeAvailability")); // Edge value name
        Assert.Null(key.GetValue("IncognitoModeAvailability"));     // Chrome value name absent
        Assert.Equal(0, key.GetValue("BrowserSignin"));

        var status = BrowserPolicyConfigurator.CheckPolicyStatus(
            kind: BrowserKind.Edge,
            customBaseKey: _edgeTestBaseKey,
            hive: RegistryHive.CurrentUser);

        Assert.True(status.IsFullyApplied);
        Assert.True(status.PrivateModeDisabled);
    }

    [Fact]
    public void ChromePolicy_Uses_IncognitoModeAvailability_InsteadOf_InPrivateModeAvailability()
    {
        var result = BrowserPolicyConfigurator.ApplyPolicies(
            kind: BrowserKind.Chrome,
            customBaseKey: _chromeTestBaseKey,
            hive: RegistryHive.CurrentUser);

        Assert.True(result.Success, result.ErrorMessage);

        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
        using var key = baseKey.OpenSubKey(_chromeTestBaseKey);
        Assert.NotNull(key);

        Assert.Equal(0, key.GetValue("PasswordManagerEnabled"));
        Assert.Equal(2, key.GetValue("DeveloperToolsAvailability"));
        Assert.Equal(1, key.GetValue("IncognitoModeAvailability")); // Chrome value name
        Assert.Null(key.GetValue("InPrivateModeAvailability"));     // Edge value name absent
        Assert.Equal(0, key.GetValue("BrowserSignin"));

        var status = BrowserPolicyConfigurator.CheckPolicyStatus(
            kind: BrowserKind.Chrome,
            customBaseKey: _chromeTestBaseKey,
            hive: RegistryHive.CurrentUser);

        Assert.True(status.IsFullyApplied);
        Assert.True(status.PrivateModeDisabled);
    }

    [Fact]
    public void EdgePolicy_RemovePolicies_CleansUpAllValues()
    {
        BrowserPolicyConfigurator.ApplyPolicies(
            kind: BrowserKind.Edge,
            customBaseKey: _edgeTestBaseKey,
            hive: RegistryHive.CurrentUser);

        var statusBefore = BrowserPolicyConfigurator.CheckPolicyStatus(
            kind: BrowserKind.Edge,
            customBaseKey: _edgeTestBaseKey,
            hive: RegistryHive.CurrentUser);
        Assert.True(statusBefore.IsFullyApplied);

        var removeResult = BrowserPolicyConfigurator.RemovePolicies(
            kind: BrowserKind.Edge,
            customBaseKey: _edgeTestBaseKey,
            hive: RegistryHive.CurrentUser);
        Assert.True(removeResult.Success);

        var statusAfter = BrowserPolicyConfigurator.CheckPolicyStatus(
            kind: BrowserKind.Edge,
            customBaseKey: _edgeTestBaseKey,
            hive: RegistryHive.CurrentUser);
        Assert.False(statusAfter.IsFullyApplied);
        Assert.False(statusAfter.PrivateModeDisabled);
    }

    [Fact]
    public void WarningNotices_Contain_WholePC_OptIn_Warning()
    {
        Assert.Contains("HKLM", BrowserPolicyConfigurator.WarningNoticeEn);
        Assert.Contains("EVERY user", BrowserPolicyConfigurator.WarningNoticeEn);
        Assert.Contains("opt-in", BrowserPolicyConfigurator.WarningNoticeEn);

        Assert.Contains("HKLM", BrowserPolicyConfigurator.WarningNoticeBm);
        Assert.Contains("SEMUA pengguna", BrowserPolicyConfigurator.WarningNoticeBm);
        Assert.Contains("opt-in", BrowserPolicyConfigurator.WarningNoticeBm);
    }
}
