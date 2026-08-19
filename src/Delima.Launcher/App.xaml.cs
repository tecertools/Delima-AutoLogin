using System.Windows;
using Delima.Launcher.Theming;

namespace Delima.Launcher;

/// <summary>
/// Application entry point for DELIMa Smart Launcher.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Apply default theme to Application resources at startup
        ThemeBuilder.ApplyTheme(Resources, null);
    }
}
