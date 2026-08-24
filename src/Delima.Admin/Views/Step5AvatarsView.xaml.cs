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
        if (DataContext is Step5AvatarsViewModel vm)
        {
            try
            {
                string path = vm.PrintAvatarSheet();
                MessageBox.Show($"Helaian kata laluan gambar ({vm.SelectedYearFilter} - {vm.SelectedClassFilter}) telah dibuka di pelayar untuk dicetak.\n\nLokasi fail:\n{path}",
                    "Cetak Kata Laluan Gambar", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ralat semasa menjana helaian cetakan kata laluan gambar: {ex.Message}",
                    "Ralat Cetakan", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

