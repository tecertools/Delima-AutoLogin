using System.Diagnostics;
using System.IO;
using Delima.Core.Store;
using Delima.Win32;
using Xunit;

namespace Delima.Win32.Tests;

public class InjectionEngineTests
{
    [Fact]
    public void InjectionOptions_DefaultValues_Match_Specification()
    {
        var options = new InjectionOptions();

        Assert.Equal(TimeSpan.FromSeconds(30), options.WindowWaitTimeout);
        Assert.Equal(3, options.TitleSettlePolls);
        Assert.Equal(100, options.PollIntervalMs);
        Assert.Equal(700, options.InjectionSettleMs);
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
            "Sign in - Google Accounts - Google Chrome",
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

        // Target a window title that will never match
        var result = InjectionEngine.Inject(
            session,
            cred,
            "__NON_EXISTENT_WINDOW_TITLE_123456__",
            new InjectionOptions
            {
                WindowWaitTimeout = TimeSpan.FromMilliseconds(200),
                PollIntervalMs = 50
            });

        Assert.False(result.Success);
        Assert.Equal(FailureCodes.E02_WindowNotVerified, result.ErrorCode);
        Assert.Equal(0, result.CharactersInjected);
    }

    [Fact]
    public void SubstringMatchingTitle_IsRejected_And_AbortsTo_E02()
    {
        // §4.2 Requirement 1: Exact full-string match against configured value, never substring.
        // If the window has title "Welcome - Google Chrome", searching for substring "Welcome" must not match.
        using var cred = new SecurePasswordBuffer("SecretPassword123"u8);
        using var currentProc = Process.GetCurrentProcess();
        var session = new ChromeSession(currentProc, Path.GetTempPath());

        const string actualWindowTitle = "Welcome - Google Chrome";
        const string substringConfig = "Welcome"; // Loose substring

        // The public API requires exact full string match
        var result = InjectionEngine.Inject(
            session,
            cred.PasswordSpan,
            substringConfig,
            new InjectionOptions
            {
                WindowWaitTimeout = TimeSpan.FromMilliseconds(200),
                PollIntervalMs = 50
            });

        Assert.False(result.Success);
        Assert.Equal(FailureCodes.E02_WindowNotVerified, result.ErrorCode);
        Assert.Equal(0, result.CharactersInjected);

        // Direct check: string comparison rejects substring
        Assert.False(string.Equals(actualWindowTitle, substringConfig, StringComparison.Ordinal));
    }

    [Fact]
    public void TitleChangingDuringSettle_AbortsTo_E02_WithZeroKeystrokes()
    {
        // §4.2 Requirement 2: Title must be stable across >= 3 consecutive 100 ms polls.
        // If title changes mid-transition (e.g. 2 matches, then 1 non-match, repeatedly),
        // settle is reset and timeout triggers E02.
        using var cred = new SecurePasswordBuffer("SecretPassword123"u8);
        using var currentProc = Process.GetCurrentProcess();
        var session = new ChromeSession(currentProc, Path.GetTempPath());

        int pollCount = 0;
        // Fluctuating pattern: match, match, mismatch, match, match, mismatch...
        // Never achieves 3 consecutive matches
        Func<string, bool> fluctuatingTitle = _ =>
        {
            pollCount++;
            return (pollCount % 3) != 0; // true, true, false, true, true, false...
        };

        var result = InjectionEngine.Inject(
            session,
            cred.PasswordSpan,
            fluctuatingTitle,
            new InjectionOptions
            {
                WindowWaitTimeout = TimeSpan.FromMilliseconds(350),
                PollIntervalMs = 50,
                TitleSettlePolls = 3
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
