using System.Runtime.InteropServices;

namespace Delima.Win32;

/// <summary>
/// A topmost borderless overlay window that covers the virtual screen during injection.
/// Swallows mouse clicks outside the Chrome window without stealing focus, preventing
/// background focus-stealing attacks or stray clicks while BlockInput is denied (per §4.2).
/// </summary>
public sealed class TopmostOverlay : IDisposable
{
    private const string ClassName = "Delima_Topmost_Input_Shield";
    private static readonly object ClassRegistrationLock = new();
    private static bool _classRegistered;
    private static NativeMethods.WndProc? _staticWndProc;

    private readonly Thread _thread;
    private readonly ManualResetEventSlim _createdEvent = new(false);
    private IntPtr _hWnd;
    private bool _disposed;

    public TopmostOverlay()
    {
        _thread = new Thread(OverlayThreadProc)
        {
            IsBackground = true,
            Name = "DelimaTopmostOverlayThread"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();

        // Wait up to 2 seconds for window creation
        _createdEvent.Wait(TimeSpan.FromSeconds(2));
    }

    public bool IsActive => _hWnd != IntPtr.Zero && !_disposed;

    private void OverlayThreadProc()
    {
        var hInstance = NativeMethods.GetModuleHandle(null);

        lock (ClassRegistrationLock)
        {
            if (!_classRegistered)
            {
                _staticWndProc = CustomWndProc;
                var wc = new NativeMethods.WNDCLASSEX
                {
                    cbSize = (uint)Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
                    style = 0,
                    lpfnWndProc = _staticWndProc,
                    cbClsExtra = 0,
                    cbWndExtra = 0,
                    hInstance = hInstance,
                    hIcon = IntPtr.Zero,
                    hCursor = IntPtr.Zero,
                    hbrBackground = IntPtr.Zero,
                    lpszMenuName = null,
                    lpszClassName = ClassName,
                    hIconSm = IntPtr.Zero
                };

                NativeMethods.RegisterClassEx(ref wc);
                _classRegistered = true;
            }
        }

        var vx = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        var vy = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
        var vcx = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
        var vcy = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);

        if (vcx <= 0) vcx = 1920;
        if (vcy <= 0) vcy = 1080;

        _hWnd = NativeMethods.CreateWindowEx(
            NativeMethods.WS_EX_TOPMOST | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE,
            ClassName,
            "DelimaInputShield",
            NativeMethods.WS_POPUP,
            vx,
            vy,
            vcx,
            vcy,
            IntPtr.Zero,
            IntPtr.Zero,
            hInstance,
            IntPtr.Zero);

        if (_hWnd != IntPtr.Zero)
        {
            NativeMethods.SetWindowPos(
                _hWnd,
                NativeMethods.HWND_TOPMOST,
                vx,
                vy,
                vcx,
                vcy,
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

            NativeMethods.ShowWindow(_hWnd, NativeMethods.SW_SHOWNOACTIVATE);
        }

        _createdEvent.Set();

        // Message pump
        while (NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }
    }

    private static IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case NativeMethods.WM_MOUSEACTIVATE:
                // Do not activate and discard mouse message
                return (IntPtr)NativeMethods.MA_NOACTIVATEANDEAT;

            case NativeMethods.WM_LBUTTONDOWN:
            case NativeMethods.WM_RBUTTONDOWN:
            case NativeMethods.WM_MBUTTONDOWN:
                // Swallow click
                return IntPtr.Zero;

            case NativeMethods.WM_CLOSE:
                NativeMethods.DestroyWindow(hWnd);
                return IntPtr.Zero;

            case NativeMethods.WM_DESTROY:
                NativeMethods.PostQuitMessage(0);
                return IntPtr.Zero;

            default:
                return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_hWnd != IntPtr.Zero)
        {
            NativeMethods.PostMessage(_hWnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            _thread.Join(1500);
            _hWnd = IntPtr.Zero;
        }

        _createdEvent.Dispose();
    }
}
