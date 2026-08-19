using System.Runtime.InteropServices;
using System.Text;

namespace Delima.Win32;

/// <summary>
/// P/Invoke surface for window inspection, keystroke injection, input blocking,
/// and low-level kiosk protections.
/// Promoted from InjectionSpike/NativeMethods.cs (T0.3 validated 100/100).
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

    // ---------- Stale-window cleanup ----------

    internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    internal const uint WM_CLOSE = 0x0010;
    internal const uint WM_DESTROY = 0x0002;
    internal const uint WM_MOUSEACTIVATE = 0x0021;
    internal const int MA_NOACTIVATEANDEAT = 3;

    internal const uint WM_LBUTTONDOWN = 0x0201;
    internal const uint WM_RBUTTONDOWN = 0x0204;
    internal const uint WM_MBUTTONDOWN = 0x0207;

    /// <summary>
    /// Closes every visible top-level window whose title starts with
    /// <paramref name="titlePrefix"/>. Call once before a test or session batch starts,
    /// so a prior interrupted run cannot be mistaken for the current one.
    /// Returns how many windows were asked to close.
    /// </summary>
    internal static int CloseWindowsWithTitlePrefix(string titlePrefix)
    {
        var closed = 0;
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd)) return true;
            var title = GetTitle(hWnd);
            if (title.StartsWith(titlePrefix, StringComparison.Ordinal))
            {
                PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                closed++;
            }
            return true; // keep enumerating
        }, IntPtr.Zero);
        return closed;
    }

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

    internal const uint INPUT_KEYBOARD = 1;
    internal const uint KEYEVENTF_KEYUP = 0x0002;
    internal const uint KEYEVENTF_UNICODE = 0x0004;

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    /// <summary>
    /// Injects a string using KEYEVENTF_UNICODE scan codes. Because each UTF-16
    /// code unit is sent directly, there is no virtual-key mapping and therefore
    /// no reserved-character problem: '+', '^', '%', '~', '(', ')', '{', '}',
    /// '[' and ']' are delivered literally. Proven at 100/100 in T0.3.
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

    /// <summary>
    /// Sends a single UTF-16 code unit as a key-down followed immediately by key-up.
    /// </summary>
    internal static bool SendUnicodeChar(char ch)
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

    // ---------- Topmost Overlay & Message Loop APIs ----------

    internal const int SM_XVIRTUALSCREEN = 76;
    internal const int SM_YVIRTUALSCREEN = 77;
    internal const int SM_CXVIRTUALSCREEN = 78;
    internal const int SM_CYVIRTUALSCREEN = 79;

    [DllImport("user32.dll")]
    internal static extern int GetSystemMetrics(int nIndex);

    internal delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public WndProc lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern ushort RegisterClassEx([In] ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr CreateWindowEx(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    internal static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    internal static extern void PostQuitMessage(int nExitCode);

    [StructLayout(LayoutKind.Sequential)]
    internal struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int pt_x;
        public int pt_y;
    }

    [DllImport("user32.dll")]
    internal static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    internal static extern bool TranslateMessage([In] ref MSG lpMsg);

    [DllImport("user32.dll")]
    internal static extern IntPtr DispatchMessage([In] ref MSG lpMsg);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr GetModuleHandle(string? lpModuleName);

    internal const uint WS_POPUP = 0x80000000;
    internal const uint WS_EX_TOPMOST = 0x00000008;
    internal const uint WS_EX_TOOLWINDOW = 0x00000080;
    internal const uint WS_EX_NOACTIVATE = 0x08000000;

    internal const int SW_HIDE = 0;
    internal const int SW_SHOWNOACTIVATE = 4;
    internal const int SW_SHOW = 5;

    internal static readonly IntPtr HWND_TOPMOST = new(-1);
    internal const uint SWP_NOSIZE = 0x0001;
    internal const uint SWP_NOMOVE = 0x0002;
    internal const uint SWP_NOACTIVATE = 0x0010;
    internal const uint SWP_SHOWWINDOW = 0x0040;

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    // ---------- Low-level Keyboard Hook (Kiosk Guard) ----------

    internal delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    internal static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    internal const int WH_KEYBOARD_LL = 13;
    internal const uint WM_KEYDOWN = 0x0100;
    internal const uint WM_KEYUP = 0x0101;
    internal const uint WM_SYSKEYDOWN = 0x0104;
    internal const uint WM_SYSKEYUP = 0x0105;

    [StructLayout(LayoutKind.Sequential)]
    internal struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    internal const uint LLKHF_ALTDOWN = 0x20;
    internal const uint VK_TAB = 0x09;
    internal const uint VK_ESCAPE = 0x1B;
    internal const uint VK_LWIN = 0x5B;
    internal const uint VK_RWIN = 0x5C;
    internal const uint VK_F4 = 0x73;
}
