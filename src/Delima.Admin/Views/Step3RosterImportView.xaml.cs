using System.Windows;
using System.Windows.Controls;
using Delima.Admin.ViewModels;
using Microsoft.Win32;

namespace Delima.Admin.Views;

public partial class Step3RosterImportView : UserControl
{
    public Step3RosterImportView()
    {
        InitializeComponent();
    }

    private void OnSelectFileClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Fail Data Roster (*.csv;*.xlsx;*.xls;*.tsv;*.txt)|*.csv;*.xlsx;*.xls;*.tsv;*.txt|Fail CSV (*.csv)|*.csv|Fail Excel (*.xlsx;*.xls)|*.xlsx;*.xls|Semua Fail (*.*)|*.*",
            Title = "Pilih Fail Roster Murid"
        };

        if (dlg.ShowDialog() == true && DataContext is Step3RosterImportViewModel vm)
        {
            vm.LoadFile(dlg.FileName);
        }
    }

    private void OnDownloadTemplateClick(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Fail CSV Templat (*.csv)|*.csv",
            FileName = "templat_roster_delima.csv",
            Title = "Muat Turun Templat Senarai Murid"
        };

        if (dlg.ShowDialog() == true && DataContext is Step3RosterImportViewModel vm)
        {
            vm.SaveTemplate(dlg.FileName);
            MessageBox.Show($"Templat senarai murid berjaya disimpan ke:\n{dlg.FileName}\n\nAnda boleh membuka dan mengisi fail ini menggunakan Microsoft Excel atau Google Sheets.", "Templat Dimuat Turun", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OnFileDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private void OnFileDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0 && DataContext is Step3RosterImportViewModel vm)
            {
                vm.LoadFile(files[0]);
            }
        }
    }

    private void OnMappingSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is Step3RosterImportViewModel vm)
        {
            vm.UpdatePreview();
        }
    }

    private void OnRunDryRunClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is Step3RosterImportViewModel vm)
        {
            vm.RunDryRunAnalysis();
        }
    }

    private void OnClearFileClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is Step3RosterImportViewModel vm)
        {
            vm.ClearFile();
        }
    }

    private void OnBackToMappingClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is Step3RosterImportViewModel vm)
        {
            vm.GoToMappingView();
        }
    }

    private void OnExportRejectsClick(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Fail CSV (*.csv)|*.csv",
            FileName = $"rejects_{DateTime.Now:yyyy-MM-dd}.csv",
            Title = "Simpan Rekod Murid Ditolak"
        };

        if (dlg.ShowDialog() == true && DataContext is Step3RosterImportViewModel vm)
        {
            vm.ExportRejectsCsv(dlg.FileName);
            MessageBox.Show($"Rekod ditolak berjaya disimpan ke: {dlg.FileName}", "Eksport Berjaya", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

