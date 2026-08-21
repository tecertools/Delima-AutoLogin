using System.Globalization;

namespace Delima.Admin.Models;

/// <summary>
/// Implements WCAG 2.2 relative luminance and contrast ratio calculations per PRD §6 Step 1 (FR-S1.4).
/// Requires at least 4.5:1 contrast for text on backgrounds.
/// </summary>
public static class ColorContrastHelper
{
    public const double MinimumContrastRatio = 4.5;

    public static (byte R, byte G, byte B)? ParseHexColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;

        string clean = hex.Trim().TrimStart('#');
        if (clean.Length == 3)
        {
            if (byte.TryParse(clean[0].ToString() + clean[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r) &&
                byte.TryParse(clean[1].ToString() + clean[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g) &&
                byte.TryParse(clean[2].ToString() + clean[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
            {
                return (r, g, b);
            }
        }
        else if (clean.Length == 6)
        {
            if (byte.TryParse(clean.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r) &&
                byte.TryParse(clean.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g) &&
                byte.TryParse(clean.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
            {
                return (r, g, b);
            }
        }

        return null;
    }

    public static double CalculateLuminance(byte r, byte g, byte b)
    {
        double rNorm = NormalizeChannel(r);
        double gNorm = NormalizeChannel(g);
        double bNorm = NormalizeChannel(b);

        return (0.2126 * rNorm) + (0.7152 * gNorm) + (0.0722 * bNorm);
    }

    private static double NormalizeChannel(byte channel)
    {
        double val = channel / 255.0;
        return val <= 0.04045 ? val / 12.92 : Math.Pow((val + 0.055) / 1.055, 2.4);
    }

    public static double CalculateContrastRatio(string hexColor1, string hexColor2)
    {
        var rgb1 = ParseHexColor(hexColor1);
        var rgb2 = ParseHexColor(hexColor2);

        if (!rgb1.HasValue || !rgb2.HasValue) return 1.0;

        double lum1 = CalculateLuminance(rgb1.Value.R, rgb1.Value.G, rgb1.Value.B);
        double lum2 = CalculateLuminance(rgb2.Value.R, rgb2.Value.G, rgb2.Value.B);

        double l1 = Math.Max(lum1, lum2);
        double l2 = Math.Min(lum1, lum2);

        return (l1 + 0.05) / (l2 + 0.05);
    }

    /// <summary>
    /// Evaluates if background hex has sufficient contrast (>= 4.5:1) against standard white (#FFFFFF) or dark text (#1F2421).
    /// Returns the best contrast ratio and whether it passes.
    /// </summary>
    public static ContrastResult EvaluateBestContrast(string bgHex)
    {
        var rgb = ParseHexColor(bgHex);
        if (!rgb.HasValue)
        {
            return new ContrastResult
            {
                IsValidHex = false,
                Ratio = 1.0,
                IsPass = false,
                BestTextColor = "#1F2421",
                Label = "TIDAK SAH"
            };
        }

        double ratioWhite = CalculateContrastRatio(bgHex, "#FFFFFF");
        double ratioDark = CalculateContrastRatio(bgHex, "#1F2421");

        bool useWhite = ratioWhite >= ratioDark;
        double bestRatio = useWhite ? ratioWhite : ratioDark;
        string bestText = useWhite ? "#FFFFFF" : "#1F2421";
        bool isPass = bestRatio >= MinimumContrastRatio;

        string label = isPass
            ? $"OK {bestRatio:F1}:1"
            : $"GAGAL {bestRatio:F1}:1";

        return new ContrastResult
        {
            IsValidHex = true,
            Ratio = bestRatio,
            IsPass = isPass,
            BestTextColor = bestText,
            Label = label
        };
    }
}

public sealed class ContrastResult
{
    public bool IsValidHex { get; init; }
    public double Ratio { get; init; }
    public bool IsPass { get; init; }
    public string BestTextColor { get; init; } = "#1F2421";
    public string Label { get; init; } = "";
}
