using System.Diagnostics;
using Microsoft.Win32;

namespace InjectionSpike;

/// <summary>
/// Chrome path resolution, throwaway-profile launch, and process-tree teardown.
/// Mirrors what production ProcessLauncher will need, so the spike validates
/// those paths too rather than only the injection step.
/// </summary>
internal sealed class ChromeSession : IDisposable
{
    public Process Process { get; }
    public string ProfileDir { get; }

    private bool _disposed;

    private ChromeSession(Process process, string profileDir)
    {
        Process = process;
        ProfileDir = profileDir;
    }

    /// <summary>
    /// Resolves chrome.exe. The PRD hardcodes the 64-bit Program Files path,
    /// which fails on 32-bit and per-user installs; this is the fallback chain
    /// production should use.
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
    /// Launches Chrome with a unique throwaway profile so no cookies, history or
    /// saved credentials survive between pupils.
    /// </summary>
    public static ChromeSession Launch(string chromePath, string url)
    {
        var profileDir = Path.Combine(Path.GetTempPath(), "delima_spike_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(profileDir);

        var psi = new ProcessStartInfo(chromePath)
        {
            UseShellExecute = false
        };

        psi.ArgumentList.Add($"--user-data-dir={profileDir}");
        psi.ArgumentList.Add("--no-first-run");
        psi.ArgumentList.Add("--no-default-browser-check");
        psi.ArgumentList.Add("--disable-features=PasswordManagerOnboarding,AutofillServerCommunication");
        psi.ArgumentList.Add("--password-store=basic");
        psi.ArgumentList.Add("--new-window");
        psi.ArgumentList.Add(url);

        var proc = Process.Start(psi)
                   ?? throw new InvalidOperationException("Process.Start returned null for chrome.exe");

        return new ChromeSession(proc, profileDir);
    }

    /// <summary>
    /// Waits until a Chrome window owned by this session's process tree is in the
    /// foreground and its title satisfies <paramref name="titlePredicate"/>.
    /// Returns the elapsed time, or null on timeout.
    ///
    /// This is the check the PRD's fixed 1,500 ms sleep is missing.
    /// </summary>
    public TimeSpan? WaitForForegroundWindow(Func<string, bool> titlePredicate, TimeSpan timeout, int pollMs = 100)
    {
        const string ChromeWindowClass = "Chrome_WidgetWin_1";
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < timeout)
        {
            var cls = NativeMethods.GetForegroundClassName();
            var title = NativeMethods.GetForegroundTitle();

            if (cls == ChromeWindowClass && titlePredicate(title))
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
    /// PID tree. Never `taskkill /IM chrome.exe`, which would also kill the
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

        // Chrome releases the profile lock asynchronously; retry the wipe.
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
