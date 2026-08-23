using System.Windows;

namespace Delima.Provision;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string[] args = e.Args ?? [];
        var options = ProvisionOptions.Parse(args);

        // If command-line arguments are provided, run headless CLI engine
        if (ProvisionOptions.HasCommandLineArgs(args))
        {
            var result = ProvisionEngine.Execute(options);
            Shutdown(result.ExitCode);
            return;
        }

        // Otherwise, launch modern WPF GUI
        var mainWindow = new MainWindow(options);
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}
