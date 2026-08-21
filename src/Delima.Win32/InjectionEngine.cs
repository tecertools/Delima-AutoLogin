using System.Diagnostics;
using Delima.Core.Store;

namespace Delima.Win32;

/// <summary>
/// Configuration options for the injection engine pipeline per Technical Architecture §3.2, §4.2, and Appendix B.
/// </summary>
public sealed record InjectionOptions
{
    /// <summary>
    /// Timeout when waiting for the Chrome window to appear, verify, and settle.
    /// Default is 30,000 ms (30 seconds) per Technical Architecture §3.2 and Appendix B.
    /// </summary>
    public TimeSpan WindowWaitTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Consecutive 100 ms polls a title must hold stably before initiating injection per §4.2 and Appendix B.
    /// Default is 3 polls (300 ms continuous stability).
    /// </summary>
    public int TitleSettlePolls { get; init; } = 3;

    /// <summary>
    /// Polling interval in milliseconds during window wait and settle.
    /// Default is 100 ms per §4.2.
    /// </summary>
    public int PollIntervalMs { get; init; } = 100;

    /// <summary>
    /// Legacy settle delay duration in milliseconds.
    /// Maintained for configuration compatibility.
    /// </summary>
    public int InjectionSettleMs { get; init; } = 400;

    /// <summary>
    /// Gap between individual keystrokes in milliseconds.
    /// Default is 0 ms per T0.3 hardware findings.
    /// </summary>
    public int PerCharDelayMs { get; init; } = 0;

    /// <summary>
    /// Whether to send Enter key after password characters.
    /// Default is false per §4.2 ("Never send {ENTER} blind").
    /// </summary>
    public bool SendEnter { get; init; } = false;

    /// <summary>
    /// Expected Chrome window class name.
    /// Default is "Chrome_WidgetWin_1".
    /// </summary>
    public string ExpectedClassName { get; init; } = "Chrome_WidgetWin_1";

    /// <summary>
    /// Optional validation check executed immediately after window title settle,
    /// before shield engagement and keystroke injection (e.g. UIA IsPassword validation for T0.4).
    /// If it returns false, injection aborts immediately with E02 and zero keystrokes.
    /// </summary>
    public Func<bool>? PreInjectionCheck { get; init; }
}

/// <summary>
/// The visual keystroke injection engine with per-keystroke window verification,
/// exact full-string title matching (§4.2), multi-poll settle stability (§4.2),
/// dual-layer input shielding (TopmostOverlay + BlockInput), and strict cancellation/abort paths.
/// </summary>
public static class InjectionEngine
{
    /// <summary>
    /// Injects credentials from an ICredential into the specified ChromeSession.
    /// Conforms to §3.4 decryption discipline: operates directly on ReadOnlySpan&lt;char&gt;
    /// without materialising a System.String.
    /// Requires an exact full-string match against expectedTitle per §4.2.
    /// </summary>
    public static InjectionResult Inject(
        ChromeSession session,
        ICredential credential,
        string expectedTitle,
        InjectionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedTitle);

