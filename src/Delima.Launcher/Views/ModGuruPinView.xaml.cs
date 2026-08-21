using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Delima.Launcher.ViewModels;

namespace Delima.Launcher.Views;

/// <summary>
/// Interaction logic for ModGuruPinView.xaml.
/// Supports both on-screen touch keypad and physical keyboard input for kiosk operation.
/// </summary>
public partial class ModGuruPinView : UserControl
{
    public ModGuruPinView()
    {
        InitializeComponent();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        Focus();
    }

    private void UserControl_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ModGuruPinViewModel vm)
            return;

        // Digit keys 0-9
        if (e.Key >= Key.D0 && e.Key <= Key.D9)
        {
            int digit = e.Key - Key.D0;
            vm.AppendDigit(digit.ToString());
            e.Handled = true;
        }
        // NumPad keys 0-9
        else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
        {
            int digit = e.Key - Key.NumPad0;
            vm.AppendDigit(digit.ToString());
            e.Handled = true;
        }
        // Backspace
        else if (e.Key == Key.Back)
        {
            vm.Backspace();
            e.Handled = true;
        }
        // Escape / Batal
        else if (e.Key == Key.Escape)
        {
            vm.Cancel();
            e.Handled = true;
        }
    }
}
