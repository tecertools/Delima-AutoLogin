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
/// Configuration options for the Route C login flow per Technical Architecture §3.2, §4.2, §4.4.1, §4.5, and Appendix B.
/// </summary>
public sealed record RouteCOptions
{
    /// <summary>
    /// Initial landing / handoff URL.
    /// Default is "https://d3.delima.edu.my/landing" confirmed at T0.2.
    /// </summary>
    public string EntryUrl { get; init; } = "https://d3.delima.edu.my/landing";

    /// <summary>
    /// Preferred browser configuration key ("auto", "edge", or "chrome").
    /// Default is "auto" (prefers Edge, fallback to Chrome) per §4.4.1.
    /// </summary>
    public string PreferredBrowser { get; init; } = "auto";

    /// <summary>
    /// Target browser kind for title defaults. If not explicitly set, defaults to Chrome titles
    /// for backward compatibility or is inferred during RouteCLoginOrchestrator execution.
    /// </summary>
    public BrowserKind? TargetBrowser { get; init; }

    private readonly IReadOnlyList<string>? _titleIdentifierPage;

    /// <summary>
    /// Exact expected window titles for Google's identifier (email) page.
    /// Matched as exact Ordinal equality against any entry in the list (§4.2). Substring matching is forbidden.
    /// Defaults to measured per-browser lists. For Edge, defaults to an empty unmeasured list that fails closed.
    /// </summary>
    public IReadOnlyList<string> TitleIdentifierPage
    {
        get => _titleIdentifierPage ?? (TargetBrowser == BrowserKind.Edge ? BrowserTitles.Edge.Identifier : BrowserTitles.Chrome.Identifier);
        init => _titleIdentifierPage = value;
    }

    private readonly IReadOnlyList<string>? _titleConsentPage;

    /// <summary>
    /// Window titles for Google's OAuth consent screen (§4.5, Appendix B).
    /// Guarded by state rather than string alone: Consent state may only be entered
    /// after successful password injection in the same run.
    /// </summary>
    public IReadOnlyList<string> TitleConsentPage
    {
        get => _titleConsentPage ?? (TargetBrowser == BrowserKind.Edge ? BrowserTitles.Edge.Consent : BrowserTitles.Chrome.Consent);
        init => _titleConsentPage = value;
    }

    private readonly IReadOnlyList<string>? _titleDestinationPage;

    /// <summary>
    /// Window titles for destination pages when OAuth consent is skipped (e.g. domain-trusted application).
    /// Matched as exact Ordinal equality against any entry in the list (§4.5).
    /// </summary>
    public IReadOnlyList<string> TitleDestinationPage
    {
        get => _titleDestinationPage ?? (TargetBrowser == BrowserKind.Edge ? BrowserTitles.Edge.Destination : BrowserTitles.Chrome.Destination);
        init => _titleDestinationPage = value;
    }

    /// <summary>
    /// Settle delay duration in milliseconds after window verification (1200 ms default to ensure input autofocus on slow PCs).
    /// </summary>
    public int InjectionSettleMs { get; init; } = 1200;

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
    /// Default is 60 seconds (raised for slow loading PCs).
    /// </summary>
    public TimeSpan WindowWaitTimeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Timeout when waiting for the browser to transition out of the identifier page.
    /// Default is 30 seconds (raised for slow loading PCs).
    /// </summary>
    public TimeSpan TransitionTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Expected Chromium window class name. Default is "Chrome_WidgetWin_1".
    /// Note per §4.2 &amp; §4.4.1: Edge and Chrome both report "Chrome_WidgetWin_1", so the class check
    /// cannot distinguish the two browsers and the PID check is doing all the work.
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
    /// Whether to send Enter key after typing password. Default is true.
    /// </summary>
    public bool SendEnterAfterPassword { get; init; } = true;

    /// <summary>
    /// Delay between keystrokes in ms. Default is 15 ms.
    /// </summary>
    public int PerCharDelayMs { get; init; } = 15;

    /// <summary>
    /// Whether to automatically find and click the landing page button (e.g. "Log Masuk ke DELIMa") via UI Automation.
    /// Default is true.
    /// </summary>
    public bool AutoClickLandingButton { get; init; } = true;

