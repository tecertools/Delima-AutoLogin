using System.Windows.Automation;

namespace Delima.Win32;

/// <summary>
/// Provides Windows UI Automation helpers for structural field verification per Technical Architecture §4.2 and §11.1 (T0.4).
/// Uses System.Windows.Automation.AutomationElement.FocusedElement per §4.2.
/// </summary>
public static class UiaHelper
{
    /// <summary>
    /// Checks whether the currently focused element reports IsPassword == true via Windows UI Automation per §11.1 (T0.4).
    /// Serves as defence-in-depth to verify the caret is in a password field before typing credentials.
    /// </summary>
    public static bool IsFocusedElementPassword()
    {
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            var element = AutomationElement.FocusedElement;
            return IsElementPassword(element);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks whether the specified AutomationElement reports IsPassword == true per §11.1 (T0.4).
    /// </summary>
    public static bool IsElementPassword(AutomationElement? element)
    {
        if (!OperatingSystem.IsWindows() || element == null) return false;

        try
        {
            return element.Current.IsPassword;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Probes the currently focused element for UI Automation inspection (T0.4).
    /// Returns (true, isPassword) if the focused element is resolved and IsPassword is read successfully.
    /// Returns (false, null) if focus is unresolvable or property reading throws an exception.
    /// </summary>
    public static (bool FocusResolvable, bool? IsPassword) ProbeFocusedElementPassword()
    {
        if (!OperatingSystem.IsWindows()) return (false, null);

        try
        {
            var element = AutomationElement.FocusedElement;
            if (element == null) return (false, null);

            var isPassword = element.Current.IsPassword;
            return (true, isPassword);
        }
        catch
        {
            return (false, null);
        }
    }

    private static readonly string[] RejectedButtonKeywords =
    [
        "install",
        "pasang",
        "app available",
        "muat turun",
        "download",
        "translate",
        "terjemah",
        "close",
        "tutup",
        "batal",
        "cancel",
        "dismiss",
        "abaikan",
        "settings",
        "tetapan",
        "search",
        "cari",
        "extensions",
        "sambungan",
        "reload",
        "muat semula",
        "back",
        "kembali"
    ];

    /// <summary>
    /// Checks whether an element accessible name indicates a login action while filtering out browser toolbar / install buttons.
    /// </summary>
    internal static bool IsLoginButton(string? name, string? buttonText)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;

        var lower = name.ToLowerInvariant();
        foreach (var rejected in RejectedButtonKeywords)
        {
            if (lower.Contains(rejected)) return false;
        }

        if (!string.IsNullOrWhiteSpace(buttonText) &&
            name.Contains(buttonText, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (name.Contains("Log Masuk ke DELIMa", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Log Masuk dengan akaun DELIMa", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Log Masuk", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Log masuk", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Log In", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Login", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Masuk ke", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Continue with Google", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Log masuk dengan Google", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Teruskan dengan Google", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Sign in with Google", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name.Trim(), "Masuk", StringComparison.OrdinalIgnoreCase) ||
            (name.Contains("Sign in", StringComparison.OrdinalIgnoreCase) && !name.Contains("Google", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines whether the given title indicates that navigation to Google Sign-in has already occurred.
    /// </summary>
    internal static bool IsGoogleSignInTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;
        var norm = InjectionEngine.NormalizeTitle(title);
        return norm.Contains("Sign in", StringComparison.OrdinalIgnoreCase) ||
               norm.Contains("Google Accounts", StringComparison.OrdinalIgnoreCase) ||
               norm.Contains("Log masuk", StringComparison.OrdinalIgnoreCase) ||
               norm.Contains("Akaun Google", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Attempts to find and click a login button by accessible name (e.g., "Log Masuk ke DELIMa") in the foreground window using UI Automation.
    /// Restricts search to web Document to avoid browser toolbar buttons and retries across client-side JS hydration delays.
    /// </summary>
    public static bool TryInvokeButtonInForeground(
        string buttonText,
        TimeSpan timeout,
        BrowserSession? session = null,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows()) return false;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout && !cancellationToken.IsCancellationRequested)
        {
            if (session != null && session.Process.HasExited)
            {
                return false;
            }

            try
            {
                var hwnd = NativeMethods.GetForegroundWindow();
                if (hwnd != IntPtr.Zero)
                {
                    if (session != null)
                    {
                        var fgPid = NativeMethods.GetForegroundProcessId();
                        if (fgPid != (uint)session.Process.Id)
                        {
                            var browserHwnd = NativeMethods.FindWindowForProcess(session.Process.Id);
                            if (browserHwnd != IntPtr.Zero)
                            {
                                NativeMethods.SetForegroundWindow(browserHwnd);
                            }
                            else if (session.Process.MainWindowHandle != IntPtr.Zero)
                            {
                                NativeMethods.SetForegroundWindow(session.Process.MainWindowHandle);
                            }
                        }
                    }

                    var title = NativeMethods.GetForegroundTitle();
                    // Don't search if window has already transitioned to Google Sign-in or beyond
                    if (IsGoogleSignInTitle(title))
                    {
                        return true;
                    }

                    var root = AutomationElement.FromHandle(hwnd);
                    if (root != null)
                    {
                        // Prefer searching within the web Document element so browser chrome/toolbar controls are excluded
                        var docCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document);
                        var doc = root.FindFirst(TreeScope.Descendants, docCondition);
                        var searchContainer = doc ?? root;

                        var condition = new OrCondition(
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink),
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text),
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Group),
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Custom));
                        var buttons = searchContainer.FindAll(TreeScope.Descendants, condition);

                        foreach (AutomationElement btn in buttons)
                        {
                            if (cancellationToken.IsCancellationRequested) return false;

                            try
                            {
                                var name = btn.Current.Name;
                                if (IsLoginButton(name, buttonText))
                                {
                                    bool invoked = false;
                                    if (btn.TryGetCurrentPattern(InvokePattern.Pattern, out var patternObj) &&
                                        patternObj is InvokePattern invokePattern)
                                    {
                                        invokePattern.Invoke();
                                        invoked = true;
                                    }

                                    if (!invoked)
                                    {
                                        btn.SetFocus();
                                        Thread.Sleep(50);
                                        NativeMethods.SendEnter();
                                    }

                                    // Wait up to 2.5 seconds checking if navigation triggered
                                    var waitNavSw = System.Diagnostics.Stopwatch.StartNew();
                                    while (waitNavSw.Elapsed < TimeSpan.FromMilliseconds(2500) && !cancellationToken.IsCancellationRequested)
                                    {
                                        Thread.Sleep(100);
                                        var currentTitle = NativeMethods.GetForegroundTitle();
                                        if (IsGoogleSignInTitle(currentTitle))
                                        {
                                            return true;
                                        }
                                    }

                                    // If navigation hasn't completed yet, continue outer poll to retry
                                    break;
                                }
                            }
                            catch
                            {
                                // Skip individual element errors
                            }
                        }
                    }
                }
            }
            catch
            {
                // Transient COM exception while page is rendering
            }

            try
            {
                Thread.Sleep(250);
            }
            catch
            {
                break;
            }
        }

        return false;
    }

    /// <summary>
    /// Backward-compatible overload without BrowserSession argument.
    /// </summary>
    public static bool TryInvokeButtonInForeground(string buttonText, TimeSpan timeout, CancellationToken cancellationToken) =>
        TryInvokeButtonInForeground(buttonText, timeout, session: null, cancellationToken);
}
