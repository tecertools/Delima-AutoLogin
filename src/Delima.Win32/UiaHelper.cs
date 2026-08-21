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
}
