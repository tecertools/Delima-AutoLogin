using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Delima.Win32;

namespace InjectionSpike;

internal static class Program
{
    // Passwords chosen to cover the characters SendKeys treats as control
    // syntax, plus ordinary controls. If SendKeys is the problem this document
    // claims, the reserved-character rows fail and the plain rows pass.
    private static readonly (string Label, string Value)[] TestPasswords =
    {
        ("plain-lower",        "murid12345"),
        ("plain-mixed",        "Murid2026x"),
        ("digits-symbols-ok",  "Murid#2026!"),
        ("plus",               "Murid+2026"),
        ("caret",              "Murid^2026"),
        ("percent",            "Murid%2026"),
        ("tilde",              "Murid~2026"),
        ("parens",             "Murid(2026)"),
        ("braces",             "Murid{2026}"),
        ("brackets",           "Murid[2026]"),
        ("all-reserved",       "M+u^r%i~d(2){0}[26]"),
        ("moe-style",          "Sk24#Murid+2026"),
    };

    // Deliberately not hardcoded. Task T0.2 is to establish what the live
    // SP-initiated entry point actually is; until that is answered, the default
    // is the portal itself, which performs the real redirect to Google. Override
    // with --url once T0.2 gives you the canonical URL.
    //
    // {0} is replaced with the URL-encoded login hint.
    private const string DefaultTimingUrl = "https://d3.delima.edu.my";
    private const string DefaultUiaUrl = "https://d3.delima.edu.my/landing";

    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var mode = args.FirstOrDefault()?.ToLowerInvariant() ?? "help";
        var runs = ParseIntArg(args, "--runs", 50);
        var method = ParseStringArg(args, "--method", "sendinput").ToLowerInvariant();
        var settleMs = ParseIntArg(args, "--settle", 400);
        var charDelayMs = ParseIntArg(args, "--char-delay", 0);
        var hint = ParseStringArg(args, "--login-hint", "m-00000000@moe-dl.edu.my");
        var noAccessibility = args.Any(a => string.Equals(a, "--no-accessibility", StringComparison.OrdinalIgnoreCase));

        var defaultUrl = mode == "uia" ? DefaultUiaUrl : DefaultTimingUrl;
        var url = ParseStringArg(args, "--url", defaultUrl);

        // Guard against exactly the mistake that produced a mislabelled run in
        // practice: --method sendkets (typo) silently fell through the old
        // `if (method == "sendkeys") ... else sendinput` check and ran
        // SendInput under a filename that read as the SendKeys control. The
        // whole point of Mode A is a clean two-way comparison; a third,
        // accidental method that nothing warns about defeats it silently.
        if (mode == "fidelity" && method != "sendkeys" && method != "sendinput")
        {
            Console.Error.WriteLine($"FATAL: --method \"{method}\" is neither \"sendkeys\" nor \"sendinput\".");
            Console.Error.WriteLine("       Refusing to guess. A typo here previously ran SendInput under a");
            Console.Error.WriteLine("       filename that looked like the SendKeys control -- check spelling.");
            return 2;
        }

        var chromePath = ChromeSession.ResolveChromePath();
        if (chromePath is null)
        {
            Console.Error.WriteLine("FATAL: chrome.exe not found via registry or well-known paths.");
            Console.Error.WriteLine("       This is itself a finding — the PRD hardcodes one path.");
            return 2;
        }
        Console.WriteLine($"Chrome: {chromePath}");

        // A run started after any interrupted/crashed earlier attempt can find
        // a leftover "SPIKE:*" window still open. WaitForForegroundWindow would
        // then latch onto that stale window instead of the fresh one, producing
        // exactly what a real session showed: ~45% NO_VERDICT_TIMEOUT and one
        // run with a 15s "ready" time versus ~650ms for the rest. Clear the
        // slate before every batch.
        var stale = NativeMethods.CloseWindowsWithTitlePrefix("SPIKE:");
        if (stale > 0)
        {
            Console.WriteLine($"Closed {stale} leftover SPIKE window(s) from an earlier run.");
            Thread.Sleep(500);
        }
        Console.WriteLine();

