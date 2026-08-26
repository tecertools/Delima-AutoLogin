using System.Windows;
using System.Windows.Controls;
using Delima.Admin.ViewModels;
using Microsoft.Win32;

namespace Delima.Admin.Views;

public partial class Step7ProvisionView : UserControl
{
    public Step7ProvisionView()
    {
        InitializeComponent();
    }

    private void OnSelectUsbRouteClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is Step7ProvisionViewModel vm)
        {
            vm.SelectedRoute = "Usb";
        }
    }

    private void OnSelectNetworkRouteClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is Step7ProvisionViewModel vm)
        {
            vm.SelectedRoute = "Network";
        }
    }

    private void OnSelectScriptRouteClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is Step7ProvisionViewModel vm)
        {
            vm.SelectedRoute = "Script";
        }
    }

    private void OnRefreshUsbClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is Step7ProvisionViewModel vm)
        {
            vm.RefreshUsbDrives();
        }
    }

    private async void OnSaveToSelectedUsbClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is Step7ProvisionViewModel vm)
        {
            if (vm.SelectedUsbDrive == null)
            {
                MessageBox.Show("Sila pilih pemacu USB terlebih dahulu.", "Tiada USB Dipilih", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool ok = await vm.SaveBundleToUsbAsync(vm.SelectedUsbDrive);
            if (ok)
            {
                MessageBox.Show(
                    $"Pakej persediaan makmal berjaya disimpan ke pendrive:\n{vm.LastExportedDirectory}\n\nAnda kini boleh mencucuk pendrive ini pada setiap PC makmal dan menjalankan '1_Sediakan_Makmal.exe'.",
                    "Pakej USB Berjaya Disediakan",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
    }

    private void OnOpenExportedFolderClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is Step7ProvisionViewModel vm)
        {
            vm.OpenExportedFolder();
        }
    }

    private async void OnSaveBundleFileClick(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "DELIMa Master Pack (*.dlmpack)|*.dlmpack",
            FileName = "school.dlmpack",
            Title = "Simpan Bungkusan Induk Sekolah"
        };

        if (dlg.ShowDialog() == true && DataContext is Step7ProvisionViewModel vm)
        {
            await vm.SaveBundleToFileAsync(dlg.FileName);
        }
    }

    private async void OnSaveToNetworkClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is Step7ProvisionViewModel vm)
        {
            await vm.SaveToNetworkAsync(vm.NetworkSharePath);
        }
    }

    private void OnCopyScriptClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is Step7ProvisionViewModel vm)
        {
            vm.CopyScriptToClipboard();
        }
    }

    private void OnExportChecklistClick(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Fail CSV (*.csv)|*.csv",
            FileName = "lab_checklist.csv",
            Title = "Simpan Senarai Semak Makmal"
        };

        if (dlg.ShowDialog() == true && DataContext is Step7ProvisionViewModel vm)
        {
            vm.ExportChecklistCsv(dlg.FileName);
        }
    }
}
