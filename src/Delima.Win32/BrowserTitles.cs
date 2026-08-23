namespace Delima.Win32;

/// <summary>
/// Per-browser window title definitions for Route C visual SSO per Technical Architecture §4.4.1.
/// Titles are matched using exact Ordinal equality against per-browser lists (§4.2). Substring matching is strictly forbidden.
/// </summary>
public static class BrowserTitles
{
    public static class Chrome
    {
        public static readonly IReadOnlyList<string> Identifier = new[]
        {
            "Sign in - Google Accounts - Google Chrome",
            "Sign in \u2013 Google accounts - Google Chrome"
        };

        public static readonly IReadOnlyList<string> Consent = new[]
        {
            "Sign in - Google Accounts - Google Chrome",
            "Sign in \u2013 Google accounts - Google Chrome"
        };

        public static readonly IReadOnlyList<string> Destination = new[]
        {
            "DELIMa - Google Chrome",
            "DELIMa 3.0 - Google Chrome",
            "Classes - Google Classroom - Google Chrome",
            "Google Classroom - Google Chrome"
        };
    }

    public static class Edge
    {
        // UNMEASURED — see T0.4_UIA_Verification.md for the 20-run measurement procedure.
        // Left intentionally empty so running against an unmeasured browser fails closed
        // rather than silently matching nothing in a way that looks like a bug (§4.4.1).
        public static readonly IReadOnlyList<string> Identifier = Array.Empty<string>();

        // UNMEASURED — see T0.4_UIA_Verification.md for the 20-run measurement procedure.
        // Left intentionally empty so running against an unmeasured browser fails closed (§4.4.1).
        public static readonly IReadOnlyList<string> Consent = Array.Empty<string>();

        // UNMEASURED — see T0.4_UIA_Verification.md for the 20-run measurement procedure.
        // Left intentionally empty so running against an unmeasured browser fails closed (§4.4.1).
        public static readonly IReadOnlyList<string> Destination = Array.Empty<string>();
    }

    /// <summary>
    /// Returns the exact title lists for the specified browser kind.
    /// </summary>
    public static (IReadOnlyList<string> Identifier, IReadOnlyList<string> Consent, IReadOnlyList<string> Destination) GetTitlesForBrowser(BrowserKind kind) =>
        kind switch
        {
            BrowserKind.Edge => (Edge.Identifier, Edge.Consent, Edge.Destination),
            BrowserKind.Chrome => (Chrome.Identifier, Chrome.Consent, Chrome.Destination),
            _ => (Chrome.Identifier, Chrome.Consent, Chrome.Destination)
        };
}
