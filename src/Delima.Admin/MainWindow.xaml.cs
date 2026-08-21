using System.Windows;
using Delima.Admin.ViewModels;

namespace Delima.Admin;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWizardViewModel();
    }
}
