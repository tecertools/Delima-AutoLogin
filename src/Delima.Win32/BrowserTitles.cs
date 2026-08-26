namespace Delima.Win32;

/// <summary>
/// Per-browser window title definitions for Route C visual SSO per Technical Architecture §4.4.1.
/// Titles are matched using exact Ordinal equality against per-browser lists (§4.2) and normalized fallbacks.
/// </summary>
public static class BrowserTitles
{
    public static class Chrome
    {
        public static readonly IReadOnlyList<string> Identifier = new[]
        {
            // English variants (T0.4 measured at index 0 and 1)
            "Sign in - Google Accounts - Google Chrome",
            "Sign in \u2013 Google accounts - Google Chrome",
            "Sign in - Google Accounts - Chrome",

            // Malay verb + English noun variants (empirically confirmed: "Log masuk - Google Accounts")
            "Log masuk - Google Accounts - Google Chrome",
            "Log masuk \u2013 Google Accounts - Google Chrome",
            "Log masuk \u2014 Google Accounts - Google Chrome",
            "Log masuk - Google Accounts - Chrome",

            // Malay verb + Malay noun variants
            "Log masuk - Akaun Google - Google Chrome",
            "Log masuk \u2013 Akaun Google - Google Chrome",
            "Log masuk \u2014 Akaun Google - Google Chrome",
            "Log Masuk - Akaun Google - Google Chrome",
            "Log Masuk \u2013 Akaun Google - Google Chrome",
            "Log Masuk \u2014 Akaun Google - Google Chrome",
            "Log masuk - Akaun Google - Chrome"
        };

        public static readonly IReadOnlyList<string> Consent = Identifier;

        public static readonly IReadOnlyList<string> Destination = new[]
        {
            "DELIMa 3.0 Digital Educational Learning Initiative Malaysia - Google Chrome",
            "DELIMa - Google Chrome",
            "DELIMa 3.0 - Google Chrome",
            "Classes - Google Classroom - Google Chrome",
            "Google Classroom - Google Chrome",
            "Kelas - Google Classroom - Google Chrome",
            "AINS \u2014 Advanced Integrated NILAM System | Kementerian Pendidikan Malaysia - Google Chrome",
            "AINS - Advanced Integrated NILAM System | Kementerian Pendidikan Malaysia - Google Chrome",
            "AINS \u2013 Advanced Integrated NILAM System | Kementerian Pendidikan Malaysia - Google Chrome",
            "AINS - Google Chrome",
            "AINS \u2014 Advanced Integrated NILAM System - Google Chrome",
            "AINS \u2013 Advanced Integrated NILAM System - Google Chrome"
        };
    }

