using System.Diagnostics;
using System.IO;
using Delima.Core.Audit;
using Delima.Core.Store;

namespace Delima.Win32;

/// <summary>
/// States of the Route C login orchestration pipeline.
/// </summary>
public enum LoginFlowState
{
    NotStarted,
    LaunchingBrowser,
    WaitingForIdentifierPage,
    InjectingIdentifier,
    WaitingForTransition,
    WaitingForPasswordPage,
    InjectingPassword,
    WaitingForConsentPage,
    Completed,
    Failed,
    Aborted
}

/// <summary>
/// Configuration options for the Route C login flow per Technical Architecture §3.2, §4.2, §4.5, and Appendix B.
/// </summary>
public sealed record RouteCOptions
{
    /// <summary>
    /// Initial landing / handoff URL.
    /// Default is "https://d3.delima.edu.my/landing" confirmed at T0.2.
    /// </summary>
    public string EntryUrl { get; init; } = "https://d3.delima.edu.my/landing";

    /// <summary>
    /// Exact expected window titles for Google's identifier (email) page.
    /// Matched as exact Ordinal equality against any entry in the list (§4.2).
    /// Defaults to both measured T0.4 variants (hyphen and en-dash).
    /// </summary>
    public IReadOnlyList<string> TitleIdentifierPage { get; init; } = new[]
    {
        "Sign in - Google Accounts - Google Chrome",
        "Sign in \u2013 Google accounts - Google Chrome"
    };

    /// <summary>
    /// Window titles for Google's OAuth consent screen (§4.5, Appendix B).
    /// Guarded by state rather than string alone: Consent state may only be entered
    /// after successful password injection in the same run.
    /// </summary>
    public IReadOnlyList<string> TitleConsentPage { get; init; } = new[]
    {
        "Sign in - Google Accounts - Google Chrome",
        "Sign in \u2013 Google accounts - Google Chrome"
    };

    /// <summary>
    /// Window titles for destination pages when OAuth consent is skipped (e.g. domain-trusted application).
    /// Matched as exact Ordinal equality against any entry in the list (§4.5).
    /// </summary>
    public IReadOnlyList<string> TitleDestinationPage { get; init; } = new[]
    {
        "DELIMa - Google Chrome",
        "DELIMa 3.0 - Google Chrome",
        "Classes - Google Classroom - Google Chrome",
        "Google Classroom - Google Chrome"
    };

    /// <summary>
    /// Settle delay duration in milliseconds after window verification (700 ms default per T0.4 latency findings).
    /// </summary>
    public int InjectionSettleMs { get; init; } = 700;

    /// <summary>
    /// Consecutive 100 ms polls a title must hold stably before initiating injection (§4.2).
    /// Default is 3 polls.
    /// </summary>
    public int TitleSettlePolls { get; init; } = 3;

    /// <summary>
    /// Interval in milliseconds between window title samples. Default is 100 ms.
    /// </summary>
    public int PollIntervalMs { get; init; } = 100;

    /// <summary>
    /// Timeout when waiting for a specific page title to appear and settle.
    /// Default is 30 seconds per §3.2.
    /// </summary>
    public TimeSpan WindowWaitTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Timeout when waiting for the browser to transition out of the identifier page.
    /// Default is 15 seconds.
    /// </summary>
    public TimeSpan TransitionTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Expected Chrome window class name. Default is "Chrome_WidgetWin_1".
    /// </summary>
    public string ExpectedClassName { get; init; } = "Chrome_WidgetWin_1";

    /// <summary>
    /// Whether to require UI Automation IsPassword == true before password injection (§4.2, §11.1 T0.4).
    /// Enabled (true) by default per T0.4 verification.
    /// </summary>
    public bool CheckUiaPasswordElement { get; init; } = true;

    /// <summary>
    /// Whether to send Enter key after typing email. Default is true.
    /// </summary>
    public bool SendEnterAfterEmail { get; init; } = true;

    /// <summary>
    /// Whether to send Enter key after typing password. Default is false per §4.2.
    /// </summary>
    public bool SendEnterAfterPassword { get; init; } = false;

