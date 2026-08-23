using System.Diagnostics;

namespace Delima.Win32;

/// <summary>
/// Backward-compatible ChromeSession wrapper inheriting from <see cref="BrowserSession"/> per Technical Architecture §4.4.1.
/// New code should prefer <see cref="BrowserSession"/> directly.
/// </summary>
public sealed class ChromeSession : BrowserSession
{
    public ChromeSession(Process process, string profileDir, string executablePath = "")
        : base(process, profileDir, BrowserKind.Chrome, executablePath)
    {
    }

    /// <summary>
    /// Launches Chrome with a unique throwaway profile so no cookies, history, or
    /// saved credentials survive between pupils.
    /// Passes --force-renderer-accessibility by default so Chrome exposes its accessibility tree for UIA per §4.2 and §11.1.
    /// </summary>
    public static ChromeSession Launch(string chromePath, string url, bool forceRendererAccessibility = true)
    {
        var session = BrowserSession.Launch(chromePath, url, forceRendererAccessibility, BrowserKind.Chrome);
        return new ChromeSession(session.Process, session.ProfileDir, chromePath);
    }
}