    public static class Edge
    {
        // Empirically measured across Windows builds and locales.
        // Covers both zero-width space (\u200b) and standard space in "Microsoft Edge",
        // English and Malay (Bahasa Melayu), and common profile suffixes.
        public static readonly IReadOnlyList<string> Identifier = new[]
        {
            // Edge English (with zero-width space \u200b)
            "Sign in - Google Accounts - Personal - Microsoft\u200b Edge",
            "Sign in - Google Accounts - Profile 1 - Microsoft\u200b Edge",
            "Sign in - Google Accounts - Profile 2 - Microsoft\u200b Edge",
            "Sign in - Google Accounts - Microsoft\u200b Edge",
            "Sign in \u2013 Google accounts - Personal - Microsoft\u200b Edge",
            "Sign in \u2013 Google accounts - Profile 1 - Microsoft\u200b Edge",
            "Sign in \u2013 Google accounts - Microsoft\u200b Edge",
            "Sign in \u2014 Google Accounts - Personal - Microsoft\u200b Edge",
            "Sign in \u2014 Google Accounts - Microsoft\u200b Edge",
            "Sign in - Google Accounts - InPrivate - Microsoft\u200b Edge",

            // Edge English (standard space)
            "Sign in - Google Accounts - Personal - Microsoft Edge",
            "Sign in - Google Accounts - Profile 1 - Microsoft Edge",
            "Sign in - Google Accounts - Profile 2 - Microsoft Edge",
            "Sign in - Google Accounts - Microsoft Edge",
            "Sign in \u2013 Google accounts - Personal - Microsoft Edge",
            "Sign in \u2013 Google accounts - Profile 1 - Microsoft Edge",
            "Sign in \u2013 Google accounts - Microsoft Edge",
            "Sign in \u2014 Google Accounts - Personal - Microsoft Edge",
            "Sign in \u2014 Google Accounts - Microsoft Edge",
            "Sign in - Google Accounts - InPrivate - Microsoft Edge",

            // Edge Malay mixed-language (verb BM + noun English) — EMPIRICALLY CONFIRMED
            "Log masuk - Google Accounts - Personal - Microsoft\u200b Edge",
            "Log masuk - Google Accounts - Profile 1 - Microsoft\u200b Edge",
            "Log masuk - Google Accounts - Microsoft\u200b Edge",
            "Log masuk \u2013 Google Accounts - Personal - Microsoft\u200b Edge",
            "Log masuk \u2013 Google Accounts - Microsoft\u200b Edge",
            "Log masuk \u2014 Google Accounts - Personal - Microsoft\u200b Edge",
            "Log masuk \u2014 Google Accounts - Microsoft\u200b Edge",

            // Edge Malay full-BM (with zero-width space \u200b)
            "Log masuk - Akaun Google - Personal - Microsoft\u200b Edge",
            "Log masuk - Akaun Google - Profile 1 - Microsoft\u200b Edge",
            "Log masuk - Akaun Google - Profile 2 - Microsoft\u200b Edge",
            "Log masuk - Akaun Google - Microsoft\u200b Edge",
            "Log masuk \u2013 Akaun Google - Personal - Microsoft\u200b Edge",
            "Log masuk \u2013 Akaun Google - Profile 1 - Microsoft\u200b Edge",
            "Log masuk \u2013 Akaun Google - Microsoft\u200b Edge",
            "Log masuk \u2014 Akaun Google - Personal - Microsoft\u200b Edge",
            "Log masuk \u2014 Akaun Google - Microsoft\u200b Edge",
            "Log Masuk - Akaun Google - Personal - Microsoft\u200b Edge",
            "Log Masuk - Akaun Google - Profile 1 - Microsoft\u200b Edge",
            "Log Masuk - Akaun Google - Microsoft\u200b Edge",
            "Log Masuk \u2013 Akaun Google - Personal - Microsoft\u200b Edge",
            "Log Masuk \u2013 Akaun Google - Microsoft\u200b Edge",
            "Log masuk - Akaun Google - InPrivate - Microsoft\u200b Edge",

            // Edge Malay mixed-language (standard space) — EMPIRICALLY CONFIRMED
            "Log masuk - Google Accounts - Personal - Microsoft Edge",
            "Log masuk - Google Accounts - Profile 1 - Microsoft Edge",
            "Log masuk - Google Accounts - Microsoft Edge",
            "Log masuk \u2013 Google Accounts - Personal - Microsoft Edge",
            "Log masuk \u2013 Google Accounts - Microsoft Edge",
            "Log masuk \u2014 Google Accounts - Personal - Microsoft Edge",
            "Log masuk \u2014 Google Accounts - Microsoft Edge",

            // Edge Malay full-BM (standard space)
            "Log masuk - Akaun Google - Personal - Microsoft Edge",
            "Log masuk - Akaun Google - Profile 1 - Microsoft Edge",
            "Log masuk - Akaun Google - Profile 2 - Microsoft Edge",
            "Log masuk - Akaun Google - Microsoft Edge",
            "Log masuk \u2013 Akaun Google - Personal - Microsoft Edge",
            "Log masuk \u2013 Akaun Google - Profile 1 - Microsoft Edge",
            "Log masuk \u2013 Akaun Google - Microsoft Edge",
            "Log masuk \u2014 Akaun Google - Personal - Microsoft Edge",
            "Log masuk \u2014 Akaun Google - Microsoft Edge",
            "Log Masuk - Akaun Google - Personal - Microsoft Edge",
            "Log Masuk - Akaun Google - Profile 1 - Microsoft Edge",
            "Log Masuk - Akaun Google - Microsoft Edge",
            "Log Masuk \u2013 Akaun Google - Personal - Microsoft Edge",
            "Log Masuk \u2013 Akaun Google - Microsoft Edge",
            "Log masuk - Akaun Google - InPrivate - Microsoft Edge"
        };