        return Inject(session, credential.PasswordSpan, expectedTitle, options, cancellationToken);
    }

    /// <summary>
    /// Injects characters directly from a ReadOnlySpan&lt;char&gt;.
    /// Requires an exact full-string match against expectedTitle per §4.2.
    /// </summary>
    public static InjectionResult Inject(
        ChromeSession session,
        ReadOnlySpan<char> passwordSpan,
        string expectedTitle,
        InjectionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedTitle);

        return Inject(
            session,
            passwordSpan,
            title => string.Equals(title, expectedTitle, StringComparison.Ordinal),
            options,
            cancellationToken);
    }

    /// <summary>
    /// Internal injection overload accepting a title predicate for testing dynamic title transitions.
    /// Kept internal to prevent public callers from using loose substring checks per §4.2.
    /// </summary>
    internal static InjectionResult Inject(
        ChromeSession session,
        ICredential credential,
        Func<string, bool> titlePredicate,
        InjectionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(titlePredicate);

        return Inject(session, credential.PasswordSpan, titlePredicate, options, cancellationToken);
    }

    /// <summary>
    /// Internal injection overload accepting a title predicate for testing dynamic title transitions.
    /// </summary>
    internal static InjectionResult Inject(
        ChromeSession session,
        ReadOnlySpan<char> passwordSpan,
        Func<string, bool> titlePredicate,
        InjectionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(titlePredicate);

        options ??= new InjectionOptions();
        var sw = Stopwatch.StartNew();

        // 1. Pre-check: Immediate cancellation
        if (cancellationToken.IsCancellationRequested)
        {
            return InjectionResult.Aborted(0, sw.Elapsed);
        }

        // 2. Window Verification & Settle Polling (≥ 3 consecutive 100 ms polls per §4.2)
        var windowVerified = WaitForVerifiedAndSettledWindow(
            session,
            titlePredicate,
            options.ExpectedClassName,
            options.WindowWaitTimeout,
            options.TitleSettlePolls,
            options.PollIntervalMs,
            cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            return InjectionResult.Aborted(0, sw.Elapsed);
        }

        if (!windowVerified)
        {
            return InjectionResult.WindowTimeout(sw.Elapsed);
        }

        // 3. Pre-injection field check (e.g. UIA IsPassword validation for T0.4)
        if (options.PreInjectionCheck != null)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return InjectionResult.Aborted(0, sw.Elapsed);
            }

            if (!options.PreInjectionCheck())
            {
                return InjectionResult.Failure(
                    FailureCodes.E02_WindowNotVerified,
                    charsInjected: 0,
                    blockInputGranted: false,
                    elapsed: sw.Elapsed);
            }
        }

        // 4. Engage Injection Shield (TopmostOverlay + BlockInput)
        using var shield = KioskGuard.EngageInjectionShield();
        var blockInputGranted = shield.BlockInputGranted;

        // 5. Per-Keystroke Verification & Injection
        var charsInjected = 0;

        for (var i = 0; i < passwordSpan.Length; i++)
        {
            // Abort check before each keystroke
            if (cancellationToken.IsCancellationRequested)
            {
                return InjectionResult.Aborted(charsInjected, sw.Elapsed);
            }

            // Strict Window Verification BEFORE every keystroke
            if (!IsWindowVerified(session, titlePredicate, options.ExpectedClassName))
            {
                // Verification failed: zero further keystrokes sent
                return InjectionResult.WindowLost(charsInjected, sw.Elapsed);
            }

            // Send Unicode character via KEYEVENTF_UNICODE
            var ch = passwordSpan[i];
            if (!NativeMethods.SendUnicodeChar(ch))
            {
                return InjectionResult.Failure(
                    FailureCodes.E02_WindowNotVerified,
                    charsInjected,
                    blockInputGranted,
                    sw.Elapsed);
            }

            charsInjected++;

            if (options.PerCharDelayMs > 0 && i < passwordSpan.Length - 1)
            {
                Thread.Sleep(options.PerCharDelayMs);
            }
        }

        // 5. Optional Enter key
        if (options.SendEnter)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return InjectionResult.Aborted(charsInjected, sw.Elapsed);
            }

            if (!IsWindowVerified(session, titlePredicate, options.ExpectedClassName))
            {
                return InjectionResult.WindowLost(charsInjected, sw.Elapsed);
            }

            NativeMethods.SendEnter();
        }

        sw.Stop();
        return InjectionResult.Succeeded(charsInjected, blockInputGranted, sw.Elapsed);
    }

    /// <summary>
    /// Checks whether the current foreground window belongs to the expected Chrome process,
    /// has the expected window class, and exactly matches the expected title per §4.2.
    /// </summary>
    public static bool IsWindowVerified(
        ChromeSession session,
        string expectedTitle,
        string expectedClassName = "Chrome_WidgetWin_1")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedTitle);
        return IsWindowVerified(session, title => string.Equals(title, expectedTitle, StringComparison.Ordinal), expectedClassName);
    }

    /// <summary>
    /// Internal verification method accepting a title predicate for testing.
    /// </summary>
    internal static bool IsWindowVerified(
        ChromeSession session,
        Func<string, bool> titlePredicate,
        string expectedClassName = "Chrome_WidgetWin_1")
    {
        var foregroundHwnd = NativeMethods.GetForegroundWindow();
        if (foregroundHwnd == IntPtr.Zero) return false;

        var pid = NativeMethods.GetForegroundProcessId();
        if (session.Process.HasExited || pid != (uint)session.Process.Id)
        {
            return false;
        }

        var className = NativeMethods.GetForegroundClassName();
        if (!string.Equals(className, expectedClassName, StringComparison.Ordinal))
        {
            return false;
        }

        var title = NativeMethods.GetForegroundTitle();
        return titlePredicate(title);
    }

    /// <summary>
    /// Polling loop requiring consecutive identical matches across settlePolls intervals per §4.2.
    /// Aborts to false if the required stability is not reached within timeout.
    /// </summary>
    internal static bool WaitForVerifiedAndSettledWindow(
        ChromeSession session,
        Func<string, bool> titlePredicate,
        string expectedClassName,
        TimeSpan timeout,
        int settlePolls,
        int pollIntervalMs,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        int consecutiveMatches = 0;
        int requiredPolls = Math.Max(1, settlePolls);
        int interval = Math.Max(10, pollIntervalMs);

        while (sw.Elapsed < timeout)
        {
            if (cancellationToken.IsCancellationRequested) return false;

            if (IsWindowVerified(session, titlePredicate, expectedClassName))
            {
                consecutiveMatches++;
                if (consecutiveMatches >= requiredPolls)
                {
                    return true;
                }
            }
            else
            {
                consecutiveMatches = 0;
            }

            Thread.Sleep(interval);
        }

        return false;
    }
}
