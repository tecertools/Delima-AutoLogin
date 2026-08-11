using System.Runtime.InteropServices;
using System.Text;

namespace InjectionSpike;

/// <summary>
/// P/Invoke surface for window inspection and keystroke injection.
/// </summary>
internal static class NativeMethods
{
    // ---------- Window inspection ----------

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool BlockInput([MarshalAs(UnmanagedType.Bool)] bool fBlockIt);

    internal static string GetForegroundClassName()
    {
        var hWnd = GetForegroundWindow();
        if (hWnd == IntPtr.Zero) return string.Empty;
        var sb = new StringBuilder(256);
        return GetClassName(hWnd, sb, sb.Capacity) == 0 ? string.Empty : sb.ToString();
    }

    internal static string GetForegroundTitle()
    {
        var hWnd = GetForegroundWindow();
        return hWnd == IntPtr.Zero ? string.Empty : GetTitle(hWnd);
    }

    internal static string GetTitle(IntPtr hWnd)
    {
        var sb = new StringBuilder(1024);
        return GetWindowText(hWnd, sb, sb.Capacity) == 0 ? string.Empty : sb.ToString();
    }

    internal static uint GetForegroundProcessId()
    {
        var hWnd = GetForegroundWindow();
        if (hWnd == IntPtr.Zero) return 0;
        GetWindowThreadProcessId(hWnd, out var pid);
        return pid;
    }

    // ---------- SendInput ----------

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    /// <summary>
    /// Injects a string using KEYEVENTF_UNICODE scan codes. Because each UTF-16
    /// code unit is sent directly, there is no virtual-key mapping and therefore
    /// no reserved-character problem: '+', '^', '%', '~', '(', ')', '{', '}',
    /// '[' and ']' are delivered literally. This is the behaviour the spike is
    /// built to prove against SendKeys.
    /// </summary>
    internal static bool SendUnicodeString(string text, int perCharDelayMs = 0)
    {
        foreach (var ch in text)
        {
            if (!SendUnicodeChar(ch)) return false;
            if (perCharDelayMs > 0) Thread.Sleep(perCharDelayMs);
        }
        return true;
    }

    private static bool SendUnicodeChar(char ch)
    {
        var inputs = new INPUT[2];

        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].U.ki = new KEYBDINPUT
        {
            wVk = 0,
            wScan = ch,
            dwFlags = KEYEVENTF_UNICODE,
            time = 0,
            dwExtraInfo = IntPtr.Zero
        };

        inputs[1].type = INPUT_KEYBOARD;
        inputs[1].U.ki = new KEYBDINPUT
        {
            wVk = 0,
            wScan = ch,
            dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
            time = 0,
            dwExtraInfo = IntPtr.Zero
        };

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        return sent == inputs.Length;
    }

    private const ushort VK_RETURN = 0x0D;

    internal static bool SendEnter()
    {
        var inputs = new INPUT[2];

        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].U.ki = new KEYBDINPUT { wVk = VK_RETURN, wScan = 0, dwFlags = 0, time = 0, dwExtraInfo = IntPtr.Zero };

        inputs[1].type = INPUT_KEYBOARD;
        inputs[1].U.ki = new KEYBDINPUT { wVk = VK_RETURN, wScan = 0, dwFlags = KEYEVENTF_KEYUP, time = 0, dwExtraInfo = IntPtr.Zero };

        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>()) == inputs.Length;
    }
}
