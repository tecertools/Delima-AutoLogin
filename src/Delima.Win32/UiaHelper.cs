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

    /// <summary>
    /// Attempts to find and click a button by accessible name (e.g., "Log Masuk ke DELIMa") in the foreground window using UI Automation.
    /// Returns true if button was found and invoked; false otherwise.
    /// </summary>
    public static bool TryInvokeButtonInForeground(string buttonText, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows()) return false;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var hwnd = NativeMethods.GetForegroundWindow();
                if (hwnd != IntPtr.Zero)
                {
                    var title = NativeMethods.GetForegroundTitle();
                    // Don't search if window has already transitioned to Google Sign-in or beyond
                    if (title.Contains("Sign in", StringComparison.OrdinalIgnoreCase) ||
                        title.Contains("Google Accounts", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    var root = AutomationElement.FromHandle(hwnd);
                    if (root != null)
                    {
                        var condition = new OrCondition(
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink));
                        var buttons = root.FindAll(TreeScope.Descendants, condition);

                        foreach (AutomationElement btn in buttons)
                        {
                            try
                            {
                                var name = btn.Current.Name;
                                if (!string.IsNullOrEmpty(name) &&
                                    (name.Contains(buttonText, StringComparison.OrdinalIgnoreCase) ||
                                     name.Contains("Log Masuk", StringComparison.OrdinalIgnoreCase) ||
                                     name.Contains("DELIMa", StringComparison.OrdinalIgnoreCase)))
                                {
                                    if (btn.TryGetCurrentPattern(InvokePattern.Pattern, out var patternObj) &&
                                        patternObj is InvokePattern invokePattern)
                                    {
                                        invokePattern.Invoke();
                                        return true;
                                    }

                                    btn.SetFocus();
                                    Thread.Sleep(50);
                                    NativeMethods.SendEnter();
                                    return true;
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
                Thread.Sleep(200);
            }
            catch
            {
                break;
            }
        }

        return false;
    }
}
