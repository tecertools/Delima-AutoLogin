using System.Windows;
using System.Windows.Controls;
using Delima.Provision.ViewModels;

namespace Delima.Provision;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ProvisionViewModel _viewModel;

    public MainWindow(ProvisionOptions options)
    {
        InitializeComponent();
        _viewModel = new ProvisionViewModel(options);
        DataContext = _viewModel;

        // Keep password box synced if prefilled
        if (!string.IsNullOrEmpty(_viewModel.Passphrase))
        {
            AdminPasswordBox.Password = _viewModel.Passphrase;
        }
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox box)
        {
            _viewModel.Passphrase = box.Password;
        }
    }
}