    /// <summary>
    /// Button label or text substring to match for auto-clicking on the landing page.
    /// Default is "Log Masuk ke DELIMa".
    /// </summary>
    public string LandingButtonText { get; init; } = "Log Masuk ke DELIMa";
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
    public BrowserSession? Session { get; init; }
    public int TotalCharsInjected { get; init; }
    public TimeSpan Elapsed { get; init; }

    public ChromeSession? ChromeSession => Session as ChromeSession ?? (Session != null ? new ChromeSession(Session.Process, Session.ProfileDir, Session.ExecutablePath) : null);

    public static RouteCResult Succeeded(BrowserSession session, int charsInjected, TimeSpan elapsed) =>
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
        BrowserSession? session,
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

    public static RouteCResult FromInjectionResult(InjectionResult ir, BrowserSession? session, int priorChars = 0) =>
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
/// Orchestrates the two-stage visual SSO login flow (Route C) per Technical Architecture §4.2, §4.4.1, §4.5, and §7.
/// Sequence-gated: Password injection cannot fire without a preceding verified transition out of the identifier page.
/// </summary>
public static class RouteCLoginOrchestrator
{
    /// <summary>
    /// Executes the full Route C two-step injection flow for a pupil session.
    /// Supports Microsoft Edge and Google Chrome, preferring Edge per §4.4.1.
    /// </summary>
    public static async Task<RouteCResult> ExecuteAsync(
        string? browserPath,
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

        // 1. Resolve browser executable path honoring preference (auto | edge | chrome)
        (BrowserKind Kind, string Path)? resolvedBrowser = null;
        if (!string.IsNullOrWhiteSpace(browserPath))
        {
            if (File.Exists(browserPath))
            {
                resolvedBrowser = (BrowserSession.DetectBrowserKind(browserPath), browserPath);
            }
        }
        else
        {
            resolvedBrowser = BrowserSession.ResolveBrowser(options.PreferredBrowser);
        }

        if (resolvedBrowser == null || !File.Exists(resolvedBrowser.Value.Path))
        {
            AuditLogger.RecordEntry(new AuditLogEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                Event = "browser_resolution_failed",
                Outcome = "FAILURE",
                OutcomeCode = FailureCodes.E01_NoBrowserFound,
                Details = $"No supported browser found (preference: {options.PreferredBrowser}).",
                SoftwareVersion = "2.0.0",
                WindowsUser = Environment.UserName
            });

            onStateChanged?.Invoke(LoginFlowState.Failed);
            return RouteCResult.Failure(
                FailureCodes.E01_NoBrowserFound,
                FailureCodes.GetPupilMessageBm(FailureCodes.E01_NoBrowserFound),
                FailureCodes.GetTeacherAction(FailureCodes.E01_NoBrowserFound),
                session: null,
                charsInjected: 0,
                sw.Elapsed);
        }

        // Record resolved browser in audit log (§4.4.1)
        AuditLogger.RecordEntry(new AuditLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Event = "browser_resolved",
            Outcome = "SUCCESS",
            Details = $"Resolved browser {resolvedBrowser.Value.Kind} at '{resolvedBrowser.Value.Path}' (preference: {options.PreferredBrowser}).",
            SoftwareVersion = "2.0.0",
            WindowsUser = Environment.UserName
        });

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

        // Configure effective options with resolved TargetBrowser if not explicitly set
        var effectiveOptions = options.TargetBrowser == null
            ? options with { TargetBrowser = resolvedBrowser.Value.Kind }
            : options;

        // 2. Launch browser session with throwaway profile
        onStateChanged?.Invoke(LoginFlowState.LaunchingBrowser);
        BrowserSession? session = null;

