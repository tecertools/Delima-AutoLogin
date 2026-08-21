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

    private void OnSaveBundleFileClick(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "DELIMa Master Pack (*.dlmpack)|*.dlmpack",
            FileName = "school.dlmpack",
            Title = "Simpan Bungkusan Induk Sekolah"
        };

        if (dlg.ShowDialog() == true && DataContext is Step7ProvisionViewModel vm)
        {
            vm.SaveBundleToFile(dlg.FileName);
        }
    }

    private void OnSaveToNetworkClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is Step7ProvisionViewModel vm)
        {
            try
            {
                string targetDir = vm.NetworkSharePath;
                if (!System.IO.Directory.Exists(targetDir))
                {
                    System.IO.Directory.CreateDirectory(targetDir);
                }
                string targetFile = System.IO.Path.Combine(targetDir, "school.dlmpack");
                vm.SaveBundleToFile(targetFile);
            }
            catch (Exception ex)
            {
                vm.StatusMessage = $"Ralat menyimpan ke laluan rangkaian: {ex.Message}";
                vm.IsSuccess = false;
            }
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
