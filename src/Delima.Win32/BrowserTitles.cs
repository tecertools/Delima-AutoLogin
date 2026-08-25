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
            "DELIMa 3.0 Digital Educational Learning Initiative Malaysia - Google Chrome",
            "DELIMa - Google Chrome",
            "DELIMa 3.0 - Google Chrome",
            "Classes - Google Classroom - Google Chrome",
            "Google Classroom - Google Chrome",
            "Kelas - Google Classroom - Google Chrome",                                                           // UNVERIFIED — BM locale, measure in lab
            "AINS \u2014 Advanced Integrated NILAM System | Kementerian Pendidikan Malaysia - Google Chrome",    // UNVERIFIED — measure in lab (em-dash variant)
            "AINS - Advanced Integrated NILAM System | Kementerian Pendidikan Malaysia - Google Chrome",        // UNVERIFIED — measure in lab (hyphen variant)
            "AINS - Google Chrome",                                                                             // UNVERIFIED — measure in lab (short fallback)
            "AINS \u2014 Advanced Integrated NILAM System - Google Chrome"                                      // UNVERIFIED — measure in lab (no KPM suffix)
        };
    }

    public static class Edge
    {
        // Empirically measured via T0.4 Part 6 runs.
        // Note the zero-width space (\u200b) in "Microsoft\u200b Edge" and profile suffixes.
        public static readonly IReadOnlyList<string> Identifier = new[]
        {
            "Sign in - Google Accounts - Personal - Microsoft\u200b Edge",
            "Sign in - Google Accounts - Profile 1 - Microsoft\u200b Edge",
            "Sign in - Google Accounts - Microsoft\u200b Edge",
            "Sign in \u2013 Google accounts - Personal - Microsoft\u200b Edge",
            "Sign in \u2013 Google accounts - Profile 1 - Microsoft\u200b Edge",
            "Sign in \u2013 Google accounts - Microsoft\u200b Edge"
        };

        public static readonly IReadOnlyList<string> Consent = new[]
        {
            "Sign in - Google Accounts - Personal - Microsoft\u200b Edge",
            "Sign in - Google Accounts - Profile 1 - Microsoft\u200b Edge",
            "Sign in - Google Accounts - Microsoft\u200b Edge",
            "Sign in \u2013 Google accounts - Personal - Microsoft\u200b Edge",
            "Sign in \u2013 Google accounts - Profile 1 - Microsoft\u200b Edge",
            "Sign in \u2013 Google accounts - Microsoft\u200b Edge"
        };

        public static readonly IReadOnlyList<string> Destination = new[]
        {
            "DELIMa 3.0 Digital Educational Learning Initiative Malaysia - Personal - Microsoft\u200b Edge",
            "DELIMa 3.0 Digital Educational Learning Initiative Malaysia - Profile 1 - Microsoft\u200b Edge",
            "DELIMa 3.0 Digital Educational Learning Initiative Malaysia - Microsoft\u200b Edge",
            "DELIMa - Personal - Microsoft\u200b Edge",
            "DELIMa 3.0 - Personal - Microsoft\u200b Edge",
            "DELIMa - Microsoft\u200b Edge",
            "DELIMa 3.0 - Microsoft\u200b Edge",
            "Classes - Google Classroom - Personal - Microsoft\u200b Edge",
            "Google Classroom - Personal - Microsoft\u200b Edge",
            "Classes - Google Classroom - Microsoft\u200b Edge",
            "Google Classroom - Microsoft\u200b Edge",
            "Kelas - Google Classroom - Personal - Microsoft\u200b Edge",                                                                    // UNVERIFIED — BM locale, measure in lab
            "Kelas - Google Classroom - Microsoft\u200b Edge",                                                                              // UNVERIFIED — BM locale, measure in lab
            "AINS \u2014 Advanced Integrated NILAM System | Kementerian Pendidikan Malaysia - Personal - Microsoft\u200b Edge",             // UNVERIFIED — measure in lab
            "AINS \u2014 Advanced Integrated NILAM System | Kementerian Pendidikan Malaysia - Profile 1 - Microsoft\u200b Edge",            // UNVERIFIED — measure in lab
            "AINS \u2014 Advanced Integrated NILAM System | Kementerian Pendidikan Malaysia - Microsoft\u200b Edge",                        // UNVERIFIED — measure in lab
            "AINS - Advanced Integrated NILAM System | Kementerian Pendidikan Malaysia - Microsoft\u200b Edge",                            // UNVERIFIED — measure in lab (hyphen variant)
            "AINS - Personal - Microsoft\u200b Edge",                                                                                       // UNVERIFIED — measure in lab (short fallback)
            "AINS - Microsoft\u200b Edge",                                                                                                  // UNVERIFIED — measure in lab (short fallback)
            "AINS \u2014 Advanced Integrated NILAM System - Microsoft\u200b Edge"                                                           // UNVERIFIED — measure in lab (no KPM suffix)
        };
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
