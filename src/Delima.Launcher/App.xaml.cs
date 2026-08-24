using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Delima.Core.Store;
using Delima.Launcher.Theming;
using Delima.Launcher.ViewModels;
using Delima.Win32.Store;

namespace Delima.Launcher;

/// <summary>
/// Application entry point for DELIMa Smart Launcher.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            LogCrash("AppDomain Unhandled Exception", ex);
            MessageBox.Show(
                $"Ralat tidak dijangka (Domain):\n\n{ex?.Message ?? "Unknown error"}\n\nLog ralat disimpan di:\n{GetLogFilePath()}",
                "Ralat Delima Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        };

        DispatcherUnhandledException += (s, args) =>
        {
            LogCrash("Dispatcher Unhandled Exception", args.Exception);
            MessageBox.Show(
                $"Ralat tidak dijangka (UI):\n\n{args.Exception.Message}\n\nLog ralat disimpan di:\n{GetLogFilePath()}",
                "Ralat Delima Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            LogCrash("TaskScheduler Unobserved Task Exception", args.Exception);
            args.SetObserved();
        };

        string? storeDir = null;
        bool isKiosk = false;
        if (e.Args != null)
        {
            for (int i = 0; i < e.Args.Length; i++)
            {
                if ((string.Equals(e.Args[i], "--store-dir", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(e.Args[i], "-s", StringComparison.OrdinalIgnoreCase)) && i + 1 < e.Args.Length)
                {
                    storeDir = e.Args[i + 1];
                    i++;
                }
                else if (string.Equals(e.Args[i], "--kiosk", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(e.Args[i], "-k", StringComparison.OrdinalIgnoreCase))
                {
                    isKiosk = true;
                }
            }
        }

        string effectiveStoreDir = storeDir ?? DpapiCredentialStore.GetDefaultStoreDirectory();
        ThemeInfo? customTheme = null;
        if (DpapiCredentialStore.StoreExists(effectiveStoreDir))
        {
            try
            {
                using var store = DpapiCredentialStore.Open(effectiveStoreDir);
                customTheme = store.Theme;
            }
            catch
            {
                // Fallback to default theme on error
            }
        }

        // Apply WPF-UI's Fluent 2 control theme first, then layer the app's own brand theme on top
        Wpf.Ui.Appearance.ApplicationThemeManager.ApplySystemTheme();
        ThemeBuilder.ApplyTheme(Resources, customTheme);

        var mainVm = new MainViewModel(storeDir);
        var mainWindow = new MainWindow(isKiosk: isKiosk) { DataContext = mainVm };
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    private static string GetLogFilePath()
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Delima", "logs");
        return Path.Combine(logDir, "launcher_crash.log");
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