    /// <summary>
    /// Delay between keystrokes in ms. Default is 0 ms.
    /// </summary>
    public int PerCharDelayMs { get; init; } = 0;
}

/// <summary>
/// Result of a Route C login orchestration attempt.
/// </summary>
public sealed record RouteCResult
{
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? PupilMessage { get; init; }
    public string? TeacherAction { get; init; }
    public ChromeSession? Session { get; init; }
    public int TotalCharsInjected { get; init; }
    public TimeSpan Elapsed { get; init; }

    public static RouteCResult Succeeded(ChromeSession session, int charsInjected, TimeSpan elapsed) =>
        new()
        {
            Success = true,
            Session = session,
            TotalCharsInjected = charsInjected,
            Elapsed = elapsed
        };

    public static RouteCResult Failure(
        string errorCode,
        string pupilMessage,
        string teacherAction,
        ChromeSession? session,
        int charsInjected,
        TimeSpan elapsed) =>
        new()
        {
            Success = false,
            ErrorCode = errorCode,
            PupilMessage = pupilMessage,
            TeacherAction = teacherAction,
            Session = session,
            TotalCharsInjected = charsInjected,
            Elapsed = elapsed
        };

    public static RouteCResult FromInjectionResult(InjectionResult ir, ChromeSession? session, int priorChars = 0) =>
        new()
        {
            Success = ir.Success,
            ErrorCode = ir.ErrorCode,
            PupilMessage = ir.PupilMessage ?? FailureCodes.GetPupilMessageBm(ir.ErrorCode ?? FailureCodes.E02_WindowNotVerified),
            TeacherAction = ir.TeacherAction ?? FailureCodes.GetTeacherAction(ir.ErrorCode ?? FailureCodes.E02_WindowNotVerified),
            Session = session,
            TotalCharsInjected = priorChars + ir.CharactersInjected,
            Elapsed = ir.Elapsed
        };
}