        return mode switch
        {
            "fidelity" => RunFidelity(chromePath, runs, method, settleMs, charDelayMs),
            "timing"   => RunTiming(chromePath, runs, url, hint),
            "uia"      => RunUia(chromePath, runs, url, noAccessibility),
            _          => PrintHelp()
        };
    }

    // ------------------------------------------------------------------
    // Mode A — character fidelity against a local page.
    // Isolates injection correctness from Google entirely: no network, no
    // account, no rate limiting. This is the run that answers the SendKeys
    // question definitively.
    // ------------------------------------------------------------------
    private static int RunFidelity(string chromePath, int runs, string method, int settleMs, int charDelayMs)
    {
        var pagePath = Path.Combine(AppContext.BaseDirectory, "testpage.html");
        if (!File.Exists(pagePath))
        {
            // Fall back to the source tree when run via `dotnet run`.
            var alt = Path.Combine(Directory.GetCurrentDirectory(), "testpage.html");
            if (File.Exists(alt)) pagePath = alt;
            else
            {
                Console.Error.WriteLine($"FATAL: testpage.html not found at {pagePath}");
                return 2;
            }
        }

        Console.WriteLine($"MODE A — fidelity   method={method}   runs={runs}   " +
                          $"settle={settleMs}ms   char-delay={charDelayMs}ms");
        Console.WriteLine(new string('-', 78));

        // Build the run order up front, guaranteeing every password in
        // TestPasswords appears at least once before any repeats.
        if (runs < TestPasswords.Length)
        {
            Console.WriteLine($"NOTE: --runs {runs} is fewer than the {TestPasswords.Length} test " +
                              $"passwords. Raising to {TestPasswords.Length} so every password gets " +
                              "at least one run -- a fidelity test that never tries a password proves nothing about it.");
            runs = TestPasswords.Length;
        }

        var order = new List<(string Label, string Value)>(TestPasswords);
        for (var i = 0; order.Count < runs; i++)
            order.Add(TestPasswords[i % TestPasswords.Length]);

        var results = new List<Result>();

        foreach (var (label, value) in order)
        {
            var r = FidelityRun(chromePath, pagePath, label, value, method, settleMs, charDelayMs);
            results.Add(r);
            Console.WriteLine(
                $"  {results.Count,3}. {label,-18} {(r.Success ? "PASS" : "FAIL"),-4}  " +
                $"ready={r.WindowReadyMs,5}ms  {r.Detail}");
        }

        Console.WriteLine();
        SummariseByLabel(results);
        var csv = WriteCsv(results, $"fidelity_{method}_d{charDelayMs}");
        Console.WriteLine($"\nCSV: {csv}");

        var failed = results.Count(r => !r.Success);
        Console.WriteLine(failed == 0
            ? "\nVERDICT: all characters survived injection."
            : $"\nVERDICT: {failed}/{results.Count} runs corrupted the password.");
        return 0;
    }

    private static Result FidelityRun(
        string chromePath, string pagePath, string label, string password, string method,
        int settleMs, int charDelayMs)
    {
        var url = "file:///" + pagePath.Replace('\\', '/') + "#expected=" + Uri.EscapeDataString(password);
        using var session = ChromeSession.Launch(chromePath, url, forceRendererAccessibility: false);

        var ready = session.WaitForForegroundWindow(
            t => t.StartsWith("SPIKE:", StringComparison.Ordinal),
            TimeSpan.FromSeconds(30));

        if (ready is null)
            return new Result(label, password.Length, method, false, -1, "WINDOW_NOT_READY");

        // Let the render settle so the autofocused field is genuinely accepting input.
        Thread.Sleep(settleMs);

        var readyMs = (int)ready.Value.TotalMilliseconds;

        var blocked = NativeMethods.BlockInput(true);
        string? thrown = null;
        try
        {
            if (method == "sendkeys")
                SendKeys.SendWait(password);      // reserved chars parsed as syntax
            else
                NativeMethods.SendUnicodeString(password, charDelayMs);
        }
        catch (Exception ex)
        {
            thrown = $"{ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            if (blocked) NativeMethods.BlockInput(false);
        }

        if (thrown is not null)
            return new Result(label, password.Length, method, false, readyMs, $"SENDKEYS_THREW: {thrown}");

        var title = session.WaitForTitle(
            t => t.StartsWith("SPIKE:PASS", StringComparison.Ordinal) ||
                 t.StartsWith("SPIKE:FAIL", StringComparison.Ordinal),
            TimeSpan.FromSeconds(5));

        if (title is null)
            return new Result(label, password.Length, method, false, readyMs, "NO_VERDICT_TIMEOUT");

        var ok = title.StartsWith("SPIKE:PASS", StringComparison.Ordinal);
        var detail = ok ? "" : title.Replace("SPIKE:FAIL:", "");
        if (!blocked && ok) detail = "blockinput_denied";

        return new Result(label, password.Length, method, ok, readyMs, detail);
    }

    // ------------------------------------------------------------------
    // Mode B — real Chrome cold-start and window-detection latency against
    // Google. Deliberately injects nothing: this measures the race the PRD's
    // 1,500 ms sleep loses, without touching a real account.
    // ------------------------------------------------------------------
    private static int RunTiming(string chromePath, int runs, string urlTemplate, string loginHint)
    {
        var url = urlTemplate.Contains("{0}", StringComparison.Ordinal)
            ? string.Format(CultureInfo.InvariantCulture, urlTemplate, Uri.EscapeDataString(loginHint))
            : urlTemplate;

        Console.WriteLine($"MODE B — timing   runs={runs}");
        Console.WriteLine($"URL: {url}");
        Console.WriteLine("No keystrokes are sent in this mode.");
        Console.WriteLine(new string('-', 78));

        var results = new List<Result>();

        for (var i = 0; i < runs; i++)
        {
            using var session = ChromeSession.Launch(chromePath, url, forceRendererAccessibility: false);

            var ready = session.WaitForForegroundWindow(
                t => !IsBlankChromeTitle(t) &&
                     (t.Contains("Sign in", StringComparison.OrdinalIgnoreCase) ||
                      t.Contains("Log masuk", StringComparison.OrdinalIgnoreCase) ||
                      t.Contains("DELIMa", StringComparison.OrdinalIgnoreCase) ||
                      t.Contains("Google Account", StringComparison.OrdinalIgnoreCase) ||
                      t.Contains("Akaun Google", StringComparison.OrdinalIgnoreCase)),
                TimeSpan.FromSeconds(45));

            var ms = ready is null ? -1 : (int)ready.Value.TotalMilliseconds;
            var title = NativeMethods.GetForegroundTitle();
            var ok = ready is not null;

            results.Add(new Result("google-signin", 0, "none", ok, ms, ok ? title : "TIMEOUT"));
            Console.WriteLine($"  {i + 1,3}. {(ok ? "OK  " : "FAIL")}  ready={ms,6}ms  {title}");

            Thread.Sleep(1000); // avoid tripping Google's rate limiting
        }

        Console.WriteLine();
        var good = results.Where(r => r.Success).Select(r => r.WindowReadyMs).OrderBy(x => x).ToList();
        if (good.Count > 0)
        {
            Console.WriteLine($"  window-ready p50 : {Percentile(good, 0.50),6} ms");
            Console.WriteLine($"  window-ready p95 : {Percentile(good, 0.95),6} ms");
            Console.WriteLine($"  window-ready max : {good[^1],6} ms");
            Console.WriteLine();
            var over1500 = good.Count(x => x > 1500);
            Console.WriteLine($"  runs exceeding the PRD's 1,500 ms assumption: {over1500}/{good.Count} " +
                              $"({100.0 * over1500 / good.Count:F0}%)");
            Console.WriteLine("  Each of those would have injected into whatever held focus instead.");
        }
        Console.WriteLine($"  timeouts: {results.Count(r => !r.Success)}/{results.Count}");

        var csv = WriteCsv(results, "timing");
        Console.WriteLine($"\nCSV: {csv}");
        return 0;
    }

    // ------------------------------------------------------------------
    // Mode C — UI Automation Verification Probe (T0.4)
    // Purely observational: an operator drives Chrome by hand; the probe polls
    // every 100 ms and records focus_resolvable and is_password properties.
    // Never automates sign-in or types credentials.
    // ------------------------------------------------------------------
    private static int RunUia(string chromePath, int runs, string url, bool noAccessibility)
    {
        Console.WriteLine($"MODE C — uia (T0.4 probe)   runs={runs}   force-accessibility={!noAccessibility}");
        Console.WriteLine($"URL: {url}");
        Console.WriteLine();
        Console.WriteLine("OPERATOR INSTRUCTIONS:");
        Console.WriteLine("  1. Probe launches Chrome at the entry URL.");
        Console.WriteLine("  2. Click the sign-in button.");
        Console.WriteLine("  3. Wait on identifier page (2s) -> Type your real email -> Press Enter.");
        Console.WriteLine("  4. Wait on password page (2s). DO NOT TYPE A PASSWORD.");
        Console.WriteLine("  5. Close the Chrome window. Probe will log the run and relaunch.");
        Console.WriteLine(new string('-', 78));

        var csvPath = ResolveSpikeResultsPath($"uia_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        var directory = Path.GetDirectoryName(csvPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var writer = new StreamWriter(csvPath, false, Encoding.UTF8);
        writer.WriteLine("run,elapsed_ms,window_title,focus_resolvable,is_password");
        writer.Flush();

        var totalSamples = 0;
        var totalResolvedSamples = 0;
        var totalPasswordTrueSamples = 0;

        for (var run = 1; run <= runs; run++)
        {
            Console.WriteLine($"[{run}/{runs}] Launching Chrome...");
            using var session = ChromeSession.Launch(chromePath, url, forceRendererAccessibility: !noAccessibility);
            var sw = Stopwatch.StartNew();
            var runSamples = 0;

            while (!session.Process.HasExited)
            {
                var elapsedMs = sw.ElapsedMilliseconds;
                var title = NativeMethods.GetForegroundTitle();
                var (focusResolvable, isPassword) = UiaHelper.ProbeFocusedElementPassword();

                var isPasswordStr = focusResolvable && isPassword.HasValue
                    ? (isPassword.Value ? "true" : "false")
                    : "";

                var escapedTitle = title.Replace("\"", "\"\"");
                writer.WriteLine($"{run},{elapsedMs},\"{escapedTitle}\",{(focusResolvable ? "true" : "false")},{isPasswordStr}");
                writer.Flush();

                runSamples++;
                totalSamples++;
                if (focusResolvable) totalResolvedSamples++;
                if (focusResolvable && isPassword == true) totalPasswordTrueSamples++;

                Thread.Sleep(100);
            }

            Console.WriteLine($"[{run}/{runs}] Window closed. Recorded {runSamples} samples ({sw.ElapsedMilliseconds} ms).");

            if (run < runs)
            {
                Thread.Sleep(500);
            }
        }

        Console.WriteLine();
        Console.WriteLine("==============================================================================");
        Console.WriteLine("T0.4 UIA PROBE BATCH COMPLETE");
        Console.WriteLine("==============================================================================");
        Console.WriteLine($"Total runs completed    : {runs}");
        Console.WriteLine($"Total samples recorded  : {totalSamples}");
        Console.WriteLine($"Focus resolvable samples: {totalResolvedSamples}");
        Console.WriteLine($"IsPassword=true samples : {totalPasswordTrueSamples}");
        Console.WriteLine($"CSV Output              : {csvPath}");
        Console.WriteLine();
        Console.WriteLine("Refer to Visual_SSO/T0.4_UIA_Verification.md Part 3 to evaluate results:");
        Console.WriteLine("  Q1. Focus resolvable on identifier page (>= 49/50)");
        Console.WriteLine("  Q2. IsPassword == false on identifier page (50/50 - no false positives)");
        Console.WriteLine("  Q3. Focus resolvable on password page (>= 49/50)");
        Console.WriteLine("  Q4. IsPassword == true on password page (50/50 - no tolerance)");
        Console.WriteLine("  Q5. Settle latency from page load to property readable (p50/p95)");
        Console.WriteLine("  Q6. Accessibility flag overhead (compare with --no-accessibility)");
        Console.WriteLine("==============================================================================");

        return 0;
    }

    /// <summary>
    /// A blank/loading Chrome window still carries "Google Chrome" in its
    /// title, which is why the ready-check must not match on that string alone.
    /// Covers English and BM variants of the empty states seen in practice.
    /// </summary>
    private static bool IsBlankChromeTitle(string t)
    {
        if (string.IsNullOrWhiteSpace(t)) return true;
        var t2 = t.Trim();
        return t2.Equals("Untitled - Google Chrome", StringComparison.OrdinalIgnoreCase)
            || t2.Equals("New Tab - Google Chrome", StringComparison.OrdinalIgnoreCase)
            || t2.Equals("Tab Baharu - Google Chrome", StringComparison.OrdinalIgnoreCase)
            || t2.Equals("Untitled", StringComparison.OrdinalIgnoreCase)
            || t2.Equals("Google Chrome", StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------

    private record Result(
        string Label, int PasswordLength, string Method, bool Success, int WindowReadyMs, string Detail);

    private static void SummariseByLabel(List<Result> results)
    {
        Console.WriteLine("SUMMARY BY PASSWORD");
        Console.WriteLine(new string('-', 78));
        foreach (var g in results.GroupBy(r => r.Label))
        {
            var pass = g.Count(r => r.Success);
            var total = g.Count();
            var flag = pass == total ? "" : "   <-- CORRUPTED";
            Console.WriteLine($"  {g.Key,-18} {pass,3}/{total,-3} passed{flag}");
        }
    }

    private static int Percentile(List<int> sorted, double p)
    {
        if (sorted.Count == 0) return -1;
        var idx = (int)Math.Ceiling(p * sorted.Count) - 1;
        return sorted[Math.Clamp(idx, 0, sorted.Count - 1)];
    }

    private static string WriteCsv(List<Result> results, string prefix)
    {
        var path = ResolveSpikeResultsPath($"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var w = new StreamWriter(path, false, Encoding.UTF8);
        w.WriteLine("run,label,password_length,method,success,window_ready_ms,detail,machine,os");
        var machine = Environment.MachineName;
        var os = Environment.OSVersion.VersionString;
        for (var i = 0; i < results.Count; i++)
        {
            var r = results[i];
            w.WriteLine($"{i + 1},{r.Label},{r.PasswordLength},{r.Method},{r.Success}," +
                        $"{r.WindowReadyMs},\"{r.Detail}\",{machine},\"{os}\"");
        }
        return path;
    }

    private static string ResolveSpikeResultsPath(string filename)
    {
        var cur = Directory.GetCurrentDirectory();
        if (Directory.Exists(Path.Combine(cur, "spike-results")))
        {
            return Path.Combine(cur, "spike-results", filename);
        }

        if (Directory.Exists(Path.Combine(cur, "..", "spike-results")))
        {
            return Path.GetFullPath(Path.Combine(cur, "..", "spike-results", filename));
        }

        var localSpike = Path.Combine(cur, "spike-results");
        return Path.Combine(localSpike, filename);
    }

    private static int ParseIntArg(string[] args, string name, int fallback)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out var v) ? v : fallback;
    }

    private static string ParseStringArg(string[] args, string name, string fallback)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
    }

    private static int PrintHelp()
    {
        Console.WriteLine("""
            DELIMa injection and UIA verification spike (T0.3 / T0.4)

              dotnet run -- fidelity --method sendinput --runs 50
              dotnet run -- fidelity --method sendkeys  --runs 50
              dotnet run -- timing   --runs 50 --url https://d3.delima.edu.my
              dotnet run -- uia      --runs 50 --url https://d3.delima.edu.my/landing
              dotnet run -- uia      --runs 50 --no-accessibility

            fidelity          Mode A: Local page. Verifies every character survives injection.
                              Run both methods and compare. No network, no account.

            timing            Mode B: Real Google sign-in page. Measures cold-start and
                              window-detection latency. Sends no keystrokes.

            uia               Mode C: UI Automation verification probe (T0.4).
                              Observes whether Chrome reports IsPassword reliably.
                              Driven manually by operator; no automated keystrokes or stored credentials.

            Options
              --runs N            number of runs                (default 50)
              --method M          sendinput | sendkeys          (default sendinput)
              --settle MS         pause after window ready      (default 400)
              --char-delay MS     gap between characters        (default 0)
                                  Raise to 10-20 if fast injection drops characters on lab hardware.
              --url U             entry URL (timing / uia mode)
              --login-hint E      address substituted into --url (timing mode)
              --no-accessibility  omit --force-renderer-accessibility in uia mode (for baseline timing)

            Run on representative lab hardware, not a developer machine or RDP session.
            """);
        return 1;
    }
}
