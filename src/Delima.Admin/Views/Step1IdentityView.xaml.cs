using System.Windows;
using System.Windows.Controls;
using Delima.Admin.ViewModels;
using Microsoft.Win32;

namespace Delima.Admin.Views;

public partial class Step1IdentityView : UserControl
{
    public Step1IdentityView()
    {
        InitializeComponent();
    }

    private void OnSelectCrestClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Imej Lambang (*.png;*.jpg;*.svg)|*.png;*.jpg;*.jpeg;*.svg|Semua Fail (*.*)|*.*",
            Title = "Pilih Lambang Sekolah"
        };

        if (dlg.ShowDialog() == true && DataContext is Step1IdentityViewModel vm)
        {
            vm.CrestPath = dlg.FileName;
        }
    }
}
