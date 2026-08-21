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
            Filter = "Fail Data APDM (*.csv;*.xlsx;*.xls)|*.csv;*.xlsx;*.xls|Semua Fail (*.*)|*.*",
            Title = "Pilih Fail Roster Murid"
        };

        if (dlg.ShowDialog() == true && DataContext is Step3RosterImportViewModel vm)
        {
            vm.LoadFile(dlg.FileName);
        }
    }

    private void OnMappingSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is Step3RosterImportViewModel vm)
        {
            vm.UpdatePreview();
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