/// <summary>
/// Orchestrates the two-stage visual SSO login flow (Route C) per Technical Architecture §4.2, §4.4, §4.5, and §7.
/// Sequence-gated: Password injection cannot fire without a preceding verified transition out of the identifier page.
/// </summary>
public static class RouteCLoginOrchestrator
{
    /// <summary>
    /// Executes the full Route C two-step injection flow for a pupil session.
    /// </summary>
    public static async Task<RouteCResult> ExecuteAsync(
        string? chromePath,
        string email,
        ICredential credential,
        RouteCOptions? options = null,
        Action<LoginFlowState>? onStateChanged = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentNullException.ThrowIfNull(credential);

        options ??= new RouteCOptions();
        var sw = Stopwatch.StartNew();

        // 1. Resolve Chrome executable path
        var resolvedChrome = chromePath ?? ChromeSession.ResolveChromePath();
        if (string.IsNullOrEmpty(resolvedChrome) || !File.Exists(resolvedChrome))
        {
            onStateChanged?.Invoke(LoginFlowState.Failed);
            return RouteCResult.Failure(
                FailureCodes.E01_ChromeNotInstalled,
                FailureCodes.GetPupilMessageBm(FailureCodes.E01_ChromeNotInstalled),
                FailureCodes.GetTeacherAction(FailureCodes.E01_ChromeNotInstalled),
                session: null,
                charsInjected: 0,
                sw.Elapsed);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            onStateChanged?.Invoke(LoginFlowState.Aborted);
            return RouteCResult.Failure(
                FailureCodes.E03_InjectionAborted,
                FailureCodes.GetPupilMessageBm(FailureCodes.E03_InjectionAborted),
                FailureCodes.GetTeacherAction(FailureCodes.E03_InjectionAborted),
                session: null,
                charsInjected: 0,
                sw.Elapsed);
        }

        // 2. Launch Chrome session with throwaway profile
        onStateChanged?.Invoke(LoginFlowState.LaunchingBrowser);
        ChromeSession? session = null;

        try
        {
            session = ChromeSession.Launch(resolvedChrome, options.EntryUrl);

            var injectionOptions = new InjectionOptions
            {
                WindowWaitTimeout = options.WindowWaitTimeout,
                TitleSettlePolls = options.TitleSettlePolls,
                PollIntervalMs = options.PollIntervalMs,
                InjectionSettleMs = options.InjectionSettleMs,
                PerCharDelayMs = options.PerCharDelayMs,
                ExpectedClassName = options.ExpectedClassName
            };

            int totalChars = 0;

            // ====================================================================
            // Step 1: Identifier (Email) Injection
            // ====================================================================
            onStateChanged?.Invoke(LoginFlowState.WaitingForIdentifierPage);

            var emailResult = await Task.Run(() =>
            {
                onStateChanged?.Invoke(LoginFlowState.InjectingIdentifier);
                return InjectionEngine.Inject(
                    session,
                    email.AsSpan(),
                    options.TitleIdentifierPage,
                    injectionOptions with { SendEnter = options.SendEnterAfterEmail },
                    cancellationToken);
            }, cancellationToken);

            if (!emailResult.Success)
            {
                var state = emailResult.ErrorCode == FailureCodes.E03_InjectionAborted
                    ? LoginFlowState.Aborted
                    : LoginFlowState.Failed;
                onStateChanged?.Invoke(state);

                session.Dispose();
                return RouteCResult.FromInjectionResult(emailResult, null);
            }

            totalChars += emailResult.CharactersInjected;

            // ====================================================================
            // Sequence Gate (§4.2): Must observe verified transition OUT OF identifier title
            // ====================================================================
            onStateChanged?.Invoke(LoginFlowState.WaitingForTransition);
            var transitionVerified = await Task.Run(() =>
                WaitForTransitionOut(session, options.TitleIdentifierPage, options.TransitionTimeout, options.PollIntervalMs, cancellationToken),
                cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                onStateChanged?.Invoke(LoginFlowState.Aborted);
                session.Dispose();
                return RouteCResult.Failure(
                    FailureCodes.E03_InjectionAborted,
                    FailureCodes.GetPupilMessageBm(FailureCodes.E03_InjectionAborted),
                    FailureCodes.GetTeacherAction(FailureCodes.E03_InjectionAborted),
                    null, totalChars, sw.Elapsed);
            }

            if (!transitionVerified)
            {
                // Title failed to transition away from identifier page within timeout
                onStateChanged?.Invoke(LoginFlowState.Failed);
                session.Dispose();
                return RouteCResult.Failure(
                    FailureCodes.E02_WindowNotVerified,
                    FailureCodes.GetPupilMessageBm(FailureCodes.E02_WindowNotVerified),
                    FailureCodes.GetTeacherAction(FailureCodes.E02_WindowNotVerified),
                    null, totalChars, sw.Elapsed);
            }

            // ====================================================================
            // Step 2: Password Injection (Sequence-Gated per §4.2 & T0.4)
            // ====================================================================
            onStateChanged?.Invoke(LoginFlowState.WaitingForPasswordPage);

            string? capturedPasswordTitle = null;
            var passwordResult = await Task.Run(() =>
            {
                onStateChanged?.Invoke(LoginFlowState.InjectingPassword);

                var passwordOptions = injectionOptions with
                {
                    SendEnter = options.SendEnterAfterPassword,
                    PreInjectionCheck = options.CheckUiaPasswordElement && OperatingSystem.IsWindows()
                        ? () => UiaHelper.IsFocusedElementPassword()
                        : null
                };

                // §4.2: Title check degrades to sequence-and-stability (changed away from identifier titles and stable for TitleSettlePolls)
                // Primary gate is IsPassword == true via UIA PreInjectionCheck.
                return InjectionEngine.Inject(
                    session,
                    credential.PasswordSpan,
                    title =>
                    {
                        var matches = !string.IsNullOrWhiteSpace(title) && !options.TitleIdentifierPage.Any(idTitle => string.Equals(title, idTitle, StringComparison.Ordinal));
                        if (matches)
                        {
                            capturedPasswordTitle = title;
                        }
                        return matches;
                    },
                    passwordOptions,
                    cancellationToken);
            }, cancellationToken);

            if (!passwordResult.Success)
            {
                var state = passwordResult.ErrorCode == FailureCodes.E03_InjectionAborted
                    ? LoginFlowState.Aborted
                    : LoginFlowState.Failed;
                onStateChanged?.Invoke(state);

                session.Dispose();
                return RouteCResult.FromInjectionResult(passwordResult, null, totalChars);
            }

            totalChars += passwordResult.CharactersInjected;

            // ====================================================================
            // Step 3: OAuth Consent Screen / Destination Verification (§4.5 & §7.1)
            // Sequence: identifier → password → consent → destination.
            // Reaching consent is the normal, successful terminal state of injection.
            // The software does NOT click Continue (pupil presses it; identity check G2).
            // Topmost overlay is already down since password injection completed.
            // Guarded by state rather than string alone: Consent state may only be entered
            // AFTER successful password injection in the same run, never from title match alone.
            // ====================================================================
            var resolution = await Task.Run(() =>
                WaitForPostPasswordResolution(session, options, capturedPasswordTitle, cancellationToken),
                cancellationToken);

            switch (resolution)
            {
                case PostPasswordResolution.ConsentPageReached:
                    onStateChanged?.Invoke(LoginFlowState.WaitingForConsentPage);
                    return RouteCResult.Succeeded(session, totalChars, sw.Elapsed);

                case PostPasswordResolution.DestinationReached:
                    onStateChanged?.Invoke(LoginFlowState.Completed);
                    return RouteCResult.Succeeded(session, totalChars, sw.Elapsed);

                case PostPasswordResolution.PasswordRejected:
                    onStateChanged?.Invoke(LoginFlowState.Failed);
                    session.Dispose();
                    return RouteCResult.Failure(
                        FailureCodes.E14_PasswordRejected,
                        FailureCodes.GetPupilMessageBm(FailureCodes.E14_PasswordRejected),
                        FailureCodes.GetTeacherAction(FailureCodes.E14_PasswordRejected),
                        null, totalChars, sw.Elapsed);

                case PostPasswordResolution.Aborted:
                    onStateChanged?.Invoke(LoginFlowState.Aborted);
                    session.Dispose();
                    return RouteCResult.Failure(
                        FailureCodes.E03_InjectionAborted,
                        FailureCodes.GetPupilMessageBm(FailureCodes.E03_InjectionAborted),
                        FailureCodes.GetTeacherAction(FailureCodes.E03_InjectionAborted),
                        null, totalChars, sw.Elapsed);

                default: // UnknownState
                    onStateChanged?.Invoke(LoginFlowState.Failed);
                    session.Dispose();
                    return RouteCResult.Failure(
                        FailureCodes.E02_WindowNotVerified,
                        FailureCodes.GetPupilMessageBm(FailureCodes.E02_WindowNotVerified),
                        FailureCodes.GetTeacherAction(FailureCodes.E02_WindowNotVerified),
                        null, totalChars, sw.Elapsed);
            }
        }
        catch (OperationCanceledException)
        {
            onStateChanged?.Invoke(LoginFlowState.Aborted);
            session?.Dispose();
            return RouteCResult.Failure(
                FailureCodes.E03_InjectionAborted,
                FailureCodes.GetPupilMessageBm(FailureCodes.E03_InjectionAborted),
                FailureCodes.GetTeacherAction(FailureCodes.E03_InjectionAborted),
                null, 0, sw.Elapsed);
        }
        catch (Exception ex)
        {
            onStateChanged?.Invoke(LoginFlowState.Failed);
            session?.Dispose();
            return RouteCResult.Failure(
                FailureCodes.E02_WindowNotVerified,
                FailureCodes.GetPupilMessageBm(FailureCodes.E02_WindowNotVerified),
                FailureCodes.GetTeacherAction(FailureCodes.E02_WindowNotVerified) + $": {ex.Message}",
                null, 0, sw.Elapsed);
        }
    }

