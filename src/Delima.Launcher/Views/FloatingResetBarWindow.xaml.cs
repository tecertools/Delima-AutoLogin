using System.Windows;
using Delima.Core.Roster;
using Delima.Win32;

namespace Delima.Launcher.Views;

public partial class FloatingResetBarWindow : Window
{
    private readonly ChromeSession? _session;
    private readonly Action _onReset;

    public FloatingResetBarWindow(Student student, ChromeSession? session, Action onReset)
    {
        InitializeComponent();
        _session = session;
        _onReset = onReset;

        PupilNameText.Text = student.DisplayName;

        Loaded += (_, _) =>
        {
            // Position at top center of screen with 16px margin per PRD §7.4
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            Left = (screenWidth - Width) / 2;
            Top = 16;
        };
    }

    private void OnLogoutClicked(object sender, RoutedEventArgs e)
    {
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
