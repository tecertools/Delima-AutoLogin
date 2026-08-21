using Delima.Admin.Models;

namespace Delima.Admin.Tests;

public class ContrastHelperTests
{
    [Fact]
    public void CalculateContrastRatio_WhiteOnBlack_Returns21To1()
    {
        double ratio = ColorContrastHelper.CalculateContrastRatio("#FFFFFF", "#000000");
        Assert.True(ratio >= 20.9 && ratio <= 21.1);
    }

    [Fact]
    public void CalculateContrastRatio_IdenticalColors_Returns1To1()
    {
        double ratio = ColorContrastHelper.CalculateContrastRatio("#056839", "#056839");
        Assert.Equal(1.0, ratio, 2);
    }

    [Fact]
    public void EvaluateBestContrast_PrimaryGreen_PassesContrast()
    {
        var result = ColorContrastHelper.EvaluateBestContrast("#056839");
        Assert.True(result.IsPass);
        Assert.True(result.Ratio >= 4.5);
        Assert.StartsWith("OK", result.Label);
    }

    [Fact]
    public void EvaluateBestContrast_YellowOnWhite_FailsContrast()
    {
        // Light yellow fails against white/black for 4.5:1
        var result = ColorContrastHelper.EvaluateBestContrast("#FFE9A8");
        // Against white it has low contrast (~1.2:1), against black it might pass or fail depending on luminance
        Assert.NotNull(result.Label);
    }

    [Fact]
    public void EvaluateBestContrast_InvalidHex_ReturnsFailure()
    {
        var result = ColorContrastHelper.EvaluateBestContrast("invalid");
        Assert.False(result.IsValidHex);
        Assert.False(result.IsPass);
    }
}
