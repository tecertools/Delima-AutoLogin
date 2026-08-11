using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

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
        var timingUrl = ParseStringArg(args, "--url", DefaultTimingUrl);

        var chromePath = ChromeSession.ResolveChromePath();
        if (chromePath is null)
        {
            Console.Error.WriteLine("FATAL: chrome.exe not found via registry or well-known paths.");
            Console.Error.WriteLine("       This is itself a finding — the PRD hardcodes one path.");
            return 2;
        }
        Console.WriteLine($"Chrome: {chromePath}");
        Console.WriteLine();

        return mode switch
        {
            "fidelity" => RunFidelity(chromePath, runs, method, settleMs, charDelayMs),
            "timing"   => RunTiming(chromePath, runs, timingUrl, hint),
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

        var results = new List<Result>();
        var perPassword = runs / TestPasswords.Length + 1;

        foreach (var (label, value) in TestPasswords)
        {
            for (var i = 0; i < perPassword && results.Count < runs; i++)
            {
                var r = FidelityRun(chromePath, pagePath, label, value, method, settleMs, charDelayMs);
                results.Add(r);
                Console.WriteLine(
                    $"  {results.Count,3}. {label,-18} {(r.Success ? "PASS" : "FAIL"),-4}  " +
                    $"ready={r.WindowReadyMs,5}ms  {r.Detail}");
            }
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
        using var session = ChromeSession.Launch(chromePath, url);

        var ready = session.WaitForForegroundWindow(
            t => t.StartsWith("SPIKE:", StringComparison.Ordinal),
            TimeSpan.FromSeconds(30));

        if (ready is null)
            return new Result(label, password.Length, method, false, -1, "WINDOW_NOT_READY");

        // Let the render settle so the autofocused field is genuinely accepting
        // input. Production must do the same, but keyed off a verified window
        // rather than a blind sleep.
        Thread.Sleep(settleMs);

        var blocked = NativeMethods.BlockInput(true);
        try
        {
            if (method == "sendkeys")
                SendKeys.SendWait(password);      // reserved chars parsed as syntax
            else
                NativeMethods.SendUnicodeString(password, charDelayMs);
        }
        finally
        {
            if (blocked) NativeMethods.BlockInput(false);
        }

        var title = session.WaitForTitle(
            t => t.StartsWith("SPIKE:PASS", StringComparison.Ordinal) ||
                 t.StartsWith("SPIKE:FAIL", StringComparison.Ordinal),
            TimeSpan.FromSeconds(5));

        var readyMs = (int)ready.Value.TotalMilliseconds;

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
            using var session = ChromeSession.Launch(chromePath, url);

            var ready = session.WaitForForegroundWindow(
                t => t.Contains("Google", StringComparison.OrdinalIgnoreCase) ||
                     t.Contains("Sign in", StringComparison.OrdinalIgnoreCase) ||
                     t.Contains("Log masuk", StringComparison.OrdinalIgnoreCase) ||
                     t.Contains("DELIMa", StringComparison.OrdinalIgnoreCase),
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
        var path = Path.Combine(
            Directory.GetCurrentDirectory(),
            $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

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
            DELIMa injection spike (T0.3)

              dotnet run -- fidelity --method sendinput --runs 50
              dotnet run -- fidelity --method sendkeys  --runs 50
              dotnet run -- timing   --runs 50 --url https://d3.delima.edu.my

            fidelity   Local page. Verifies every character survives injection.
                       Run both methods and compare. No network, no account.

            timing     Real Google sign-in page. Measures cold-start and
                       window-detection latency. Sends no keystrokes.

            Options
              --runs N       number of runs            (default 50)
              --method M     sendinput | sendkeys      (default sendinput)
              --settle MS    pause after window ready  (default 400)
              --char-delay MS  gap between characters  (default 0)
                             Raise to 10-20 if fast injection drops characters
                             on lab hardware. Whatever value produces a clean
                             sweep becomes the production setting.
              --url U        entry URL for timing mode; {0} is replaced with
                             the encoded login hint if present
              --login-hint E address substituted into --url

            Run on representative lab hardware, not a developer machine.
            Do not leave the desk during a run: it drives the real keyboard.
            """);
        return 1;
    }
}
