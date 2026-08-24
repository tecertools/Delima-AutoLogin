using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Delima.Admin;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Wpf.Ui.Appearance.ApplicationThemeManager.ApplySystemTheme();

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            LogCrash("AppDomain Unhandled Exception", ex);
            MessageBox.Show(
                $"Ralat tidak dijangka (Domain):\n\n{ex?.Message ?? "Unknown error"}\n\nLog ralat disimpan di:\n{GetLogFilePath()}",
                "Ralat Delima Admin",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        };

        DispatcherUnhandledException += (s, args) =>
        {
            LogCrash("Dispatcher Unhandled Exception", args.Exception);
            MessageBox.Show(
                $"Ralat tidak dijangka (UI):\n\n{args.Exception.Message}\n\nLog ralat disimpan di:\n{GetLogFilePath()}",
                "Ralat Delima Admin",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            LogCrash("TaskScheduler Unobserved Task Exception", args.Exception);
            args.SetObserved();
        };
    }

    private static string GetLogFilePath()
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Delima", "logs");
        return Path.Combine(logDir, "admin_crash.log");
    }

    private static void LogCrash(string source, Exception? ex)
    {
        try
        {
            var logPath = GetLogFilePath();
            var dir = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}]\n{ex?.ToString() ?? "Unknown exception"}\n\n";
            File.AppendAllText(logPath, text);
        }
        catch
        {
            // Suppress secondary failures during crash logging
        }
    }
}
