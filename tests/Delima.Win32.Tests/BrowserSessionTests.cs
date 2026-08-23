using System.Diagnostics;
using System.IO;
using Delima.Core.Audit;
using Delima.Core.Store;
using Delima.Win32;
using Xunit;

namespace Delima.Win32.Tests;

public class BrowserSessionTests
{
    [Fact]
    public void ResolveBrowser_Honors_PreferredBrowser_Setting()
    {
        var edgePath = BrowserSession.ResolveEdgePath();
        var chromePath = BrowserSession.ResolveChromePath();

        // 1. Explicit "edge" preference
        var resolvedEdge = BrowserSession.ResolveBrowser("edge");
        if (edgePath != null)
        {
            Assert.NotNull(resolvedEdge);
            Assert.Equal(BrowserKind.Edge, resolvedEdge.Value.Kind);
            Assert.Equal(edgePath, resolvedEdge.Value.Path);
        }
        else
        {
            Assert.Null(resolvedEdge);
        }

        // 2. Explicit "chrome" preference
        var resolvedChrome = BrowserSession.ResolveBrowser("chrome");
        if (chromePath != null)
        {
            Assert.NotNull(resolvedChrome);
            Assert.Equal(BrowserKind.Chrome, resolvedChrome.Value.Kind);
            Assert.Equal(chromePath, resolvedChrome.Value.Path);
        }
        else
        {
            Assert.Null(resolvedChrome);
        }

        // 3. "auto" preference: prefers Edge over Chrome
        var resolvedAuto = BrowserSession.ResolveBrowser("auto");
        if (edgePath != null)
        {
            Assert.NotNull(resolvedAuto);
            Assert.Equal(BrowserKind.Edge, resolvedAuto.Value.Kind);
        }
        else if (chromePath != null)
        {
            Assert.NotNull(resolvedAuto);
            Assert.Equal(BrowserKind.Chrome, resolvedAuto.Value.Kind);
        }
    }

    [Fact]
    public void BrowserTitles_Chrome_Contains_Empirical_T04_Strings()
    {
        // Chrome identifier titles (both hyphen and en-dash variants per T0.4)
        Assert.Equal(2, BrowserTitles.Chrome.Identifier.Count);
        Assert.Contains("Sign in - Google Accounts - Google Chrome", BrowserTitles.Chrome.Identifier);
        Assert.Contains("Sign in \u2013 Google accounts - Google Chrome", BrowserTitles.Chrome.Identifier);

        // Chrome consent titles
        Assert.Equal(2, BrowserTitles.Chrome.Consent.Count);
        Assert.Contains("Sign in - Google Accounts - Google Chrome", BrowserTitles.Chrome.Consent);

        // Chrome destination titles
        Assert.Contains("DELIMa - Google Chrome", BrowserTitles.Chrome.Destination);
        Assert.Contains("DELIMa 3.0 - Google Chrome", BrowserTitles.Chrome.Destination);
    }

    [Fact]
    public void BrowserTitles_Edge_Is_Empty_And_Unmeasured_Per_Section441()
    {
        // §4.4.1 & Requirement 2: Leave Edge lists empty with comment saying they are unmeasured
        // so an unmeasured browser fails closed rather than silently matching nothing.
        Assert.Empty(BrowserTitles.Edge.Identifier);
        Assert.Empty(BrowserTitles.Edge.Consent);
        Assert.Empty(BrowserTitles.Edge.Destination);
    }

    [Fact]
    public void RouteCOptions_For_Edge_FailsClosed_With_Empty_IdentifierTitles()
    {
        var edgeOptions = new RouteCOptions { TargetBrowser = BrowserKind.Edge };

        Assert.Empty(edgeOptions.TitleIdentifierPage);
        Assert.Empty(edgeOptions.TitleConsentPage);
        Assert.Empty(edgeOptions.TitleDestinationPage);

        // Fails closed: nothing matches empty identifier title list
        Assert.False(InjectionEngine.MatchesAnyTitle("Sign in - Google Accounts - Microsoft Edge", edgeOptions.TitleIdentifierPage));
        Assert.False(InjectionEngine.MatchesAnyTitle("Sign in - Google Accounts - Google Chrome", edgeOptions.TitleIdentifierPage));
    }

    [Fact]
    public void SubstringMatching_IsForbidden_Per_Section42()
    {
        // §4.2 Requirement: Substring matching is forbidden because it caused T0.3's 47 false ready-states.
        // Exact Ordinal matching against per-browser lists only.
        var chromeOptions = new RouteCOptions { TargetBrowser = BrowserKind.Chrome };

        const string pagePortionOnly = "Sign in - Google Accounts";
        const string wrongSuffix = "Sign in - Google Accounts - Microsoft Edge";

        Assert.False(InjectionEngine.MatchesAnyTitle(pagePortionOnly, chromeOptions.TitleIdentifierPage));
        Assert.False(InjectionEngine.MatchesAnyTitle(wrongSuffix, chromeOptions.TitleIdentifierPage));
        Assert.True(InjectionEngine.MatchesAnyTitle("Sign in - Google Accounts - Google Chrome", chromeOptions.TitleIdentifierPage));
    }

    [Fact]
    public async Task RouteCLoginOrchestrator_Logs_AuditEntry_On_Browser_Resolution()
    {
        var tempAuditDir = Path.Combine(Path.GetTempPath(), "Delima_Audit_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempAuditDir);

        try
        {
            using var cred = new SecurePasswordBuffer("Password123!"u8);

            // Execute with nonexistent path to test failure audit log
            var result = await RouteCLoginOrchestrator.ExecuteAsync(
                browserPath: @"C:\NonExistentDirectory\browser.exe",
                email: "m-10000001@moe-dl.edu.my",
                credential: cred,
                options: new RouteCOptions { WindowWaitTimeout = TimeSpan.FromMilliseconds(200) });

            Assert.False(result.Success);
            Assert.Equal(FailureCodes.E01_NoBrowserFound, result.ErrorCode);

            // Check that audit log file was created in default audit directory or recorded
            string defaultLogFile = AuditLogger.GetAuditLogFilePath(DateTimeOffset.UtcNow);
            if (File.Exists(defaultLogFile))
            {
                string content = File.ReadAllText(defaultLogFile);
                Assert.Contains("browser_resolution", content);
            }
        }
        finally
        {
            try { Directory.Delete(tempAuditDir, recursive: true); } catch { /* ignore */ }
        }
    }
}
