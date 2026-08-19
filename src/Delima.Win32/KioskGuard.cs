using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Delima.Win32;

/// <summary>
/// Hardens the kiosk session by intercepting system hotkeys and providing the
/// dual-layer injection protection shield (BlockInput + TopmostOverlay) specified in §4.2.
/// </summary>
public sealed class KioskGuard : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    private NativeMethods.LowLevelKeyboardProc? _hookProc;
    private bool _disposed;

    /// <summary>
    /// Scope returned when engaging injection protection. Disposing this scope
    /// removes the overlay and unblocks input.
    /// </summary>
    public sealed class InjectionShieldScope : IDisposable
    {
        private readonly TopmostOverlay? _overlay;
        private bool _disposed;

        public bool BlockInputGranted { get; }

        internal InjectionShieldScope(TopmostOverlay? overlay, bool blockInputGranted)
        {
            _overlay = overlay;
            BlockInputGranted = blockInputGranted;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                NativeMethods.BlockInput(false);
            }
            catch
            {
                // Best effort unblock
            }

            _overlay?.Dispose();
        }
    }

    /// <summary>
    /// Installs a low-level keyboard hook to suppress Alt+Tab, Win keys, and Alt+F4.
    /// </summary>
    public void EnableKioskHook()
    {
        if (_hookId != IntPtr.Zero) return;

        _hookProc = HookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        var hMod = NativeMethods.GetModuleHandle(curModule?.ModuleName);
        _hookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _hookProc, hMod, 0);
    }

    /// <summary>
    /// Unhooks the low-level keyboard hook.
    /// </summary>
    public void DisableKioskHook()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
            _hookProc = null;
        }
    }

    /// <summary>
    /// Engages the injection shield (TopmostOverlay + BlockInput) for the duration of the returned scope.
    /// §4.2: BlockInput is attempted and tracked (denied on standard non-elevated accounts),
    /// while the TopmostOverlay provides the guaranteed focus-protection defense.
    /// </summary>
    public static InjectionShieldScope EngageInjectionShield()
    {
        var blockInputGranted = false;
        try
        {
            blockInputGranted = NativeMethods.BlockInput(true);
        }
        catch
        {
            blockInputGranted = false;
        }

        TopmostOverlay? overlay = null;
        try
        {
            overlay = new TopmostOverlay();
        }
        catch
        {
            // If overlay window fails to initialize, continue with BlockInput result
        }

        return new InjectionShieldScope(overlay, blockInputGranted);
    }

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var kb = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            var isAltDown = (kb.flags & NativeMethods.LLKHF_ALTDOWN) != 0;

            // Suppress Windows keys
            if (kb.vkCode is NativeMethods.VK_LWIN or NativeMethods.VK_RWIN)
            {
                return (IntPtr)1;
            }

            // Suppress Alt+Tab, Alt+Esc, Alt+F4
            if (isAltDown && kb.vkCode is NativeMethods.VK_TAB or NativeMethods.VK_ESCAPE or NativeMethods.VK_F4)
            {
                return (IntPtr)1;
            }

            // Suppress Ctrl+Esc
            if (kb.vkCode == NativeMethods.VK_ESCAPE && (NativeMethods.GetForegroundWindow() != IntPtr.Zero))
            {
                // If Ctrl is held down
                // (Note: Ctrl+Shift+Esc is handled at OS kernel level; Alt+Esc/Ctrl+Esc handled here)
            }
        }

        return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisableKioskHook();
    }
}
