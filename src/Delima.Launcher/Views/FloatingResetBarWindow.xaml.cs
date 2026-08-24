using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Delima.Core.Audit;
using Delima.Core.Roster;
using Delima.Core.Services;
using Delima.Win32;
using Wpf.Ui.Controls;

namespace Delima.Launcher.Views;

public partial class FloatingResetBarWindow : FluentWindow
{
    private readonly Student _student;
    private readonly School? _school;
    private readonly BrowserSession? _session;
    private readonly Action _onReset;
    private readonly Action? _onConsentRefused;
    private readonly int _idleResetSeconds;
    private readonly string? _auditDirectory;
    private SessionWatchdog? _watchdog;
    private Timer? _titleWatcherTimer;
    private bool _isCompact = false;
    private string? _savedPrompt;
    private bool _destinationReached = false;
    private int _teardownPerformed = 0;

    public bool IsCompact => _isCompact;
    public bool DestinationReached => _destinationReached;

    public FloatingResetBarWindow(
        Student student,
        BrowserSession? session,
        Action onReset,
        int idleResetSeconds = 600,
        string? initialPrompt = "Lihat nama kamu. Kalau betul, tekan butang biru di bawah.",
        School? school = null,
        Action? onConsentRefused = null,
        string? auditDirectory = null)
    {
        InitializeComponent();
        _student = student;
        _school = school;
        _session = session;
        _onReset = onReset;
        _onConsentRefused = onConsentRefused;
        _idleResetSeconds = idleResetSeconds > 0 ? idleResetSeconds : 600;
        _savedPrompt = initialPrompt;
        _auditDirectory = auditDirectory;

        PupilNameText.Text = student.DisplayName;
        SetPromptMessage(initialPrompt);
        LoadStudentAvatar();

        Loaded += (_, _) =>
        {
            // Position at top center of screen with 16px margin per PRD §7.4
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            Left = (screenWidth - ActualWidth) / 2;
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
                        PerformTeardownAndReset(isConsentRefusal: !_destinationReached);
                    });
                }
            );

            // Watch for destination page load in Chrome/Edge or process exit/cancellation
            if (_session != null)
            {
                _titleWatcherTimer = new Timer(CheckWindowTitle, null, 500, 500);
            }
        };

        Closed += (_, _) =>
        {
            PerformTeardownAndReset(isConsentRefusal: !_destinationReached);
        };
    }

    private void OnBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch
            {
                // Ignore drag exceptions (e.g. rapid double clicks)
            }
        }
    }

    private void OnToggleCollapseClicked(object sender, RoutedEventArgs e)
    {
        SetCompactMode(!_isCompact);
    }

    public void SetCompactMode(bool compact)
    {
        _isCompact = compact;
        if (_isCompact)
        {
            SubtitleText.Visibility = Visibility.Collapsed;
            ConsentPromptBorder.Visibility = Visibility.Collapsed;
            ToggleIcon.Text = "▼";
            ToggleCollapseButton.ToolTip = "Besarkan bar";
            PupilNameText.MaxWidth = 120;
        }
        else
        {
            SubtitleText.Visibility = Visibility.Visible;
            if (!string.IsNullOrWhiteSpace(_savedPrompt))
            {
                ConsentPromptBorder.Visibility = Visibility.Visible;
            }
            ToggleIcon.Text = "▲";
            ToggleCollapseButton.ToolTip = "Kecilkan bar";
            PupilNameText.MaxWidth = 200;
        }
    }

    /// <summary>
    /// Sets or clears the prompt message line on the floating reset bar (§4.5, PRD §7.4).
    /// </summary>
    public void SetPromptMessage(string? message)
    {
        _savedPrompt = message;
        if (string.IsNullOrWhiteSpace(message) || _isCompact)
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
            if (_session == null) return;

            // If the browser process has exited (e.g. closed by user or cancelled)
            if (_session.Process.HasExited)
            {
                Dispatcher.Invoke(() =>
                {
                    PerformTeardownAndReset(isConsentRefusal: !_destinationReached);
                });
                return;
            }

            var title = NativeMethods.GetForegroundTitle();
            if (string.IsNullOrEmpty(title)) return;

            // If the window has transitioned out of Google Sign-in / Consent page into DELIMa / destination
            if (title.Contains("DELIMa", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("Classroom", StringComparison.OrdinalIgnoreCase) ||
                (!title.Contains("Sign in", StringComparison.OrdinalIgnoreCase) &&
                 !title.Contains("Google Accounts", StringComparison.OrdinalIgnoreCase) &&
                 !title.Contains("Welcome", StringComparison.OrdinalIgnoreCase) &&
                 !title.Contains("Log masuk", StringComparison.OrdinalIgnoreCase) &&
                 !title.Contains("Akaun Google", StringComparison.OrdinalIgnoreCase)))
            {
                _destinationReached = true;
                Dispatcher.Invoke(() =>
                {
                    SetPromptMessage(null);
                    // Automatically switch to compact mode once DELIMa destination is reached for continuous pupil use
                    SetCompactMode(true);
                });
            }
        }
        catch
        {
            // Non-critical background observation
        }
    }

    private void OnLogoutClicked(object sender, RoutedEventArgs e)
    {
        PerformTeardownAndReset(isConsentRefusal: !_destinationReached);
    }

    private void PerformTeardownAndReset(bool isConsentRefusal)
    {
        if (Interlocked.Exchange(ref _teardownPerformed, 1) != 0)
        {
            return;
        }

        _titleWatcherTimer?.Dispose();
        _titleWatcherTimer = null;

        _watchdog?.Dispose();
        _watchdog = null;

        if (isConsentRefusal)
        {
            // Log distinct G2 consent refusal event to audit log (§8)
            try
            {
                AuditLogger.RecordConsentRefused(
                    studentId: _student.Id,
                    pupilAccount: _student.EmailLocal,
                    schoolCode: _school?.Code,
                    auditDirectory: _auditDirectory);
            }
            catch
            {
                // Suppress secondary audit error
            }
        }

        try
        {
            _session?.Dispose();
        }
        catch
        {
            // Best effort session cleanup
        }

        try
        {
            if (IsVisible)
            {
                Close();
            }
        }
        catch
        {
            // Ignore close exceptions during teardown
        }

        if (isConsentRefusal && _onConsentRefused != null)
        {
            _onConsentRefused();
        }
        else
        {
            _onReset();
        }
    }

    private void LoadStudentAvatar()
    {
        try
        {
            string seed = DiceBearService.ResolveSeed(_student.Avatar, _student.Id);
            string uri = DiceBearService.GetLocalOrRemoteUri(seed);

            var img = new BitmapImage();
            img.BeginInit();
            img.UriSource = new Uri(uri, UriKind.Absolute);
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.CreateOptions = BitmapCreateOptions.DelayCreation;
            img.EndInit();
            AvatarImage.Source = img;
        }
        catch
        {
            // Keep fallback icon visible if image fails to load
        }
    }
}
