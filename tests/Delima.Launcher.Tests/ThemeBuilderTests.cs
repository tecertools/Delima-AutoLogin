using System.Windows.Media;
using Delima.Core.Store;
using Delima.Launcher.Theming;

namespace Delima.Launcher.Tests;

public class ThemeBuilderTests
{
    [Fact]
    public void ValidateTheme_WithDefaultTheme_ReturnsValid()
    {
        var theme = new ThemeInfo
        {
            Primary = "#056839",
            Accent = "#F7941D",
            ClassColours =
            [
                "#C41118", "#9E2B0E", "#A85200", "#8A6100",
                "#056839", "#2F6B12", "#0A6265", "#7A4A21"
            ]
        };

        var result = ThemeBuilder.ValidateTheme(theme);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateTheme_WithLowContrastPrimary_ReturnsError()
    {
        // Light pastel green fails white text contrast (< 4.5:1)
        var theme = new ThemeInfo
        {
            Primary = "#A8E6CF",
            Accent = "#F7941D"
        };

        var result = ThemeBuilder.ValidateTheme(theme);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Primary colour") || e.Contains("contrast ratio"));
    }

    [Fact]
    public void ValidateTheme_WithLowContrastAccent_ReturnsError()
    {
        // Very dark accent fails dark text contrast
        var theme = new ThemeInfo
        {
            Primary = "#056839",
            Accent = "#1A1208"
        };

        var result = ThemeBuilder.ValidateTheme(theme);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Dark text on accent colour"));
    }

    [Fact]
    public void BuildResourceDictionary_ProducesAllExpectedTokens()
    {
        var theme = new ThemeInfo
        {
            Primary = "#056839",
            Accent = "#F7941D",
            ClassColours = ["#C41118", "#9E2B0E"]
        };

        var dict = ThemeBuilder.BuildResourceDictionary(theme);

        Assert.NotNull(dict);
        Assert.True(dict.Contains(Tokens.PrimaryBrush));
        Assert.True(dict.Contains(Tokens.AccentBrush));
        Assert.True(dict.Contains(Tokens.SurfaceBrush));
        Assert.True(dict.Contains(Tokens.SoftSurfaceBrush));
        Assert.True(dict.Contains(Tokens.PageBackgroundBrush));
        Assert.True(dict.Contains(Tokens.BorderBrush));
        Assert.True(dict.Contains(Tokens.PrimaryTextBrush));
        Assert.True(dict.Contains(Tokens.SecondaryTextBrush));
        Assert.True(dict.Contains(Tokens.AlertBrush));
        Assert.True(dict.Contains(Tokens.FocusRingBrush));

        for (int i = 0; i < 8; i++)
        {
            Assert.True(dict.Contains(Tokens.GetClassColourKey(i)));
            Assert.IsType<SolidColorBrush>(dict[Tokens.GetClassColourKey(i)]);
        }
    }

    [Fact]
    public void CalculateContrastRatio_BlackOnWhite_Returns21()
    {
        double ratio = ThemeBuilder.CalculateContrastRatio(Colors.Black, Colors.White);
        Assert.InRange(ratio, 20.9, 21.1);
    }

    [Fact]
    public void CalculateContrastRatio_WhiteOnBrandGreen_MeetsWCAGAA()
    {
        ThemeBuilder.TryParseColor("#056839", out var brandGreen);
        double ratio = ThemeBuilder.CalculateContrastRatio(Colors.White, brandGreen);

        // Required >= 4.5:1
        Assert.True(ratio >= 4.5, $"White on #056839 ratio was {ratio:F2}:1");
    }

    [Fact]
    public void CalculateContrastRatio_DarkTextOnBrandOrange_MeetsWCAGAA()
    {
        ThemeBuilder.TryParseColor("#1A1208", out var darkText);
        ThemeBuilder.TryParseColor("#F7941D", out var brandOrange);
        double ratio = ThemeBuilder.CalculateContrastRatio(darkText, brandOrange);

        // Required >= 4.5:1
        Assert.True(ratio >= 4.5, $"Dark text on #F7941D ratio was {ratio:F2}:1");
    }
}
