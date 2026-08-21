using System.Windows;
using Delima.Core.Roster;
using Delima.Win32;

namespace Delima.Launcher.Views;

public partial class FloatingResetBarWindow : Window
{
    private readonly Student _student;
    private readonly ChromeSession? _session;
    private readonly Action _onReset;
    private readonly int _idleResetSeconds;
    private SessionWatchdog? _watchdog;

    public FloatingResetBarWindow(
        Student student,
        ChromeSession? session,
        Action onReset,
        int idleResetSeconds = 600)
    {
        InitializeComponent();
        _student = student;
        _session = session;
        _onReset = onReset;
        _idleResetSeconds = idleResetSeconds > 0 ? idleResetSeconds : 600;

        PupilNameText.Text = student.DisplayName;

        Loaded += (_, _) =>
        {
            // Position at top center of screen with 16px margin per PRD §7.4
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            Left = (screenWidth - Width) / 2;
            Top = 16;

            // Start idle reset watchdog per Architecture §9 and Appendix B
            _watchdog = new SessionWatchdog(
                idleThreshold: TimeSpan.FromSeconds(_idleResetSeconds),
                session: _session,
                student: _student,
                credential: null,
                onResetAction: () =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        Close();
                        _onReset();
                    });
                }
            );
        };

        Closed += (_, _) =>
        {
            _watchdog?.Dispose();
            _watchdog = null;
        };
    }

    private void OnLogoutClicked(object sender, RoutedEventArgs e)
    {
        _watchdog?.Dispose();
        _watchdog = null;

        try
        {
            _session?.Dispose();
        }
        catch
        {
            // Best effort session cleanup
        }

        Close();
        _onReset();
    }
}
