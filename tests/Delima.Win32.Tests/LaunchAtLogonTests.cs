using System.IO;
using Delima.Win32;
using Microsoft.Win32;
using Xunit;

namespace Delima.Win32.Tests;

public class LaunchAtLogonTests : IDisposable
{
    private readonly string _testSubKey = @"Software\DELIMa_UnitTest_RunKey_" + Guid.NewGuid().ToString("N");

    public void Dispose()
    {
        // Cleanup test subkey from HKCU
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
            baseKey.DeleteSubKeyTree(_testSubKey, throwOnMissingSubKey: false);
        }
        catch
        {
            // Best effort test cleanup
        }
    }

    [Fact]
    public void IsEnabled_ReturnsFalse_WhenEntryDoesNotExist()
    {
        bool enabled = LaunchAtLogonConfigurator.IsEnabled(
            appName: "TestApp",
            machineWide: false,
            customSubKey: _testSubKey);

        Assert.False(enabled);
    }

    [Fact]
    public void Enable_WritesRegistryValue_And_IsEnabled_ReturnsTrue()
    {
        string dummyExe = @"C:\Program Files\DELIMa Launcher\Delima.Launcher.exe";

        bool success = LaunchAtLogonConfigurator.Enable(
            executablePath: dummyExe,
            arguments: "--kiosk",
            appName: "TestDelimaLauncher",
            machineWide: false,
            customSubKey: _testSubKey);

        Assert.True(success);

        bool isEnabled = LaunchAtLogonConfigurator.IsEnabled(
            appName: "TestDelimaLauncher",
            machineWide: false,
            customSubKey: _testSubKey);

        Assert.True(isEnabled);

        string? cmd = LaunchAtLogonConfigurator.GetCommandLine(
            appName: "TestDelimaLauncher",
            machineWide: false,
            customSubKey: _testSubKey);

        Assert.NotNull(cmd);
        Assert.Contains(dummyExe, cmd);
        Assert.Contains("--kiosk", cmd);
    }

    [Fact]
    public void Disable_RemovesRegistryValue_And_IsEnabled_ReturnsFalse()
    {
        string dummyExe = @"C:\DELIMa\Delima.Launcher.exe";

        LaunchAtLogonConfigurator.Enable(
            executablePath: dummyExe,
            appName: "TestDelimaLauncher",
            machineWide: false,
            customSubKey: _testSubKey);

        Assert.True(LaunchAtLogonConfigurator.IsEnabled(
            appName: "TestDelimaLauncher",
            machineWide: false,
            customSubKey: _testSubKey));

        bool disabled = LaunchAtLogonConfigurator.Disable(
            appName: "TestDelimaLauncher",
            machineWide: false,
            customSubKey: _testSubKey);

        Assert.True(disabled);

        Assert.False(LaunchAtLogonConfigurator.IsEnabled(
            appName: "TestDelimaLauncher",
            machineWide: false,
            customSubKey: _testSubKey));

        Assert.Null(LaunchAtLogonConfigurator.GetCommandLine(
            appName: "TestDelimaLauncher",
            machineWide: false,
            customSubKey: _testSubKey));
    }
}
