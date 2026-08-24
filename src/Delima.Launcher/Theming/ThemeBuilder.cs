using System.Windows;
using System.Windows.Media;
using Delima.Core.Store;

namespace Delima.Launcher.Theming;

/// <summary>
/// Result of WCAG contrast and theme constraint validation.
/// </summary>
public sealed class ThemeValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; } = [];
    public List<string> Warnings { get; } = [];
}

/// <summary>
/// Runtime theme builder that converts ThemeInfo configuration data into WPF ResourceDictionary brushes
/// with WCAG 2.2 AA (4.5:1) contrast enforcement and fallback handling.
/// </summary>
public static class ThemeBuilder
{
    // Neutral default palette per PROMPT.txt and PRD §7.1
    public const string DefaultPageBackground = "#FFFDF7";
    public const string DefaultSurface = "#FFFFFF";
    public const string DefaultSoftSurface = "#FDF9DC";
    public const string DefaultPrimaryText = "#1A1208";
    public const string DefaultSecondaryText = "#5C5344";
    public const string DefaultBorder = "#E5DCC8";
    public const string DefaultPrimary = "#056839";
    public const string DefaultAccent = "#F7941D";
    public const string DefaultAlert = "#ED1B24";

    public static readonly string[] DefaultClassColours =
    [
        "#C41118", "#9E2B0E", "#A85200", "#8A6100",
        "#056839", "#2F6B12", "#0A6265", "#7A4A21"
    ];

