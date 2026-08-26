using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace Delima.Win32;

/// <summary>
/// Browser path resolution, throwaway-profile launch, and scoped process-tree teardown
/// for Chromium browsers (Microsoft Edge and Google Chrome) per Technical Architecture §4.4.1.
/// 
/// Note per §4.2 & §4.4.1: Both Edge and Chrome report window class "Chrome_WidgetWin_1".
/// Therefore, the class check cannot distinguish the two browsers and the PID check is doing all the work.
/// </summary>
public class BrowserSession : IDisposable
{
    public const string ChromiumWindowClass = "Chrome_WidgetWin_1";

    public Process Process { get; }
    public string ProfileDir { get; }
    public BrowserKind BrowserKind { get; }
    public string ExecutablePath { get; }

    private bool _disposed;

    public BrowserSession(Process process, string profileDir, BrowserKind browserKind = BrowserKind.Edge, string executablePath = "")
    {
        Process = process;
        ProfileDir = profileDir;
        BrowserKind = browserKind;
        ExecutablePath = executablePath;
    }

    /// <summary>
    /// Resolves msedge.exe. Checks registry App Paths across 64-bit, 32-bit, and CurrentUser,
    /// falling back to standard ProgramFiles, ProgramFilesX86, and LocalApplicationData.
    /// </summary>
    public static string? ResolveEdgePath()
    {
        const string appPaths = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe";

        foreach (var (root, view) in new[]
                 {
                     (RegistryHive.LocalMachine, RegistryView.Registry64),
                     (RegistryHive.LocalMachine, RegistryView.Registry32),
                     (RegistryHive.CurrentUser,  RegistryView.Default)
                 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(root, view);
                using var key = baseKey.OpenSubKey(appPaths);
                var path = key?.GetValue(null) as string;
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) return path;
            }
            catch
            {
                // Registry view unavailable on this SKU; fall through.
            }
        }

        string[] candidates =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                         @"Microsoft\Edge\Application\msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                         @"Microsoft\Edge\Application\msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         @"Microsoft\Edge\Application\msedge.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// Resolves chrome.exe. Checks registry App Paths across 64-bit, 32-bit, and CurrentUser,
    /// falling back to standard ProgramFiles, ProgramFilesX86, and LocalApplicationData.
    /// </summary>
    public static string? ResolveChromePath()
    {
        const string appPaths = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe";

        foreach (var (root, view) in new[]
                 {
                     (RegistryHive.LocalMachine, RegistryView.Registry64),
                     (RegistryHive.LocalMachine, RegistryView.Registry32),
                     (RegistryHive.CurrentUser,  RegistryView.Default)
                 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(root, view);
                using var key = baseKey.OpenSubKey(appPaths);
                var path = key?.GetValue(null) as string;
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) return path;
            }
            catch
            {
                // Registry view unavailable on this SKU; fall through.
            }
        }

        string[] candidates =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                         @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                         @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         @"Google\Chrome\Application\chrome.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// Resolves a supported browser honoring <paramref name="preference"/> ("auto", "edge", or "chrome").
    /// Default is "auto": resolves Edge first, then Chrome.
    /// </summary>
    public static (BrowserKind Kind, string Path)? ResolveBrowser(string? preference = "auto")
    {
        var pref = (preference ?? "auto").Trim().ToLowerInvariant();

        if (pref == "edge")
        {
            var edge = ResolveEdgePath();
            return edge != null ? (BrowserKind.Edge, edge) : null;
        }

        if (pref == "chrome")
        {
            var chrome = ResolveChromePath();
            return chrome != null ? (BrowserKind.Chrome, chrome) : null;
        }

        // "auto" (default: prefer Edge, else Chrome)
        var autoEdge = ResolveEdgePath();
        if (autoEdge != null) return (BrowserKind.Edge, autoEdge);

        var autoChrome = ResolveChromePath();
        if (autoChrome != null) return (BrowserKind.Chrome, autoChrome);

        return null;
    }