        public static readonly IReadOnlyList<string> Consent = Identifier;

        public static readonly IReadOnlyList<string> Destination = new[]
        {
            // DELIMa (with \u200b and without)
            "DELIMa 3.0 Digital Educational Learning Initiative Malaysia - Personal - Microsoft\u200b Edge",
            "DELIMa 3.0 Digital Educational Learning Initiative Malaysia - Personal - Microsoft Edge",
            "DELIMa 3.0 Digital Educational Learning Initiative Malaysia - Profile 1 - Microsoft\u200b Edge",
            "DELIMa 3.0 Digital Educational Learning Initiative Malaysia - Profile 1 - Microsoft Edge",
            "DELIMa 3.0 Digital Educational Learning Initiative Malaysia - Microsoft\u200b Edge",
            "DELIMa 3.0 Digital Educational Learning Initiative Malaysia - Microsoft Edge",
            "DELIMa - Personal - Microsoft\u200b Edge",
            "DELIMa - Personal - Microsoft Edge",
            "DELIMa 3.0 - Personal - Microsoft\u200b Edge",
            "DELIMa 3.0 - Personal - Microsoft Edge",
            "DELIMa - Microsoft\u200b Edge",
            "DELIMa - Microsoft Edge",
            "DELIMa 3.0 - Microsoft\u200b Edge",
            "DELIMa 3.0 - Microsoft Edge",

            // Google Classroom (with \u200b and without)
            "Classes - Google Classroom - Personal - Microsoft\u200b Edge",
            "Classes - Google Classroom - Personal - Microsoft Edge",
            "Google Classroom - Personal - Microsoft\u200b Edge",
            "Google Classroom - Personal - Microsoft Edge",
            "Classes - Google Classroom - Microsoft\u200b Edge",
            "Classes - Google Classroom - Microsoft Edge",
            "Google Classroom - Microsoft\u200b Edge",
            "Google Classroom - Microsoft Edge",
            "Kelas - Google Classroom - Personal - Microsoft\u200b Edge",
            "Kelas - Google Classroom - Personal - Microsoft Edge",
            "Kelas - Google Classroom - Microsoft\u200b Edge",
            "Kelas - Google Classroom - Microsoft Edge",

            // AINS (with \u200b and without)
            "AINS \u2014 Advanced Integrated NILAM System | Kementerian Pendidikan Malaysia - Personal - Microsoft\u200b Edge",
            "AINS \u2014 Advanced Integrated NILAM System | Kementerian Pendidikan Malaysia - Personal - Microsoft Edge",
            "AINS \u2014 Advanced Integrated NILAM System | Kementerian Pendidikan Malaysia - Profile 1 - Microsoft\u200b Edge",
            "AINS \u2014 Advanced Integrated NILAM System | Kementerian Pendidikan Malaysia - Profile 1 - Microsoft Edge",
            "AINS \u2014 Advanced Integrated NILAM System | Kementerian Pendidikan Malaysia - Microsoft\u200b Edge",
            "AINS \u2014 Advanced Integrated NILAM System | Kementerian Pendidikan Malaysia - Microsoft Edge",
            "AINS - Advanced Integrated NILAM System | Kementerian Pendidikan Malaysia - Microsoft\u200b Edge",
            "AINS - Advanced Integrated NILAM System | Kementerian Pendidikan Malaysia - Microsoft Edge",
            "AINS \u2013 Advanced Integrated NILAM System | Kementerian Pendidikan Malaysia - Microsoft\u200b Edge",
            "AINS \u2013 Advanced Integrated NILAM System | Kementerian Pendidikan Malaysia - Microsoft Edge",
            "AINS - Personal - Microsoft\u200b Edge",
            "AINS - Personal - Microsoft Edge",
            "AINS - Microsoft\u200b Edge",
            "AINS - Microsoft Edge",
            "AINS \u2014 Advanced Integrated NILAM System - Microsoft\u200b Edge",
            "AINS \u2014 Advanced Integrated NILAM System - Microsoft Edge",
            "AINS \u2013 Advanced Integrated NILAM System - Microsoft\u200b Edge",
            "AINS \u2013 Advanced Integrated NILAM System - Microsoft Edge"
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
