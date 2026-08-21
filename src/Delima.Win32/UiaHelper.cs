using System.Runtime.InteropServices;

namespace Delima.Win32;

/// <summary>
/// Provides Windows UI Automation helpers for structural field verification per Technical Architecture §4.2 and §11.1 (T0.4).
/// </summary>
public static class UiaHelper
{
    public const int UiaIsPasswordPropertyId = 30019;

    [DllImport("UIAutomationCore.dll", CharSet = CharSet.Unicode)]
    private static extern int UiaGetFocusedElement(out IntPtr hnode);

    [DllImport("UIAutomationCore.dll", CharSet = CharSet.Unicode)]
    private static extern int UiaGetPropertyValue(IntPtr hnode, int propertyId, [MarshalAs(UnmanagedType.Struct)] out object value);

    [DllImport("UIAutomationCore.dll", CharSet = CharSet.Unicode)]
    private static extern bool UiaNodeRelease(IntPtr hnode);

    /// <summary>
    /// Checks whether the currently focused element reports IsPassword == true via Windows UI Automation per §11.1 (T0.4).
    /// Serves as defence-in-depth to verify the caret is in a password field before typing credentials.
    /// </summary>
    public static bool IsFocusedElementPassword()
    {
        if (!OperatingSystem.IsWindows()) return false;

        IntPtr hnode = IntPtr.Zero;
        try
        {
            int hr = UiaGetFocusedElement(out hnode);
            if (hr != 0 || hnode == IntPtr.Zero)
            {
                return false;
            }

            hr = UiaGetPropertyValue(hnode, UiaIsPasswordPropertyId, out object value);
            if (hr == 0 && value is bool isPassword)
            {
                return isPassword;
            }

            return false;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (hnode != IntPtr.Zero)
            {
                UiaNodeRelease(hnode);
            }
        }
    }
}
