using System.Diagnostics;
using Delima.Core.Store;

namespace Delima.Win32;

/// <summary>
/// Configuration options for the injection engine pipeline.
/// </summary>
public sealed record InjectionOptions
{
    /// <summary>
    /// Timeout when waiting for the Chrome window to appear and verify.
    /// Default is 30,000 ms (30 seconds) per Technical Architecture §3.2.
    /// </summary>
    public TimeSpan WindowWaitTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Delay after window verification before initiating injection.
    /// Default is 400 ms per Technical Architecture §3.2.
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
}

/// <summary>
/// The visual keystroke injection engine with per-keystroke window verification,
/// dual-layer input shielding (TopmostOverlay + BlockInput), and strict cancellation/abort paths.
/// </summary>
public static class InjectionEngine
{
    /// <summary>
    /// Injects credentials from an ICredential into the specified ChromeSession.
    /// Conforms to §3.4 decryption discipline: operates directly on ReadOnlySpan&lt;char&gt;
    /// without materialising a System.String.
    /// </summary>
    public static InjectionResult Inject(
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
    /// Injects characters directly from a ReadOnlySpan&lt;char&gt;.
    /// </summary>
    public static InjectionResult Inject(
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

        // 2. Window Verification Wait
        var windowVerified = WaitForVerifiedWindow(
            session,
            titlePredicate,
            options.ExpectedClassName,
            options.WindowWaitTimeout,
            cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            return InjectionResult.Aborted(0, sw.Elapsed);
        }

        if (!windowVerified)
        {
            return InjectionResult.WindowTimeout(sw.Elapsed);
        }

        // 3. Settle Delay
        if (options.InjectionSettleMs > 0)
        {
            var settleSw = Stopwatch.StartNew();
            while (settleSw.ElapsedMilliseconds < options.InjectionSettleMs)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return InjectionResult.Aborted(0, sw.Elapsed);
                }
                Thread.Sleep(20);
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

        // 6. Optional Enter key
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
    /// has the expected window class, and satisfies the title predicate.
    /// </summary>
    public static bool IsWindowVerified(
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

    private static bool WaitForVerifiedWindow(
        ChromeSession session,
        Func<string, bool> titlePredicate,
        string expectedClassName,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        int pollMs = 100)
    {
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < timeout)
        {
            if (cancellationToken.IsCancellationRequested) return false;

            if (IsWindowVerified(session, titlePredicate, expectedClassName))
            {
                return true;
            }

            Thread.Sleep(pollMs);
        }

        return false;
    }
}
