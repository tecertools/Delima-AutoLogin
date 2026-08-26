using System.ComponentModel;
using System.Windows;
using Delima.Win32;
using Wpf.Ui.Controls;

namespace Delima.Launcher;

/// <summary>
/// Interaction logic for MainWindow.xaml with kiosk hardening per Architecture §9 and PRD §8.3.
/// </summary>
public partial class MainWindow : FluentWindow
{
    private readonly bool _isKiosk;
    private bool _allowClose = false;
    private KioskGuard? _kioskGuard;

    public bool IsKiosk => _isKiosk;

    public MainWindow(bool isKiosk = false)
    {
        _isKiosk = isKiosk;
        InitializeComponent();

        if (_isKiosk)
        {
            // Kiosk mode is fullscreen/borderless, so the window backdrop is never visible —
            // skip it entirely rather than mix Mica/Acrylic composition with the kiosk hardening path.
            WindowBackdropType = WindowBackdropType.None;
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            ResizeMode = ResizeMode.NoResize;
            Topmost = false;

            if (AppTitleBar != null)
            {
                AppTitleBar.Visibility = Visibility.Collapsed;
            }

            try
            {
                _kioskGuard = new KioskGuard();
                _kioskGuard.EnableKioskHook();
            }
            catch
            {
                // Suppress hook failures in test or non-elevated environments
            }
        }
        else
        {
            if (AppTitleBar != null)
            {
                AppTitleBar.Visibility = Visibility.Visible;
            }
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // In kiosk mode, the launcher must never exit on a pupil action at all (PRD §8.3 & Arch §9)
        if (_isKiosk && !_allowClose)
        {
            e.Cancel = true;
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _kioskGuard?.Dispose();
        _kioskGuard = null;
        base.OnClosed(e);
    }

    /// <summary>
    /// Explicitly allows the application window to close (for authorized teacher/admin exit).
    /// </summary>
    public void ForceClose()
    {
        _allowClose = true;
        Close();
    }
}