    /// <summary>
    /// Polls until the window title leaves all identifier page titles, establishing the sequence gate per §4.2.
    /// </summary>
    internal static bool WaitForTransitionOut(
        ChromeSession session,
        IReadOnlyList<string> identifierTitles,
        TimeSpan timeout,
        int pollIntervalMs,
        CancellationToken cancellationToken,
        Func<string>? titleGetter = null)
    {
        var sw = Stopwatch.StartNew();
        int interval = Math.Max(20, pollIntervalMs);
        var getTitle = titleGetter ?? NativeMethods.GetForegroundTitle;

        while (sw.Elapsed < timeout)
        {
            if (cancellationToken.IsCancellationRequested) return false;

            var currentTitle = getTitle();
            // If the title has changed from all identifier titles and window is non-empty, transition observed
            if (!string.IsNullOrEmpty(currentTitle) &&
                !identifierTitles.Any(idTitle => string.Equals(currentTitle, idTitle, StringComparison.Ordinal)))
            {
                return true;
            }

            Thread.Sleep(interval);
        }

        return false;
    }

    /// <summary>
    /// Overload accepting a single identifier title for backward compatibility.
    /// </summary>
    internal static bool WaitForTransitionOut(
        ChromeSession session,
        string identifierTitle,
        TimeSpan timeout,
        int pollIntervalMs,
        CancellationToken cancellationToken,
        Func<string>? titleGetter = null) =>
        WaitForTransitionOut(session, new[] { identifierTitle }, timeout, pollIntervalMs, cancellationToken, titleGetter);