    /// <summary>
    /// Validates theme colors against WCAG 2.2 AA (4.5:1 for standard text) and PROMPT.txt rules.
    /// </summary>
    public static ThemeValidationResult ValidateTheme(ThemeInfo theme)
    {
        var result = new ThemeValidationResult();

        if (string.IsNullOrWhiteSpace(theme.Primary))
        {
            result.Errors.Add("Primary colour is required.");
        }
        if (string.IsNullOrWhiteSpace(theme.Accent))
        {
            result.Errors.Add("Accent colour is required.");
        }

        if (!TryParseColor(theme.Primary, out var primaryColor))
        {
            result.Errors.Add($"Invalid primary colour format: {theme.Primary}");
        }
        else
        {
            // Primary green uses white text: must meet >= 4.5:1
            double whiteOnPrimary = CalculateContrastRatio(Colors.White, primaryColor);
            if (whiteOnPrimary < 4.5)
            {
                result.Errors.Add($"White text on primary colour ({theme.Primary}) has contrast ratio {whiteOnPrimary:F1}:1 (requires >= 4.5:1).");
            }
        }

        if (!TryParseColor(theme.Accent, out var accentColor))
        {
            result.Errors.Add($"Invalid accent colour format: {theme.Accent}");
        }
        else
        {
            // PROMPT.txt: Never place white text on Accent (#F7941D) - must use dark text (#1A1208)
            TryParseColor(DefaultPrimaryText, out var darkText);
            double darkOnAccent = CalculateContrastRatio(darkText, accentColor);
            if (darkOnAccent < 4.5)
            {
                result.Errors.Add($"Dark text on accent colour ({theme.Accent}) has contrast ratio {darkOnAccent:F1}:1 (requires >= 4.5:1).");
            }
        }

        // Validate class colours
        IReadOnlyList<string> classColours = (theme.ClassColours != null && theme.ClassColours.Count > 0)
            ? theme.ClassColours
            : DefaultClassColours;

        for (int i = 0; i < classColours.Count; i++)
        {
            string hex = classColours[i];
            if (!TryParseColor(hex, out var classColor))
            {
                result.Errors.Add($"Class colour [{i}] has invalid format: {hex}");
            }
            else
            {
                // Class colours must carry readable white text (>= 4.5:1)
                double whiteOnClass = CalculateContrastRatio(Colors.White, classColor);
                if (whiteOnClass < 4.5)
                {
                    result.Warnings.Add($"White text on class colour [{i}] ({hex}) has contrast ratio {whiteOnClass:F1}:1.");
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Builds a ResourceDictionary containing theme brush tokens from the given ThemeInfo.
    /// Falls back cleanly to neutral default values if themeInfo is missing or invalid.
    /// </summary>
    public static ResourceDictionary BuildResourceDictionary(ThemeInfo? themeInfo)
    {
        var dict = new ResourceDictionary();

        string primaryHex = DefaultPrimary;
        string accentHex = DefaultAccent;
        var classHexList = new List<string>(DefaultClassColours);

        if (themeInfo != null)
        {
            var validation = ValidateTheme(themeInfo);
            if (validation.IsValid)
            {
                primaryHex = themeInfo.Primary;
                accentHex = themeInfo.Accent;
                if (themeInfo.ClassColours != null && themeInfo.ClassColours.Count > 0)
                {
                    classHexList = [.. themeInfo.ClassColours];
                    while (classHexList.Count < 8)
                    {
                        classHexList.Add(DefaultClassColours[classHexList.Count % DefaultClassColours.Length]);
                    }
                }
            }
        }

        // Core UI tokens
        dict[Tokens.PageBackgroundBrush] = CreateFrozenBrush(DefaultPageBackground);
        dict[Tokens.SurfaceBrush] = CreateFrozenBrush(DefaultSurface);
        dict[Tokens.SoftSurfaceBrush] = CreateFrozenBrush(DefaultSoftSurface);
        dict[Tokens.BorderBrush] = CreateFrozenBrush(DefaultBorder);
        dict[Tokens.PrimaryTextBrush] = CreateFrozenBrush(DefaultPrimaryText);
        dict[Tokens.SecondaryTextBrush] = CreateFrozenBrush(DefaultSecondaryText);
        dict[Tokens.AlertBrush] = CreateFrozenBrush(DefaultAlert);

        // Dynamic branding tokens
        dict[Tokens.PrimaryBrush] = CreateFrozenBrush(primaryHex);
        dict[Tokens.AccentBrush] = CreateFrozenBrush(accentHex);
        dict[Tokens.FocusRingBrush] = CreateFrozenBrush(primaryHex);

        // 8 Class colours
        for (int i = 0; i < 8; i++)
        {
            string hex = i < classHexList.Count ? classHexList[i] : DefaultClassColours[i % DefaultClassColours.Length];
            dict[Tokens.GetClassColourKey(i)] = CreateFrozenBrush(hex);
        }

        return dict;
    }

    /// <summary>
    /// Merges runtime theme brushes into the specified target ResourceDictionary (or Application.Current.Resources).
    /// </summary>
    public static void ApplyTheme(ResourceDictionary targetDictionary, ThemeInfo? themeInfo)
    {
        var themeDict = BuildResourceDictionary(themeInfo);
        foreach (var key in themeDict.Keys)
        {
            targetDictionary[key] = themeDict[key];
        }
    }

    public static SolidColorBrush CreateFrozenBrush(string hex)
    {
        if (!TryParseColor(hex, out var color))
        {
            color = Colors.Gray;
        }

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public static bool TryParseColor(string? hex, out Color color)
    {
        color = Colors.Transparent;
        if (string.IsNullOrWhiteSpace(hex)) return false;

        string clean = hex.Trim();
        if (clean.StartsWith('#'))
        {
            clean = clean[1..];
        }

        try
        {
            if (clean.Length == 6)
            {
                byte r = Convert.ToByte(clean[..2], 16);
                byte g = Convert.ToByte(clean.Substring(2, 2), 16);
                byte b = Convert.ToByte(clean.Substring(4, 2), 16);
                color = Color.FromRgb(r, g, b);
                return true;
            }
            if (clean.Length == 8)
            {
                byte a = Convert.ToByte(clean[..2], 16);
                byte r = Convert.ToByte(clean.Substring(2, 2), 16);
                byte g = Convert.ToByte(clean.Substring(4, 2), 16);
                byte b = Convert.ToByte(clean.Substring(6, 2), 16);
                color = Color.FromArgb(a, r, g, b);
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// Computes WCAG 2.1 relative luminance for an sRGB colour.
    /// </summary>
    public static double CalculateLuminance(Color color)
    {
        double Linearize(double c)
        {
            c /= 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        double r = Linearize(color.R);
        double g = Linearize(color.G);
        double b = Linearize(color.B);

        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    /// <summary>
    /// Computes WCAG contrast ratio between two colours: (L1 + 0.05) / (L2 + 0.05).
    /// </summary>
    public static double CalculateContrastRatio(Color foreground, Color background)
    {
        double l1 = CalculateLuminance(foreground);
        double l2 = CalculateLuminance(background);

        double lighter = Math.Max(l1, l2);
        double darker = Math.Min(l1, l2);

        return (lighter + 0.05) / (darker + 0.05);
    }
}
