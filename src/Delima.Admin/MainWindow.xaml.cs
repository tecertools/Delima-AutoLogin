using System.Windows;
using Delima.Admin.ViewModels;

namespace Delima.Admin;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var vm = new MainWizardViewModel();
        vm.RequestClose = () => Close();
        DataContext = vm;
    }
}
