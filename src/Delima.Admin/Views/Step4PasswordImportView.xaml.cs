using System.Windows;
using System.Windows.Controls;
using Delima.Admin.Models;
using Delima.Admin.ViewModels;
using Microsoft.Win32;

namespace Delima.Admin.Views;

public partial class Step4PasswordImportView : UserControl
{
    public Step4PasswordImportView()
    {
        InitializeComponent();
    }

    private void OnLoadPasswordFileClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Fail Kata Laluan (*.csv;*.xlsx;*.xls)|*.csv;*.xlsx;*.xls|Semua Fail (*.*)|*.*",
            Title = "Pilih Fail Kata Laluan Murid"
        };

        if (dlg.ShowDialog() == true && DataContext is Step4PasswordImportViewModel vm)
        {
            vm.LoadPasswordFile(dlg.FileName);

            var deleteChoice = MessageBox.Show(
                "Adakah anda ingin memadam fail sumber kata laluan plaintext ini secara selamat dari cakera sekarang? (Disyorkan demi keselamatan)",
                "Padam Fail Sumber Secara Selamat",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (deleteChoice == MessageBoxResult.Yes)
            {
                Step4PasswordImportViewModel.SecureDeleteFile(dlg.FileName);
                MessageBox.Show("Fail sumber kata laluan telah dipadamkan dengan selamat.", "Padam Selamat", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private void OnPasswordCellClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PasswordGridItem item && DataContext is Step4PasswordImportViewModel vm)
        {
            if (item.IsRevealed)
            {
                item.IsRevealed = false;
            }
            else
            {
                PopoverPasswordBox.Password = "";
                vm.OpenRevealPopover(item);
                PopoverPasswordBox.Focus();
            }
        }
    }

    private void OnCancelPopoverClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is Step4PasswordImportViewModel vm)
        {
            vm.IsPopoverOpen = false;
        }
    }

    private void OnConfirmRevealClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is Step4PasswordImportViewModel vm)
        {
            vm.VerifyAndReveal(PopoverPasswordBox.Password);
        }
    }

    private void OnMaskAllClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is Step4PasswordImportViewModel vm)
        {
            vm.MaskAll();
        }
    }
}
