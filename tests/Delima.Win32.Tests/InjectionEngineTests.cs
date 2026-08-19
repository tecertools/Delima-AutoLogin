using System.Diagnostics;
using Delima.Core.Store;
using Delima.Win32;

namespace Delima.Win32.Tests;

public class InjectionEngineTests
{
    [Fact]
    public void InjectionOptions_DefaultValues_Match_Specification()
    {
        var options = new InjectionOptions();

        Assert.Equal(TimeSpan.FromSeconds(30), options.WindowWaitTimeout);
        Assert.Equal(400, options.InjectionSettleMs);
        Assert.Equal(0, options.PerCharDelayMs);
        Assert.False(options.SendEnter);
        Assert.Equal("Chrome_WidgetWin_1", options.ExpectedClassName);
    }

    [Fact]
    public void PreCancelledToken_Aborts_With_Zero_Keystrokes()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        using var cred = new SecurePasswordBuffer("SecretPassword123"u8);
        using var currentProc = Process.GetCurrentProcess();
        var session = new ChromeSession(currentProc, Path.GetTempPath());

        var result = InjectionEngine.Inject(
            session,
            cred,
            _ => true,
            new InjectionOptions { WindowWaitTimeout = TimeSpan.FromMilliseconds(500) },
            cts.Token);

        Assert.False(result.Success);
        Assert.Equal(FailureCodes.E03_InjectionAborted, result.ErrorCode);
        Assert.Equal(0, result.CharactersInjected);
    }

    [Fact]
    public void WindowVerificationTimeout_Returns_E02_With_Zero_Keystrokes()
    {
        using var cred = new SecurePasswordBuffer("SecretPassword123"u8);
        using var currentProc = Process.GetCurrentProcess();
        var session = new ChromeSession(currentProc, Path.GetTempPath());

        // Target a window title predicate that will never match
        var result = InjectionEngine.Inject(
            session,
            cred,
            title => title == "__NON_EXISTENT_WINDOW_TITLE_123456__",
            new InjectionOptions
            {
                WindowWaitTimeout = TimeSpan.FromMilliseconds(200),
                InjectionSettleMs = 0
            });

        Assert.False(result.Success);
        Assert.Equal(FailureCodes.E02_WindowNotVerified, result.ErrorCode);
        Assert.Equal(0, result.CharactersInjected);
    }

    [Fact]
    public void TopmostOverlay_Can_Be_Instantiated_And_Disposed()
    {
        using var overlay = new TopmostOverlay();
        Assert.True(overlay.IsActive);
    }

    [Fact]
    public void KioskGuard_EngageInjectionShield_Returns_Valid_Scope()
    {
        using var shield = KioskGuard.EngageInjectionShield();
        Assert.NotNull(shield);
    }
}
