using Delima.Win32;

namespace Delima.Win32.Tests;

public class FailureTaxonomyTests
{
    [Theory]
    [InlineData("E01", "Chrome not installed / path unresolvable", "Install Chrome")]
    [InlineData("E02", "Window not verified before timeout", "Slow PC — raise window_wait_timeout_ms")]
    [InlineData("E03", "Injection aborted by pupil", "None")]
    [InlineData("E04", "Wrong password at Google", "Update via Mod Guru; check password_version")]
    [InlineData("E05", "Password stale (password_version behind bundle)", "Re-import + re-provision")]
    [InlineData("E06", "Google CAPTCHA / \"unusual activity\"", "Space out launches; known limitation")]
    [InlineData("E07", "2SV prompt", "Escalate — this may end the product")]
    [InlineData("E08", "Account suspended / password expired", "MOE admin task")]
    [InlineData("E09", "Store decrypt failure", "Re-provision this PC")]
    [InlineData("E10", "Store stale beyond store_max_age_days", "Re-provision this PC")]
    [InlineData("E11", "No password stored for this pupil", "Complete wizard Step 4")]
    [InlineData("E12", "Picture password locked (5 failures)", "Reset via Mod Guru")]
    [InlineData("E13", "Network unreachable", "Network")]
    [InlineData("E14", "Password rejected by Google (stale credential)", "Re-import in Delima.Admin")]
    public void Taxonomy_Codes_Match_Specification_Section7(string code, string expectedCondition, string expectedTeacherAction)
    {
        Assert.True(FailureCodes.IsKnownCode(code));
        Assert.Equal(expectedCondition, FailureCodes.GetCondition(code));
        Assert.Equal(expectedTeacherAction, FailureCodes.GetTeacherAction(code));
    }

    [Fact]
    public void All_Taxonomy_Codes_Have_Calm_Pupil_Messages()
    {
        var codes = new[] { "E01", "E02", "E04", "E05", "E06", "E07", "E08", "E09", "E10", "E11", "E12", "E13", "E14" };
        foreach (var code in codes)
        {
            var msg = FailureCodes.GetPupilMessageBm(code);
            Assert.False(string.IsNullOrWhiteSpace(msg));
            Assert.DoesNotContain(code, msg); // Pupil never sees error code per §7
        }
    }
}
