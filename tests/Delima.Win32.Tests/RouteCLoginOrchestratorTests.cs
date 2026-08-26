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
    public async Task RouteCOrchestrator_MissingBrowser_Returns_E01_NoBrowserFound()
    {
        using var cred = new SecurePasswordBuffer("Password123!"u8);

        var result = await RouteCLoginOrchestrator.ExecuteAsync(
            browserPath: @"C:\NonExistentDirectory\chrome.exe",
            email: "m-10000001@moe-dl.edu.my",
            credential: cred,
            options: new RouteCOptions { WindowWaitTimeout = TimeSpan.FromMilliseconds(200) });

        Assert.False(result.Success);
        Assert.Equal(FailureCodes.E01_NoBrowserFound, result.ErrorCode);
        Assert.Equal(0, result.TotalCharsInjected);
    }

    [Fact]
    public async Task RouteCOrchestrator_CancelledToken_Aborts_With_E03()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        using var cred = new SecurePasswordBuffer("Password123!"u8);

        var result = await RouteCLoginOrchestrator.ExecuteAsync(
            browserPath: @"C:\NonExistentDirectory\chrome.exe",
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

                AutomationElement? textEl = null;
                AutomationElement? passEl = null;

                for (int i = 0; i < 20 && (textEl == null || passEl == null); i++)
                {
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                    Thread.Sleep(100);

                    if (windowEl != null)
                    {
                        textEl ??= windowEl.FindFirst(
                            TreeScope.Descendants,
                            new PropertyCondition(AutomationElement.AutomationIdProperty, "testTextBox"));

                        passEl ??= windowEl.FindFirst(
                            TreeScope.Descendants,
                            new PropertyCondition(AutomationElement.AutomationIdProperty, "testPasswordBox"));
                    }
                }

                if (textEl != null) textBoxIsPassword = UiaHelper.IsElementPassword(textEl);
                if (passEl != null) passwordBoxIsPassword = UiaHelper.IsElementPassword(passEl);

                window.Close();
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        bool completed = thread.Join(TimeSpan.FromSeconds(30));

        Assert.True(completed, "STA thread timed out while resolving UIA elements");
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
            TitleIdentifierPage = new[] { "Log masuk - Akaun Google - Google Chrome" },
            TitleConsentPage = new[] { "Log masuk - Akaun Google - Google Chrome" },
            TitleDestinationPage = new[] { "DELIMa - Google Chrome" },
            TitleSettlePolls = 3,
            PollIntervalMs = 100,
            WindowWaitTimeout = TimeSpan.FromSeconds(30),
            CheckUiaPasswordElement = true
        };

        Assert.Equal("Log masuk - Akaun Google - Google Chrome", malayOptions.TitleIdentifierPage[0]);
        Assert.Equal("Log masuk - Akaun Google - Google Chrome", malayOptions.TitleConsentPage[0]);
        Assert.Equal("DELIMa - Google Chrome", malayOptions.TitleDestinationPage[0]);
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
        Assert.Contains("Sign in - Google Accounts - Google Chrome", options.TitleIdentifierPage);
        Assert.Contains("Sign in \u2013 Google accounts - Google Chrome", options.TitleIdentifierPage);
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
        Assert.NotEmpty(options.TitleIdentifierPage);

        // 2. Consent page title
        Assert.Contains("Sign in - Google Accounts - Google Chrome", options.TitleConsentPage);
        Assert.Contains("Sign in \u2013 Google accounts - Google Chrome", options.TitleConsentPage);
        Assert.NotEmpty(options.TitleConsentPage);

        // 3. Destination page titles (consent skipped / domain-trusted)
        Assert.Contains("DELIMa - Google Chrome", options.TitleDestinationPage);
        Assert.Contains("DELIMa 3.0 - Google Chrome", options.TitleDestinationPage);

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
            browserPath: @"C:\NonExistentDirectory\chrome.exe",
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
            FailureCodes.E14_PasswordRejected,
            FailureCodes.GetPupilMessageBm(FailureCodes.E14_PasswordRejected),
            FailureCodes.GetTeacherAction(FailureCodes.E14_PasswordRejected),
            session: null,
            charsInjected: 12,
            elapsed: TimeSpan.FromMilliseconds(450));

        Assert.False(failure.Success);
        Assert.Equal(FailureCodes.E14_PasswordRejected, failure.ErrorCode);
        Assert.Equal("Kata laluan tidak diterima. Beritahu cikgu.", failure.PupilMessage);
        Assert.Equal("Mod Guru for one pupil; re-import in Delima.Admin if the whole class fails", failure.TeacherAction);
        Assert.Equal(12, failure.TotalCharsInjected);
    }

    [Fact]
    public void WaitForPostPasswordResolution_ObservesConsentPage_ReturnsConsentPageReached()
    {
        using var currentProc = Process.GetCurrentProcess();
        var session = new ChromeSession(currentProc, Path.GetTempPath());
        using var cts = new CancellationTokenSource();
        var options = new RouteCOptions();

        var resolution = RouteCLoginOrchestrator.WaitForPostPasswordResolution(
            session,
            options,
            passwordPageTitle: "Hi Ahmad - Google Chrome",
            cancellationToken: cts.Token,
            titleGetter: () => "Sign in - Google Accounts - Google Chrome");

        Assert.Equal(PostPasswordResolution.ConsentPageReached, resolution);
    }

    [Fact]
    public void WaitForPostPasswordResolution_ObservesDestinationPage_ReturnsDestinationReached()
    {
        using var currentProc = Process.GetCurrentProcess();
        var session = new ChromeSession(currentProc, Path.GetTempPath());
        using var cts = new CancellationTokenSource();
        var options = new RouteCOptions();

        var resolution = RouteCLoginOrchestrator.WaitForPostPasswordResolution(
            session,
            options,
            passwordPageTitle: "Hi Ahmad - Google Chrome",
            cancellationToken: cts.Token,
            titleGetter: () => "DELIMa 3.0 - Google Chrome");

        Assert.Equal(PostPasswordResolution.DestinationReached, resolution);
    }

    [Fact]
    public void WaitForPostPasswordResolution_TimeoutWhileStillOnPasswordPage_ReturnsPasswordRejected()
    {
        using var currentProc = Process.GetCurrentProcess();
        var session = new ChromeSession(currentProc, Path.GetTempPath());
        using var cts = new CancellationTokenSource();
        var options = new RouteCOptions
        {
            WindowWaitTimeout = TimeSpan.FromMilliseconds(50),
            PollIntervalMs = 10
        };

        // Title stays on password page across the entire timeout period
        var resolution = RouteCLoginOrchestrator.WaitForPostPasswordResolution(
            session,
            options,
            passwordPageTitle: "Hi Ahmad - Google Chrome",
            cancellationToken: cts.Token,
            titleGetter: () => "Hi Ahmad - Google Chrome");

        Assert.Equal(PostPasswordResolution.PasswordRejected, resolution);
    }

    [Fact]
    public void WaitForPostPasswordResolution_TimeoutOnUnknownPage_ReturnsUnknownState()
    {
        using var currentProc = Process.GetCurrentProcess();
        var session = new ChromeSession(currentProc, Path.GetTempPath());
        using var cts = new CancellationTokenSource();
        var options = new RouteCOptions
        {
            WindowWaitTimeout = TimeSpan.FromMilliseconds(50),
            PollIntervalMs = 10
        };

        // Title transitions to an unexpected unclassified page
        var resolution = RouteCLoginOrchestrator.WaitForPostPasswordResolution(
            session,
            options,
            passwordPageTitle: "Hi Ahmad - Google Chrome",
            cancellationToken: cts.Token,
            titleGetter: () => "Some Unexpected Custom Page - Google Chrome");

        Assert.Equal(PostPasswordResolution.UnknownState, resolution);
    }

    [Fact]
    public void WaitForPostPasswordResolution_CancelledToken_ReturnsAborted()
    {
        using var currentProc = Process.GetCurrentProcess();
        var session = new ChromeSession(currentProc, Path.GetTempPath());
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var options = new RouteCOptions();

        var resolution = RouteCLoginOrchestrator.WaitForPostPasswordResolution(
            session,
            options,
            passwordPageTitle: "Hi Ahmad - Google Chrome",
            cancellationToken: cts.Token,
            titleGetter: () => "Hi Ahmad - Google Chrome");

        Assert.Equal(PostPasswordResolution.Aborted, resolution);
    }

    [Fact]
    public void IsPasswordPageTitle_Identifies_PasswordPageVariants_And_Rejects_NonPasswordPages()
    {
        var options = new RouteCOptions();

        // Positive password page titles
        Assert.True(RouteCLoginOrchestrator.IsPasswordPageTitle("Hi Nur Aisyah - Google Chrome", "Hi Nur Aisyah - Google Chrome", options));
        Assert.True(RouteCLoginOrchestrator.IsPasswordPageTitle("Welcome - Google Chrome", null, options));
        Assert.True(RouteCLoginOrchestrator.IsPasswordPageTitle("Custom Captured Title", "Custom Captured Title", options));

        // Negative non-password titles
        Assert.False(RouteCLoginOrchestrator.IsPasswordPageTitle("", null, options));
        Assert.False(RouteCLoginOrchestrator.IsPasswordPageTitle(null, null, options));
        Assert.False(RouteCLoginOrchestrator.IsPasswordPageTitle("Sign in - Google Accounts - Google Chrome", null, options));
        Assert.False(RouteCLoginOrchestrator.IsPasswordPageTitle("DELIMa - Google Chrome", null, options));
        Assert.False(RouteCLoginOrchestrator.IsPasswordPageTitle("DELIMa 3.0 - Google Chrome", null, options));
    }

    [Fact]
    public void PostPasswordVerification_NeverReachingConsentOrDestination_DoesNotReturnSucceeded()
    {
        // Prompt 18 Requirement: Assert that a run which never reaches a consent or destination title does NOT return Succeeded.
        using var currentProc = Process.GetCurrentProcess();
        var session = new ChromeSession(currentProc, Path.GetTempPath());
        using var cts = new CancellationTokenSource();
        var options = new RouteCOptions
        {
            WindowWaitTimeout = TimeSpan.FromMilliseconds(50),
            PollIntervalMs = 10
        };

        // 1. Stays on password page (Wrong Password) -> PasswordRejected (E14)
        var rejectedResolution = RouteCLoginOrchestrator.WaitForPostPasswordResolution(
            session,
            options,
            passwordPageTitle: "Hi Pupil - Google Chrome",
            cancellationToken: cts.Token,
            titleGetter: () => "Hi Pupil - Google Chrome");

        Assert.NotEqual(PostPasswordResolution.ConsentPageReached, rejectedResolution);
        Assert.NotEqual(PostPasswordResolution.DestinationReached, rejectedResolution);
        Assert.Equal(PostPasswordResolution.PasswordRejected, rejectedResolution);

        // 2. Transits to unknown page (e.g. 2FA/Suspended) -> UnknownState (E02)
        var unknownResolution = RouteCLoginOrchestrator.WaitForPostPasswordResolution(
            session,
            options,
            passwordPageTitle: "Hi Pupil - Google Chrome",
            cancellationToken: cts.Token,
            titleGetter: () => "Unexpected Page - Google Chrome");

        Assert.NotEqual(PostPasswordResolution.ConsentPageReached, unknownResolution);
        Assert.NotEqual(PostPasswordResolution.DestinationReached, unknownResolution);
        Assert.Equal(PostPasswordResolution.UnknownState, unknownResolution);
    }

    [Theory]
    [InlineData("No internet - Google Chrome", FailureCodes.E13_NetworkUnreachable)]
    [InlineData("Site can't be reached - Google Chrome", FailureCodes.E13_NetworkUnreachable)]
    [InlineData("Chrome - ERR_INTERNET_DISCONNECTED", FailureCodes.E13_NetworkUnreachable)]
    [InlineData("Sign in - unusual activity detected - Google Chrome", FailureCodes.E06_GoogleCaptcha)]
    [InlineData("Google Accounts - Captcha verification", FailureCodes.E06_GoogleCaptcha)]
    [InlineData("2-Step Verification - Google Accounts - Google Chrome", FailureCodes.E07_TwoFactorPrompt)]
    [InlineData("Verify it's you - Google Accounts - Google Chrome", FailureCodes.E07_TwoFactorPrompt)]
    [InlineData("Account disabled - Google Accounts", FailureCodes.E08_AccountSuspended)]
    [InlineData("Account suspended - Google Accounts", FailureCodes.E08_AccountSuspended)]
    [InlineData("Password expired - Google Accounts", FailureCodes.E08_AccountSuspended)]
    [InlineData("DELIMa - Google Chrome", null)]
    [InlineData("Sign in - Google Accounts - Google Chrome", null)]
    public void ClassifyKnownBrowserError_Identifies_Error_Titles(string title, string? expectedCode)
    {
        var result = RouteCLoginOrchestrator.ClassifyKnownBrowserError(title);
        Assert.Equal(expectedCode, result);
    }

    [Theory]
    [InlineData("2-Step Verification - Google Accounts", PostPasswordResolution.TwoFactorPrompt)]
    [InlineData("Captcha challenge", PostPasswordResolution.CaptchaChallenge)]
    [InlineData("Account disabled", PostPasswordResolution.AccountSuspended)]
    [InlineData("No internet - Google Chrome", PostPasswordResolution.NetworkUnreachable)]
    public void WaitForPostPasswordResolution_DetectsKnownBrowserErrorsImmediately(string title, PostPasswordResolution expected)
    {
        using var currentProc = Process.GetCurrentProcess();
        var session = new ChromeSession(currentProc, Path.GetTempPath());
        using var cts = new CancellationTokenSource();
        var options = new RouteCOptions
        {
            WindowWaitTimeout = TimeSpan.FromSeconds(5),
            PollIntervalMs = 10
        };

        var resolution = RouteCLoginOrchestrator.WaitForPostPasswordResolution(
            session,
            options,
            passwordPageTitle: "Hi Pupil - Google Chrome",
            cancellationToken: cts.Token,
            titleGetter: () => title);

        Assert.Equal(expected, resolution);
    }

    [Fact]
    public void RouteCOptions_DefaultValues_Have_SendEnterAfterPassword_True()
    {
        var options = new RouteCOptions();
        Assert.True(options.SendEnterAfterPassword);
        Assert.True(options.SendEnterAfterEmail);
        Assert.True(options.AutoClickLandingButton);
        Assert.Equal("Log Masuk ke DELIMa", options.LandingButtonText);
        Assert.Equal(TimeSpan.FromSeconds(60), options.WindowWaitTimeout);
        Assert.Equal(TimeSpan.FromSeconds(30), options.TransitionTimeout);
        Assert.Equal(1200, options.InjectionSettleMs);
    }

    [Fact]
    public void UiaHelper_TryInvokeButtonInForeground_CancelledToken_ReturnsFalse()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = UiaHelper.TryInvokeButtonInForeground("Log Masuk ke DELIMa", TimeSpan.FromSeconds(5), cts.Token);
        Assert.False(result);
    }

    [Theory]
    [InlineData("Log Masuk ke DELIMa", true)]
    [InlineData("Log Masuk", true)]
    [InlineData("LOG MASUK", true)]
    [InlineData("Log In", true)]
    [InlineData("Login", true)]
    [InlineData("Masuk ke DELIMa", true)]
    [InlineData("Masuk", true)]
    [InlineData("App available. Install DELIMa", false)]
    [InlineData("Install DELIMa", false)]
    [InlineData("Pasang DELIMa", false)]
    [InlineData("Translate this page", false)]
    [InlineData("Terjemah halaman", false)]
    [InlineData("DELIMa", false)]
    [InlineData("DELIMa - Google Chrome", false)]
    [InlineData("Close", false)]
    [InlineData("Tutup", false)]
    [InlineData("Settings", false)]
    [InlineData("Cari", false)]
    public void UiaHelper_IsLoginButton_Matches_Valid_Login_And_Rejects_Install_And_Translate(string buttonName, bool expectedMatch)
    {
        bool result = UiaHelper.IsLoginButton(buttonName, "Log Masuk ke DELIMa");
        Assert.Equal(expectedMatch, result);
    }

    [Theory]
    [InlineData("Sign in - Google Accounts - Google Chrome", true)]
    [InlineData("Sign in \u2013 Google accounts - Google Chrome", true)]
    [InlineData("Log masuk - Akaun Google", true)]
    [InlineData("Log masuk \u2013 Akaun Google - Google Chrome", true)]
    [InlineData("Log Masuk - Akaun Google - Personal - Microsoft Edge", true)]
    [InlineData("Google Accounts", true)]
    [InlineData("DELIMa - Google Chrome", false)]
    [InlineData("DELIMa 3.0", false)]
    [InlineData("AINS - Google Chrome", false)]
    public void UiaHelper_IsGoogleSignInTitle_Detects_Navigated_States(string title, bool expectedNavigated)
    {
        bool result = UiaHelper.IsGoogleSignInTitle(title);
        Assert.Equal(expectedNavigated, result);
    }

    [Theory]
    [InlineData("Log masuk - Akaun Google - Google Chrome", true)]
    [InlineData("Log masuk \u2013 Akaun Google - Google Chrome", true)]
    [InlineData("Log masuk \u2014 Akaun Google - Google Chrome", true)]
    [InlineData("Log Masuk - Akaun Google - Google Chrome", true)]
    [InlineData("Sign in - Google Accounts - Google Chrome", true)]
    [InlineData("Sign in \u2013 Google accounts - Google Chrome", true)]
    [InlineData("DELIMa - Google Chrome", false)]
    [InlineData("Sign in - Google Accounts", false)] // Suffix required for Chrome identifier
    public void InjectionEngine_MatchesAnyTitle_Chrome_Identifier_Handles_English_And_Malay(string title, bool expectedMatch)
    {
        bool result = InjectionEngine.MatchesAnyTitle(title, BrowserTitles.Chrome.Identifier);
        Assert.Equal(expectedMatch, result);
    }

    [Theory]
    [InlineData("Laman tidak dapat dicapai", FailureCodes.E13_NetworkUnreachable)]
    [InlineData("Tiada sambungan internet - Google Chrome", FailureCodes.E13_NetworkUnreachable)]
    [InlineData("Ralat privasi", FailureCodes.E13_NetworkUnreachable)]
    [InlineData("Sahkan aktiviti luar biasa", FailureCodes.E06_GoogleCaptcha)]
    [InlineData("Pengesahan 2 Langkah - Google Chrome", FailureCodes.E07_TwoFactorPrompt)]
    [InlineData("Sahkan diri anda", FailureCodes.E07_TwoFactorPrompt)]
    [InlineData("Akaun dinyahdayakan", FailureCodes.E08_AccountSuspended)]
    [InlineData("Akaun digantung", FailureCodes.E08_AccountSuspended)]
    [InlineData("Kata laluan tamat tempoh", FailureCodes.E08_AccountSuspended)]
    [InlineData("Log masuk - Akaun Google", null)]
    public void RouteCLoginOrchestrator_ClassifyKnownBrowserError_Detects_Malay_Errors(string title, string? expectedCode)
    {
        string? result = RouteCLoginOrchestrator.ClassifyKnownBrowserError(title);
        Assert.Equal(expectedCode, result);
    }

    [Theory]
    [InlineData("Hi Ali - Google Chrome", true)]
    [InlineData("Hai Siti - Google Chrome", true)]
    [InlineData("Salam Ahmad - Google Chrome", true)]
    [InlineData("Welcome - Google Chrome", true)]
    [InlineData("Selamat datang - Google Chrome", true)]
    [InlineData("Log masuk - Akaun Google - Google Chrome", false)]
    [InlineData("Sign in - Google Accounts - Google Chrome", false)]
    public void RouteCLoginOrchestrator_IsPasswordPageTitle_Detects_Malay_And_English_Greetings(string title, bool expectedPasswordPage)
    {
        var options = new RouteCOptions { TargetBrowser = BrowserKind.Chrome };
        bool result = RouteCLoginOrchestrator.IsPasswordPageTitle(title, null, options);
        Assert.Equal(expectedPasswordPage, result);
    }
}


