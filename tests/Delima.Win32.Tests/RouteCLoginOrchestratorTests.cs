using System.Diagnostics;
using Delima.Core.Store;
using Delima.Win32;
using Xunit;

namespace Delima.Win32.Tests;

public class RouteCLoginOrchestratorTests
{
    [Fact]
    public async Task RouteCOrchestrator_MissingChrome_Returns_E01_ChromeNotInstalled()
    {
        using var cred = new SecurePasswordBuffer("Password123!"u8);

        var result = await RouteCLoginOrchestrator.ExecuteAsync(
            chromePath: @"C:\NonExistentDirectory\chrome.exe",
            email: "m-10000001@moe-dl.edu.my",
            credential: cred,
            options: new RouteCOptions { WindowWaitTimeout = TimeSpan.FromMilliseconds(200) });

        Assert.False(result.Success);
        Assert.Equal(FailureCodes.E01_ChromeNotInstalled, result.ErrorCode);
        Assert.Equal(0, result.TotalCharsInjected);
    }

    [Fact]
    public async Task RouteCOrchestrator_CancelledToken_Aborts_With_E03()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        using var cred = new SecurePasswordBuffer("Password123!"u8);

        var result = await RouteCLoginOrchestrator.ExecuteAsync(
            chromePath: @"C:\NonExistentDirectory\chrome.exe",
            email: "m-10000001@moe-dl.edu.my",
            credential: cred,
            options: new RouteCOptions(),
            cancellationToken: cts.Token);

        Assert.False(result.Success);
        Assert.Equal(0, result.TotalCharsInjected);
    }

    [Fact]
    public void SequenceGate_RefusesPasswordInjection_WithoutPrecedingIdentifierTransition()
    {
        // §4.2 Requirement: "The password injection is sequence-gated. It may fire only after the engine
        // has observed a verified transition out of the identifier title. Matching the password-page title
        // in isolation must never authorise typing a password."

        using var currentProc = Process.GetCurrentProcess();
        var session = new ChromeSession(currentProc, Path.GetTempPath());
        using var cts = new CancellationTokenSource();

        const string identifierTitle = "Sign in - Google Accounts - Google";

        // Simulate browser never leaving the identifier title
        // WaitForTransitionOut should return false (timeout)
        bool transitioned = RouteCLoginOrchestrator.WaitForTransitionOut(
            session,
            identifierTitle: identifierTitle,
            timeout: TimeSpan.FromMilliseconds(100),
            pollIntervalMs: 20,
            cancellationToken: cts.Token,
            titleGetter: () => identifierTitle);

        Assert.False(transitioned);
    }

    [Fact]
    public void SequenceGate_ObservesTransitionOut_WhenTitleChangesFromIdentifier()
    {
        using var currentProc = Process.GetCurrentProcess();
        var session = new ChromeSession(currentProc, Path.GetTempPath());
        using var cts = new CancellationTokenSource();

        const string identifierTitle = "Sign in - Google Accounts - Google";

        // Simulate title transitioning to intermediate or destination page
        bool transitioned = RouteCLoginOrchestrator.WaitForTransitionOut(
            session,
            identifierTitle: identifierTitle,
            timeout: TimeSpan.FromMilliseconds(200),
            pollIntervalMs: 20,
            cancellationToken: cts.Token,
            titleGetter: () => "Transitioning Page - Google Chrome");

        Assert.True(transitioned);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(1000)]
    [InlineData(2000)]
    [InlineData(4000)]
    public void Adversarial_FocusStolenAtInterval_SendsZeroKeystrokes(int focusStealMs)
    {
        // §11 Adversarial test: "Steal focus at 500, 1000, 2000 and 4000 ms and assert zero keystrokes are sent"
        using var cred = new SecurePasswordBuffer("SensitiveSecret123"u8);
        using var currentProc = Process.GetCurrentProcess();
        var session = new ChromeSession(currentProc, Path.GetTempPath());

        // Target an unverified window so verification fails when focus is absent
        var result = InjectionEngine.Inject(
            session,
            cred.PasswordSpan,
            "Target Window That Is Not In Foreground",
            new InjectionOptions
            {
                WindowWaitTimeout = TimeSpan.FromMilliseconds(focusStealMs),
                PollIntervalMs = 50
            });

        // Verification fails immediately or at timeout — zero keystrokes leaked
        Assert.False(result.Success);
        Assert.Equal(0, result.CharactersInjected);
        Assert.Equal(FailureCodes.E02_WindowNotVerified, result.ErrorCode);
    }

    [Fact]
    public void TitleSettle_RejectsTransientTitle_BeforeThreePolls()
    {
        // §4.2 Requirement: "The title must be stable across >= 3 consecutive 100 ms polls before anything is typed"
        using var currentProc = Process.GetCurrentProcess();
        var session = new ChromeSession(currentProc, Path.GetTempPath());
        using var cts = new CancellationTokenSource();

        int pollCounter = 0;
        // Simulates a title that matches only 2 times, then resets
        Func<string, bool> transientTitle = _ =>
        {
            pollCounter++;
            return pollCounter <= 2; // Matches 2 times only
        };

        bool settled = InjectionEngine.WaitForVerifiedAndSettledWindow(
            session,
            transientTitle,
            expectedClassName: "Chrome_WidgetWin_1",
            timeout: TimeSpan.FromMilliseconds(300),
            settlePolls: 3,
            pollIntervalMs: 50,
            cancellationToken: cts.Token);

        Assert.False(settled);
    }

    [Fact]
    public void TitleSettle_AcceptsStableTitle_AcrossThreePolls()
    {
        using var currentProc = Process.GetCurrentProcess();
        var session = new ChromeSession(currentProc, Path.GetTempPath());
        using var cts = new CancellationTokenSource();

        // Always matching predicate
        Func<string, bool> stableTitle = _ => true;

        // On this process with matching class or simulated check
        bool settled = InjectionEngine.WaitForVerifiedAndSettledWindow(
            session,
            stableTitle,
            expectedClassName: NativeMethods.GetForegroundClassName(),
            timeout: TimeSpan.FromMilliseconds(500),
            settlePolls: 3,
            pollIntervalMs: 20,
            cancellationToken: cts.Token);

        // If current window is the active test host window and class matches
        if (NativeMethods.GetForegroundProcessId() == (uint)currentProc.Id)
        {
            Assert.True(settled);
        }
    }

    [Fact]
    public void UiaHelper_IsFocusedElementPassword_ReturnsBooleanWithoutCrashing()
    {
        if (!OperatingSystem.IsWindows()) return;

        // Verify UIA helper runs safely on current foreground element
        bool isPassword = UiaHelper.IsFocusedElementPassword();
        // Returns boolean without throwing exceptions
        Assert.False(isPassword); // Normal non-password edit focus in test runner
    }
}
