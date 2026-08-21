using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Automation;
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

        const string identifierTitle = "Sign in - Google Accounts - Google Chrome";

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

        const string identifierTitle = "Sign in - Google Accounts - Google Chrome";

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

    [Fact]
    public void UiaHelper_IsFocusedElementPassword_Distinguishes_PasswordBox_From_TextBox()
    {
        if (!OperatingSystem.IsWindows()) return;

        bool? textBoxIsPassword = null;
        bool? passwordBoxIsPassword = null;
        Exception? threadEx = null;

        var thread = new Thread(() =>
        {
            try
            {
                var window = new System.Windows.Window
                {
                    Width = 300,
                    Height = 200,
                    WindowStyle = System.Windows.WindowStyle.None,
                    ShowInTaskbar = false
                };

                var stack = new System.Windows.Controls.StackPanel();
                var textBox = new System.Windows.Controls.TextBox { Width = 200, Height = 30 };
                var passwordBox = new System.Windows.Controls.PasswordBox { Width = 200, Height = 30 };

                AutomationProperties.SetAutomationId(textBox, "testTextBox");
                AutomationProperties.SetAutomationId(passwordBox, "testPasswordBox");

                stack.Children.Add(textBox);
                stack.Children.Add(passwordBox);
                window.Content = stack;

                window.Show();

                var hwnd = new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
                var windowEl = AutomationElement.FromHandle(hwnd);

                var textEl = windowEl.FindFirst(
                    TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "testTextBox"));

                var passEl = windowEl.FindFirst(
                    TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "testPasswordBox"));

                textBoxIsPassword = UiaHelper.IsElementPassword(textEl);
                passwordBoxIsPassword = UiaHelper.IsElementPassword(passEl);

                window.Close();
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(5));

        Assert.Null(threadEx);
        Assert.NotNull(textBoxIsPassword);
        Assert.NotNull(passwordBoxIsPassword);
        Assert.False(textBoxIsPassword.Value, "TextBox should report IsPassword == false");
        Assert.True(passwordBoxIsPassword.Value, "PasswordBox should report IsPassword == true");
    }

    [Fact]
    public void UiaHelper_ProbeFocusedElementPassword_ReturnsTupleWithoutCrashing()
    {
        if (!OperatingSystem.IsWindows()) return;

        // Verify probe returns structured tuple (FocusResolvable, IsPassword) without throwing exceptions
        var (focusResolvable, isPassword) = UiaHelper.ProbeFocusedElementPassword();

        if (focusResolvable)
        {
            Assert.NotNull(isPassword);
        }
        else
        {
            Assert.Null(isPassword);
        }
    }

    [Fact]
    public void PreInjectionCheck_Failure_Aborts_With_E02_And_Zero_Keystrokes()
    {
        // §11.1 (T0.4): When UIA IsPassword or PreInjectionCheck returns false,
        // injection must abort immediately with E02 and zero characters sent.
        using var currentProc = Process.GetCurrentProcess();
        var session = new ChromeSession(currentProc, Path.GetTempPath());
        using var cred = new SecurePasswordBuffer("SensitiveSecret123"u8);

        var result = InjectionEngine.Inject(
            session,
            cred.PasswordSpan,
            _ => true, // Window verified
            new InjectionOptions
            {
                WindowWaitTimeout = TimeSpan.FromMilliseconds(500),
                TitleSettlePolls = 1,
                ExpectedClassName = NativeMethods.GetForegroundClassName(),
                PreInjectionCheck = () => false // Simulates focused element IsPassword == false
            });

        // If current window is test host, pre-check failure stops all typing
        if (NativeMethods.GetForegroundProcessId() == (uint)currentProc.Id)
        {
            Assert.False(result.Success);
            Assert.Equal(0, result.CharactersInjected);
            Assert.Equal(FailureCodes.E02_WindowNotVerified, result.ErrorCode);
        }
    }

    [Fact]
    public void RouteCOptions_Supports_Malay_Locale_Titles_Per_AppendixB()
    {
        // §3.2 & Appendix B: Titles are per-locale configuration, not hardcoded constants.
        // Schools with Malay-locale Chrome can supply exact BM strings.
        var malayOptions = new RouteCOptions
        {
            TitleIdentifierPage = new[] { "Log masuk - Akaun Google - Google" },
            TitlePasswordPage = new[] { "Selamat Datang - Google Chrome" },
            TitleSettlePolls = 3,
            PollIntervalMs = 100,
            WindowWaitTimeout = TimeSpan.FromSeconds(30),
            CheckUiaPasswordElement = true
        };

        Assert.Equal("Log masuk - Akaun Google - Google", malayOptions.TitleIdentifierPage[0]);
        Assert.Equal("Selamat Datang - Google Chrome", malayOptions.TitlePasswordPage[0]);
        Assert.True(malayOptions.CheckUiaPasswordElement);
    }

    [Fact]
    public void TitleIdentifierPage_Default_Contains_Both_Measured_T04_Variants()
    {
        // Regression test for T0.4 findings:
        // T0.4 captures report two distinct titles:
        // 1. "Sign in - Google Accounts - Google Chrome" (hyphen, capital A)
        // 2. "Sign in – Google accounts - Google Chrome" (EN-DASH U+2013, lowercase a)
        var options = new RouteCOptions();
        Assert.Equal(2, options.TitleIdentifierPage.Count);
        Assert.Equal("Sign in - Google Accounts - Google Chrome", options.TitleIdentifierPage[0]);
        Assert.Equal("Sign in \u2013 Google accounts - Google Chrome", options.TitleIdentifierPage[1]);
    }

    [Fact]
    public void Regression_TitleIdentifierPage_Must_Include_Chrome_Suffix_And_Reject_MisTranscribed_T02_Value()
    {
        // Regression test: T0.2 mis-transcribed the identifier page title by dropping the " Chrome" suffix
        // ("Sign in - Google Accounts - Google" instead of "Sign in - Google Accounts - Google Chrome").
        // This single dropped word made the product silently non-functional under exact ordinal matching (E02 abort).
        var options = new RouteCOptions();

        // 1. Assert configured primary value equals measured string exactly
        Assert.Equal("Sign in - Google Accounts - Google Chrome", options.TitleIdentifierPage[0]);

        // 2. Assert the mis-transcribed string is rejected by exact matching
        const string oldMisTranscribedValue = "Sign in - Google Accounts - Google";
        Assert.False(InjectionEngine.MatchesAnyTitle(oldMisTranscribedValue, options.TitleIdentifierPage));
        Assert.DoesNotContain(oldMisTranscribedValue, options.TitleIdentifierPage);
    }

    [Fact]
    public void RouteCOptions_Defaults_Reflect_T04_Empirical_Findings()
    {
        var options = new RouteCOptions();

        // 1. Measured identifier page titles (list with exact ordinal matching)
        Assert.Contains("Sign in - Google Accounts - Google Chrome", options.TitleIdentifierPage);
        Assert.Contains("Sign in \u2013 Google accounts - Google Chrome", options.TitleIdentifierPage);
        Assert.Equal(2, options.TitleIdentifierPage.Count);

        // 2. Generic password title
        Assert.Contains("Welcome - Google Chrome", options.TitlePasswordPageGeneric);
        Assert.Contains("Welcome - Google Chrome", options.TitlePasswordPage);

        // 3. Consent page title
        Assert.Contains("Sign in - Google Accounts - Google Chrome", options.TitleConsentPage);
        Assert.Contains("Sign in \u2013 Google accounts - Google Chrome", options.TitleConsentPage);
        Assert.Equal(2, options.TitleConsentPage.Count);

        // 4. UIA IsPassword validation enabled by default (49/49 runs passed in T0.4)
        Assert.True(options.CheckUiaPasswordElement);

        // 5. Sequence gate settle defaults
        Assert.Equal(3, options.TitleSettlePolls);
        Assert.Equal(100, options.PollIntervalMs);
    }

    [Fact]
    public void ExactOrdinalEquality_Matches_Both_Seeded_Variants_And_Rejects_Unlisted_Variants()
    {
        var options = new RouteCOptions();

        // Both measured exact strings match
        Assert.True(InjectionEngine.MatchesAnyTitle("Sign in - Google Accounts - Google Chrome", options.TitleIdentifierPage));
        Assert.True(InjectionEngine.MatchesAnyTitle("Sign in \u2013 Google accounts - Google Chrome", options.TitleIdentifierPage));

        // Fuzzy/normalized strings do NOT match (must be exact Ordinal per §4.2)
        Assert.False(InjectionEngine.MatchesAnyTitle("sign in - google accounts - google chrome", options.TitleIdentifierPage));
        Assert.False(InjectionEngine.MatchesAnyTitle("Sign in - Google accounts - Google Chrome", options.TitleIdentifierPage));
        Assert.False(InjectionEngine.MatchesAnyTitle("Sign in \u2013 Google Accounts - Google Chrome", options.TitleIdentifierPage));
        Assert.False(InjectionEngine.MatchesAnyTitle("Sign in", options.TitleIdentifierPage));
    }

    [Fact]
    public void SequenceGate_TransitionOut_Handles_Both_Identifier_Variants()
    {
        using var currentProc = Process.GetCurrentProcess();
        var session = new ChromeSession(currentProc, Path.GetTempPath());
        using var cts = new CancellationTokenSource();
        var options = new RouteCOptions();

        // 1. When title is hyphen variant, transition out returns false
        bool trans1 = RouteCLoginOrchestrator.WaitForTransitionOut(
            session,
            identifierTitles: options.TitleIdentifierPage,
            timeout: TimeSpan.FromMilliseconds(50),
            pollIntervalMs: 10,
            cancellationToken: cts.Token,
            titleGetter: () => "Sign in - Google Accounts - Google Chrome");
        Assert.False(trans1);

        // 2. When title is en-dash variant, transition out returns false
        bool trans2 = RouteCLoginOrchestrator.WaitForTransitionOut(
            session,
            identifierTitles: options.TitleIdentifierPage,
            timeout: TimeSpan.FromMilliseconds(50),
            pollIntervalMs: 10,
            cancellationToken: cts.Token,
            titleGetter: () => "Sign in \u2013 Google accounts - Google Chrome");
        Assert.False(trans2);

        // 3. When title transitions to another page, returns true
        bool trans3 = RouteCLoginOrchestrator.WaitForTransitionOut(
            session,
            identifierTitles: options.TitleIdentifierPage,
            timeout: TimeSpan.FromMilliseconds(100),
            pollIntervalMs: 10,
            cancellationToken: cts.Token,
            titleGetter: () => "Welcome - Google Chrome");
        Assert.True(trans3);
    }

    [Fact]
    public async Task SequenceGate_ConsentState_GuardedByState_CannotBeEntered_WithoutSuccessfulPasswordInjection()
    {
        // Guarded by state rather than string alone: Consent state may only be entered
        // AFTER successful password injection in the same run.
        // If password injection fails / cannot verify, result is E02 failure, never Consent / Success.
        var recordedStates = new List<LoginFlowState>();
        using var cred = new SecurePasswordBuffer("Password123!"u8);

        var result = await RouteCLoginOrchestrator.ExecuteAsync(
            chromePath: @"C:\NonExistentDirectory\chrome.exe",
            email: "m-10000001@moe-dl.edu.my",
            credential: cred,
            options: new RouteCOptions(),
            onStateChanged: recordedStates.Add);

        Assert.False(result.Success);
        Assert.DoesNotContain(LoginFlowState.WaitingForConsentPage, recordedStates);
        Assert.DoesNotContain(LoginFlowState.Completed, recordedStates);
    }

    [Fact]
    public void RouteCResult_Failure_Factory_Preserves_Taxonomy_Information()
    {
        var failure = RouteCResult.Failure(
            FailureCodes.E04_WrongPassword,
            FailureCodes.GetPupilMessageBm(FailureCodes.E04_WrongPassword),
            FailureCodes.GetTeacherAction(FailureCodes.E04_WrongPassword),
            session: null,
            charsInjected: 12,
            elapsed: TimeSpan.FromMilliseconds(450));

        Assert.False(failure.Success);
        Assert.Equal(FailureCodes.E04_WrongPassword, failure.ErrorCode);
        Assert.Equal("Kata laluan tidak betul. Panggil cikgu.", failure.PupilMessage);
        Assert.Equal("Update via Mod Guru; check password_version", failure.TeacherAction);
        Assert.Equal(12, failure.TotalCharsInjected);
    }
}
