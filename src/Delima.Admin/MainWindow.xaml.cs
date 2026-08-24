using Delima.Admin.ViewModels;
using Wpf.Ui.Controls;

namespace Delima.Admin;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        var vm = new MainWizardViewModel();
        vm.RequestClose = () => Close();
        DataContext = vm;
    }
}