    /// <summary>
    /// Polls after password injection until consent screen or destination is reached,
    /// or detects password rejection / timeout per §4.5 and §7.1.
    /// </summary>
    internal static PostPasswordResolution WaitForPostPasswordResolution(
        ChromeSession session,
        RouteCOptions options,
        string? passwordPageTitle,
        CancellationToken cancellationToken,
        Func<string>? titleGetter = null)
    {
        var sw = Stopwatch.StartNew();
        int interval = Math.Max(20, options.PollIntervalMs);
        var getTitle = titleGetter ?? NativeMethods.GetForegroundTitle;

        while (sw.Elapsed < options.WindowWaitTimeout)
        {
            if (cancellationToken.IsCancellationRequested) return PostPasswordResolution.Aborted;

            var currentTitle = getTitle();

            // 1. Consent screen reached (§4.5)
            if (InjectionEngine.MatchesAnyTitle(currentTitle, options.TitleConsentPage))
            {
                return PostPasswordResolution.ConsentPageReached;
            }

            // 2. Destination reached (consent skipped / domain-trusted)
            if (InjectionEngine.MatchesAnyTitle(currentTitle, options.TitleDestinationPage))
            {
                return PostPasswordResolution.DestinationReached;
            }

            Thread.Sleep(interval);
        }

        if (cancellationToken.IsCancellationRequested) return PostPasswordResolution.Aborted;

        // 3. Check if still on a password-page title after timeout -> Password rejected (§7.1)
        var finalTitle = getTitle();
        if (IsPasswordPageTitle(finalTitle, passwordPageTitle, options))
        {
            return PostPasswordResolution.PasswordRejected;
        }

        // 4. Anything else -> Unknown state (E02)
        return PostPasswordResolution.UnknownState;
    }

    /// <summary>
    /// Evaluates whether the window title corresponds to Google's password page per §4.2 and §7.1.
    /// </summary>
    internal static bool IsPasswordPageTitle(string? currentTitle, string? passwordPageTitle, RouteCOptions options)
    {
        if (string.IsNullOrWhiteSpace(currentTitle)) return false;

        // Not a password page if it matches consent, destination, or identifier titles
        if (InjectionEngine.MatchesAnyTitle(currentTitle, options.TitleConsentPage)) return false;
        if (InjectionEngine.MatchesAnyTitle(currentTitle, options.TitleDestinationPage)) return false;
        if (InjectionEngine.MatchesAnyTitle(currentTitle, options.TitleIdentifierPage)) return false;

        // Title is unchanged from the observed password page title during injection
        if (!string.IsNullOrEmpty(passwordPageTitle) && string.Equals(currentTitle, passwordPageTitle, StringComparison.Ordinal))
        {
            return true;
        }

        // Common Google password page title formats
        if (currentTitle.StartsWith("Hi ", StringComparison.Ordinal) ||
            currentTitle.StartsWith("Welcome", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }
}

/// <summary>
/// Possible resolution states after password injection in Route C (§4.5, §7.1).
/// </summary>
internal enum PostPasswordResolution
{
    ConsentPageReached,
    DestinationReached,
    PasswordRejected,
    UnknownState,
    Aborted
}
