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
    private Timer? _titleWatcherTimer;

    public FloatingResetBarWindow(
        Student student,
        ChromeSession? session,
        Action onReset,
        int idleResetSeconds = 600,
        string? initialPrompt = "Lihat nama kamu. Kalau betul, tekan butang biru di bawah.")
    {
        InitializeComponent();
        _student = student;
        _session = session;
        _onReset = onReset;
        _idleResetSeconds = idleResetSeconds > 0 ? idleResetSeconds : 600;

        PupilNameText.Text = student.DisplayName;
        SetPromptMessage(initialPrompt);

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

            // Watch for destination page load in Chrome to clear the consent prompt
            if (_session != null && !string.IsNullOrEmpty(initialPrompt))
            {
                _titleWatcherTimer = new Timer(CheckWindowTitle, null, 1000, 500);
            }
        };

        Closed += (_, _) =>
        {
            _watchdog?.Dispose();
            _watchdog = null;
            _titleWatcherTimer?.Dispose();
            _titleWatcherTimer = null;
        };
    }

    /// <summary>
    /// Sets or clears the prompt message line on the floating reset bar (§4.5, PRD §7.4).
    /// </summary>
    public void SetPromptMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            ConsentPromptBorder.Visibility = Visibility.Collapsed;
        }
        else
        {
            ConsentPromptText.Text = message;
            ConsentPromptBorder.Visibility = Visibility.Visible;
        }
    }

    private void CheckWindowTitle(object? state)
    {
        try
        {
            if (_session == null || _session.Process.HasExited) return;

            var title = NativeMethods.GetForegroundTitle();
            if (string.IsNullOrEmpty(title)) return;

            // If the window has transitioned out of Google Sign-in / Consent page into DELIMa / destination
            if (title.Contains("DELIMa", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("Classroom", StringComparison.OrdinalIgnoreCase) ||
                (!title.Contains("Sign in", StringComparison.OrdinalIgnoreCase) &&
                 !title.Contains("Google Accounts", StringComparison.OrdinalIgnoreCase) &&
                 !title.Contains("Welcome", StringComparison.OrdinalIgnoreCase)))
            {
                Dispatcher.Invoke(() => SetPromptMessage(null));
                _titleWatcherTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }
        catch
        {
            // Non-critical background observation
        }
    }

    private void OnLogoutClicked(object sender, RoutedEventArgs e)
    {
        _titleWatcherTimer?.Dispose();
        _titleWatcherTimer = null;

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