        try
        {
            session = BrowserSession.Launch(
                resolvedBrowser.Value.Path,
                effectiveOptions.EntryUrl,
                forceRendererAccessibility: true,
                browserKind: resolvedBrowser.Value.Kind);

            var injectionOptions = new InjectionOptions
            {
                WindowWaitTimeout = effectiveOptions.WindowWaitTimeout,
                TitleSettlePolls = effectiveOptions.TitleSettlePolls,
                PollIntervalMs = effectiveOptions.PollIntervalMs,
                InjectionSettleMs = effectiveOptions.InjectionSettleMs,
                PerCharDelayMs = effectiveOptions.PerCharDelayMs,
                ExpectedClassName = effectiveOptions.ExpectedClassName
            };

            int totalChars = 0;

            // If auto-clicking landing page button is enabled, start background UIA watcher
            CancellationTokenSource? landingCts = null;
            if (effectiveOptions.AutoClickLandingButton && OperatingSystem.IsWindows())
            {
                landingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var landingToken = landingCts.Token;
                _ = Task.Run(() =>
                {
                    try
                    {
                        UiaHelper.TryInvokeButtonInForeground(effectiveOptions.LandingButtonText, effectiveOptions.WindowWaitTimeout, session, landingToken);
                    }
                    catch
                    {
                        // Non-critical background task
                    }
                }, landingToken);
            }

            // ====================================================================
            // Step 1: Identifier (Email) Injection
            // ====================================================================
            onStateChanged?.Invoke(LoginFlowState.WaitingForIdentifierPage);

            var emailOptions = injectionOptions with
            {
                SendEnter = effectiveOptions.SendEnterAfterEmail,
                OnWindowVerified = () => onStateChanged?.Invoke(LoginFlowState.InjectingIdentifier)
            };

            var emailResult = await Task.Run(() =>
            {
                return InjectionEngine.Inject(
                    session,
                    email.AsSpan(),
                    effectiveOptions.TitleIdentifierPage,
                    emailOptions,
                    cancellationToken);
            }, cancellationToken);

            landingCts?.Cancel();
            landingCts?.Dispose();

            if (!emailResult.Success)
            {
                var classified = ClassifyKnownBrowserError(NativeMethods.GetForegroundTitle());
                var finalCode = (emailResult.ErrorCode == FailureCodes.E02_WindowNotVerified && classified != null)
                    ? classified
                    : emailResult.ErrorCode;

                var state = finalCode == FailureCodes.E03_InjectionAborted
                    ? LoginFlowState.Aborted
                    : LoginFlowState.Failed;
                onStateChanged?.Invoke(state);

                session.Dispose();
                return RouteCResult.Failure(
                    finalCode ?? FailureCodes.E02_WindowNotVerified,
                    emailResult.PupilMessage ?? FailureCodes.GetPupilMessageBm(finalCode ?? FailureCodes.E02_WindowNotVerified),
                    emailResult.TeacherAction ?? FailureCodes.GetTeacherAction(finalCode ?? FailureCodes.E02_WindowNotVerified),
                    null, emailResult.CharactersInjected, sw.Elapsed);
            }

            totalChars += emailResult.CharactersInjected;

            // ====================================================================
            // Sequence Gate (§4.2): Must observe verified transition OUT OF identifier title
            // ====================================================================
            onStateChanged?.Invoke(LoginFlowState.WaitingForTransition);
            var transitionVerified = await Task.Run(() =>
                WaitForTransitionOut(session, effectiveOptions.TitleIdentifierPage, effectiveOptions.TransitionTimeout, effectiveOptions.PollIntervalMs, cancellationToken),
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
                var classified = ClassifyKnownBrowserError(NativeMethods.GetForegroundTitle());
                var finalCode = classified ?? FailureCodes.E02_WindowNotVerified;

                onStateChanged?.Invoke(LoginFlowState.Failed);
                session.Dispose();
                return RouteCResult.Failure(
                    finalCode,
                    FailureCodes.GetPupilMessageBm(finalCode),
                    FailureCodes.GetTeacherAction(finalCode),
                    null, totalChars, sw.Elapsed);
            }

            // ====================================================================
            // Step 2: Password Injection (Sequence-Gated per §4.2 & T0.4)
            // ====================================================================
            onStateChanged?.Invoke(LoginFlowState.WaitingForPasswordPage);

            string? capturedPasswordTitle = null;
            var passwordResult = await Task.Run(() =>
            {
                var passwordOptions = injectionOptions with
                {
                    SendEnter = effectiveOptions.SendEnterAfterPassword,
                    OnWindowVerified = () => onStateChanged?.Invoke(LoginFlowState.InjectingPassword),
                    PreInjectionCheck = effectiveOptions.CheckUiaPasswordElement && OperatingSystem.IsWindows()
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
                        var matches = !string.IsNullOrWhiteSpace(title) && !InjectionEngine.MatchesAnyTitle(title, effectiveOptions.TitleIdentifierPage);
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
                var classified = ClassifyKnownBrowserError(NativeMethods.GetForegroundTitle());
                var finalCode = (passwordResult.ErrorCode == FailureCodes.E02_WindowNotVerified && classified != null)
                    ? classified
                    : passwordResult.ErrorCode;

                var state = finalCode == FailureCodes.E03_InjectionAborted
                    ? LoginFlowState.Aborted
                    : LoginFlowState.Failed;
                onStateChanged?.Invoke(state);

                session.Dispose();
                return RouteCResult.Failure(
                    finalCode ?? FailureCodes.E02_WindowNotVerified,
                    passwordResult.PupilMessage ?? FailureCodes.GetPupilMessageBm(finalCode ?? FailureCodes.E02_WindowNotVerified),
                    passwordResult.TeacherAction ?? FailureCodes.GetTeacherAction(finalCode ?? FailureCodes.E02_WindowNotVerified),
                    null, totalChars + passwordResult.CharactersInjected, sw.Elapsed);
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
                WaitForPostPasswordResolution(session, effectiveOptions, capturedPasswordTitle, cancellationToken),
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

                case PostPasswordResolution.CaptchaChallenge:
                    onStateChanged?.Invoke(LoginFlowState.Failed);
                    session.Dispose();
                    return RouteCResult.Failure(
                        FailureCodes.E06_GoogleCaptcha,
                        FailureCodes.GetPupilMessageBm(FailureCodes.E06_GoogleCaptcha),
                        FailureCodes.GetTeacherAction(FailureCodes.E06_GoogleCaptcha),
                        null, totalChars, sw.Elapsed);

                case PostPasswordResolution.TwoFactorPrompt:
                    onStateChanged?.Invoke(LoginFlowState.Failed);
                    session.Dispose();
                    return RouteCResult.Failure(
                        FailureCodes.E07_TwoFactorPrompt,
                        FailureCodes.GetPupilMessageBm(FailureCodes.E07_TwoFactorPrompt),
                        FailureCodes.GetTeacherAction(FailureCodes.E07_TwoFactorPrompt),
                        null, totalChars, sw.Elapsed);

                case PostPasswordResolution.AccountSuspended:
                    onStateChanged?.Invoke(LoginFlowState.Failed);
                    session.Dispose();
                    return RouteCResult.Failure(
                        FailureCodes.E08_AccountSuspended,
                        FailureCodes.GetPupilMessageBm(FailureCodes.E08_AccountSuspended),
                        FailureCodes.GetTeacherAction(FailureCodes.E08_AccountSuspended),
                        null, totalChars, sw.Elapsed);

                case PostPasswordResolution.NetworkUnreachable:
                    onStateChanged?.Invoke(LoginFlowState.Failed);
                    session.Dispose();
                    return RouteCResult.Failure(
                        FailureCodes.E13_NetworkUnreachable,
                        FailureCodes.GetPupilMessageBm(FailureCodes.E13_NetworkUnreachable),
                        FailureCodes.GetTeacherAction(FailureCodes.E13_NetworkUnreachable),
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
    /// Classifies known browser error titles (CAPTCHA, 2SV, suspended account, unreachable network).
    /// </summary>
    internal static string? ClassifyKnownBrowserError(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;

        // E13: Network unreachable (English & Malay)
        if (title.StartsWith("No internet", StringComparison.OrdinalIgnoreCase) ||
            title.StartsWith("Site can't be reached", StringComparison.OrdinalIgnoreCase) ||
            title.StartsWith("Laman tidak dapat dicapai", StringComparison.OrdinalIgnoreCase) ||
            title.StartsWith("Tiada sambungan internet", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("ERR_INTERNET_DISCONNECTED", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("ERR_NAME_NOT_RESOLVED", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("ERR_CONNECTION_REFUSED", StringComparison.OrdinalIgnoreCase) ||
            title.StartsWith("Privacy error", StringComparison.OrdinalIgnoreCase) ||
            title.StartsWith("Ralat privasi", StringComparison.OrdinalIgnoreCase))
        {
            return FailureCodes.E13_NetworkUnreachable;
        }

        // E06: Google CAPTCHA / unusual activity (English & Malay)
        if (title.Contains("unusual activity", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("aktiviti luar biasa", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Captcha", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("robot", StringComparison.OrdinalIgnoreCase))
        {
            return FailureCodes.E06_GoogleCaptcha;
        }

        // E07: 2SV prompt (English & Malay)
        if (title.Contains("2-Step Verification", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Pengesahan 2 Langkah", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Verify it's you", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Sahkan diri anda", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("2-Step", StringComparison.OrdinalIgnoreCase))
        {
            return FailureCodes.E07_TwoFactorPrompt;
        }

        // E08: Account suspended / password expired (English & Malay)
        if (title.Contains("Account disabled", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Akaun dinyahdayakan", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Account suspended", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Akaun digantung", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Password expired", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Kata laluan tamat tempoh", StringComparison.OrdinalIgnoreCase))
        {
            return FailureCodes.E08_AccountSuspended;
        }

        return null;
    }

    /// <summary>
    /// Polls until the window title leaves all identifier page titles, establishing the sequence gate per §4.2.
    /// </summary>
    internal static bool WaitForTransitionOut(
        BrowserSession session,
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
                !InjectionEngine.MatchesAnyTitle(currentTitle, identifierTitles))
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
        BrowserSession session,
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
        BrowserSession session,
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

            // 3. Known browser error detected
            var knownError = ClassifyKnownBrowserError(currentTitle);
            if (knownError != null)
            {
                return MapKnownErrorToResolution(knownError);
            }

            Thread.Sleep(interval);
        }

        if (cancellationToken.IsCancellationRequested) return PostPasswordResolution.Aborted;

        // Check if final title matches a known browser error
        var finalTitle = getTitle();
        var finalKnownError = ClassifyKnownBrowserError(finalTitle);
        if (finalKnownError != null)
        {
            return MapKnownErrorToResolution(finalKnownError);
        }

        // 4. Check if still on a password-page title after timeout -> Password rejected (§7.1)
        if (IsPasswordPageTitle(finalTitle, passwordPageTitle, options))
        {
            return PostPasswordResolution.PasswordRejected;
        }

        // 5. Anything else -> Unknown state (E02)
        return PostPasswordResolution.UnknownState;
    }

    private static PostPasswordResolution MapKnownErrorToResolution(string errorCode) => errorCode switch
    {
        FailureCodes.E06_GoogleCaptcha => PostPasswordResolution.CaptchaChallenge,
        FailureCodes.E07_TwoFactorPrompt => PostPasswordResolution.TwoFactorPrompt,
        FailureCodes.E08_AccountSuspended => PostPasswordResolution.AccountSuspended,
        FailureCodes.E13_NetworkUnreachable => PostPasswordResolution.NetworkUnreachable,
        _ => PostPasswordResolution.UnknownState
    };

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

        // Not a password page if it's a known error title
        if (ClassifyKnownBrowserError(currentTitle) != null) return false;

        // Title is unchanged from the observed password page title during injection
        if (!string.IsNullOrEmpty(passwordPageTitle) && string.Equals(currentTitle, passwordPageTitle, StringComparison.Ordinal))
        {
            return true;
        }

        // Common Google password page title formats (English & Malay)
        if (currentTitle.StartsWith("Hi ", StringComparison.OrdinalIgnoreCase) ||
            currentTitle.StartsWith("Hai ", StringComparison.OrdinalIgnoreCase) ||
            currentTitle.StartsWith("Salam ", StringComparison.OrdinalIgnoreCase) ||
            currentTitle.StartsWith("Welcome", StringComparison.OrdinalIgnoreCase) ||
            currentTitle.StartsWith("Selamat datang", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}

/// <summary>
/// Possible resolution states after password injection in Route C (§4.5, §7.1).
/// </summary>
public enum PostPasswordResolution
{
    ConsentPageReached,
    DestinationReached,
    PasswordRejected,
    CaptchaChallenge,
    TwoFactorPrompt,
    AccountSuspended,
    NetworkUnreachable,
    UnknownState,
    Aborted
}
