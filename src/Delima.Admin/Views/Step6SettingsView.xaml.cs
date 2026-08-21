using System.Windows;
using System.Windows.Controls;
using Delima.Admin.ViewModels;
using Delima.Core.Store;

namespace Delima.Admin.Views;

public partial class Step6SettingsView : UserControl
{
    public Step6SettingsView()
    {
        InitializeComponent();
    }

    private void OnAddDestinationClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is Step6SettingsViewModel vm)
        {
            vm.AddDestination();
        }
    }

    private void OnRemoveDestinationClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is DestinationConfig item && DataContext is Step6SettingsViewModel vm)
        {
            vm.RemoveDestination(item);
        }
    }
}
