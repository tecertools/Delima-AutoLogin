using System.Windows;
using System.Windows.Controls;
using Delima.Admin.ViewModels;

namespace Delima.Admin.Views;

public partial class Step5AvatarsView : UserControl
{
    public Step5AvatarsView()
    {
        InitializeComponent();
    }

    private void OnCycleAvatarClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is AvatarAssignmentItem item && DataContext is Step5AvatarsViewModel vm)
        {
            vm.CycleAvatar(item);
        }
    }

    private void OnPrintAvatarSheetClick(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Helaian avatar kelas sedia dicetak untuk dinding kelas.", "Cetak Helaian Avatar", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