    /// <summary>
    /// Launches Microsoft Edge or Google Chrome with a unique throwaway profile so no cookies, history, or
    /// saved credentials survive between pupils.
    /// Passes --force-renderer-accessibility by default so Chromium exposes its accessibility tree for UIA per §4.2 and §11.1.
    /// </summary>
    public static BrowserSession Launch(
        string browserPath,
        string url,
        bool forceRendererAccessibility = true,
        BrowserKind? browserKind = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(browserPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        var kind = browserKind ?? DetectBrowserKind(browserPath);
        var profileDir = Path.Combine(Path.GetTempPath(), "delima_session_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(profileDir);

        SeedProfilePreferences(profileDir);

        var psi = new ProcessStartInfo(browserPath)
        {
            UseShellExecute = false
        };

        psi.ArgumentList.Add($"--user-data-dir={profileDir}");
        psi.ArgumentList.Add("--no-first-run");
        psi.ArgumentList.Add("--no-default-browser-check");
        psi.ArgumentList.Add("--disable-extensions");
        psi.ArgumentList.Add("--disable-component-extensions-with-background-pages");
        psi.ArgumentList.Add("--disable-default-apps");
        if (forceRendererAccessibility)
        {
            psi.ArgumentList.Add("--force-renderer-accessibility");
        }
        psi.ArgumentList.Add("--disable-features=Translate,TranslateUI,AppBanners,InstallPrompt,PwaInstallPrompt,WebAppInstallation,WebAppManifest,PasswordManagerOnboarding,AutofillServerCommunication,OptimizationGuideModelDownloading,OptimizationHintsUI,EdgeShowSmartScreenWarning,msEdgeWebWidget,msEdgeShoppingAssistant,msEdgeSidebarV2,msHubs,msPromotions");
        psi.ArgumentList.Add("--disable-translate");
        psi.ArgumentList.Add("--disable-infobars");
        psi.ArgumentList.Add("--disable-notifications");
        psi.ArgumentList.Add("--disable-popup-blocking");
        psi.ArgumentList.Add("--disable-prompt-on-repost");
        psi.ArgumentList.Add("--disable-search-engine-choice-screen");
        psi.ArgumentList.Add("--lang=ms-MY");
        psi.ArgumentList.Add("--accept-lang=ms-MY,ms,en-US,en");
        psi.ArgumentList.Add("--password-store=basic");
        psi.ArgumentList.Add("--new-window");
        psi.ArgumentList.Add(url);

        var proc = Process.Start(psi)
                   ?? throw new InvalidOperationException($"Process.Start returned null for '{browserPath}'");

        return new BrowserSession(proc, profileDir, kind, browserPath);
    }

    /// <summary>
    /// Pre-populates the throwaway profile directory with Preferences and Local State
    /// to disable translation prompts, PWA app install banners, and set accepted language to Malay.
    /// </summary>
    private static void SeedProfilePreferences(string profileDir)
    {
        try
        {
            var defaultDir = Path.Combine(profileDir, "Default");
            Directory.CreateDirectory(defaultDir);

            var preferencesJson = """
            {
              "translate": {
                "enabled": false,
                "blocked_languages": ["ms", "zsm", "en", "id", "en-US", "ms-MY"]
              },
              "translate_blocked_languages": ["ms", "zsm", "en", "id", "en-US", "ms-MY"],
              "translate_site_blocklist": ["d3.delima.edu.my", "delima.edu.my", "accounts.google.com"],
              "intl": {
                "accept_languages": "ms-MY,ms,en-US,en",
                "selected_languages": "ms-MY,ms,en-US,en"
              },
              "app_banners": {
                "pwa_install_prompts_enabled": false
              },
              "profile": {
                "password_manager_enabled": false,
                "default_content_setting_values": {
                  "notifications": 2,
                  "automatic_downloads": 1
                }
              },
              "browser": {
                "show_hub_popup_on_first_add": false,
                "has_seen_welcome_page": true,
                "check_default_browser": false
              },
              "edge": {
                "smartscreen_enabled": false,
                "shopping_assistant_enabled": false,
                "sidebar": {
                  "sidebar_search_open_in_sidebar": false
                }
              }
            }
            """;
            File.WriteAllText(Path.Combine(defaultDir, "Preferences"), preferencesJson);

            var localStateJson = """
            {
              "translate": {
                "enabled": false
              },
              "intl": {
                "app_locale": "ms"
              },
              "browser": {
                "enabled_labs_experiments": [],
                "has_seen_welcome_page": true
              }
            }
            """;
            File.WriteAllText(Path.Combine(profileDir, "Local State"), localStateJson);
        }
        catch
        {
            // Non-critical best-effort pre-configuration
        }
    }

    /// <summary>
    /// Infers browser kind from executable name or path.
    /// </summary>
    public static BrowserKind DetectBrowserKind(string path)
    {
        var fileName = Path.GetFileName(path);
        if (fileName.Contains("edge", StringComparison.OrdinalIgnoreCase))
        {
            return BrowserKind.Edge;
        }
        return BrowserKind.Chrome;
    }

    /// <summary>
    /// Waits until a browser window owned by this session's process tree is in the
    /// foreground and its title satisfies <paramref name="titlePredicate"/>.
    /// Returns elapsed time, or null on timeout.
    /// Note: PID check is required because Edge and Chrome share the "Chrome_WidgetWin_1" class.
    /// </summary>
    public TimeSpan? WaitForForegroundWindow(Func<string, bool> titlePredicate, TimeSpan timeout, int pollMs = 100)
    {
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < timeout)
        {
            var cls = NativeMethods.GetForegroundClassName();
            var title = NativeMethods.GetForegroundTitle();
            var pid = NativeMethods.GetForegroundProcessId();

            if (cls == ChromiumWindowClass && pid == (uint)Process.Id && titlePredicate(title))
            {
                sw.Stop();
                return sw.Elapsed;
            }

            Thread.Sleep(pollMs);
        }

        return null;
    }

    /// <summary>Polls the foreground window title until it matches, or times out.</summary>
    public string? WaitForTitle(Func<string, bool> predicate, TimeSpan timeout, int pollMs = 100)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            var title = NativeMethods.GetForegroundTitle();
            if (predicate(title)) return title;
            Thread.Sleep(pollMs);
        }
        return null;
    }

    /// <summary>
    /// Graceful close first, /T /F only as a timeout fallback, and scoped to this
    /// PID tree. Never `taskkill /IM chrome.exe` or `taskkill /IM msedge.exe`, which would also kill the
    /// teacher's own browser and corrupt its profile.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (!Process.HasExited)
            {
                Process.CloseMainWindow();
                if (!Process.WaitForExit(3000))
                {
                    RunTaskkill(Process.Id);
                    Process.WaitForExit(5000);
                }
            }
        }
        catch
        {
            // Process already gone.
        }

        try { Process.Dispose(); } catch { /* ignore */ }

        // Browser releases the profile lock asynchronously; retry the wipe.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (Directory.Exists(ProfileDir)) Directory.Delete(ProfileDir, recursive: true);
                break;
            }
            catch
            {
                Thread.Sleep(400);
            }
        }
    }

    private static void RunTaskkill(int pid)
    {
        try
        {
            var psi = new ProcessStartInfo("taskkill")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add("/T");
            psi.ArgumentList.Add("/F");
            psi.ArgumentList.Add("/PID");
            psi.ArgumentList.Add(pid.ToString());

            using var p = Process.Start(psi);
            p?.WaitForExit(5000);
        }
        catch
        {
            // Best effort.
        }
    }
}
