using Delima.Admin.Models;

namespace Delima.Admin.Tests;

public class PasswordStrengthTests
{
    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("12345678901")] // 11 chars
    public void Evaluate_TooShort_ReturnsNotAcceptable(string pass)
    {
        var result = PasswordStrengthEvaluator.Evaluate(pass);
        Assert.False(result.IsAcceptable);
        Assert.Equal("Lemah", result.LevelLabel);
    }

    [Theory]
    [InlineData("123456789012")]
    [InlineData("administrator")]
    [InlineData("admin12345678")]
    public void Evaluate_CommonPassword_ReturnsNotAcceptable(string commonPass)
    {
        var result = PasswordStrengthEvaluator.Evaluate(commonPass);
        Assert.False(result.IsAcceptable);
        Assert.Contains("biasa", result.HintText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_StrongPassphrase_ReturnsKuatAndAcceptable()
    {
        var result = PasswordStrengthEvaluator.Evaluate("MakmalKomputer2026!#");
        Assert.True(result.IsAcceptable);
        Assert.Equal("Kuat", result.LevelLabel);
        Assert.True(result.ScorePercent >= 80);
        Assert.Equal("#056839", result.BarColorHex);
    }

    [Fact]
    public void RecoverySheetInfo_ComputeKeyCheckValue_ReturnsDeterministicFingerprint()
    {
        string kcv1 = RecoverySheetInfo.ComputeKeyCheckValue("MakmalKomputer2026!", "BBA1234");
        string kcv2 = RecoverySheetInfo.ComputeKeyCheckValue("MakmalKomputer2026!", "BBA1234");
        string kcvOther = RecoverySheetInfo.ComputeKeyCheckValue("OtherPassword123!", "BBA1234");

        Assert.Equal(8, kcv1.Length);
        Assert.Equal(kcv1, kcv2);
        Assert.NotEqual(kcv1, kcvOther);
    }
}
