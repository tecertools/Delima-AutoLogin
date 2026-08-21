namespace Delima.Win32;

/// <summary>
/// Defines failure taxonomy codes and messages per Technical Architecture §7.
/// The pupil sees a calm BM message; the teacher sees an actionable code.
/// </summary>
public static class FailureCodes
{
    public const string E01_ChromeNotInstalled = "E01";
    public const string E02_WindowNotVerified = "E02";
    public const string E03_InjectionAborted = "E03";
    public const string E04_WrongPassword = "E04";
    public const string E05_PasswordStale = "E05";
    public const string E06_GoogleCaptcha = "E06";
    public const string E07_TwoFactorPrompt = "E07";
    public const string E08_AccountSuspended = "E08";
    public const string E09_StoreDecryptFailure = "E09";
    public const string E10_StoreStale = "E10";
    public const string E11_NoPasswordStored = "E11";
    public const string E12_PicturePasswordLocked = "E12";
    public const string E13_NetworkUnreachable = "E13";
    public const string E14_PasswordRejected = "E14";

    private static readonly Dictionary<string, (string Condition, string PupilMessageBm, string TeacherAction)> Taxonomy =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [E01_ChromeNotInstalled] = ("Chrome not installed / path unresolvable", "Alamak, ada masalah. Panggil cikgu.", "Install Chrome"),
            [E02_WindowNotVerified] = ("Window not verified before timeout", "Cuba lagi.", "Slow PC — raise window_wait_timeout_ms"),
            [E03_InjectionAborted] = ("Injection aborted by pupil", "", "None"),
            [E04_WrongPassword] = ("Wrong password at Google", "Kata laluan tidak betul. Panggil cikgu.", "Update via Mod Guru; check password_version"),
            [E05_PasswordStale] = ("Password stale (password_version behind bundle)", "Kata laluan sudah tukar. Panggil cikgu.", "Re-import + re-provision"),
            [E06_GoogleCaptcha] = ("Google CAPTCHA / \"unusual activity\"", "Tunggu sekejap, cuba lagi.", "Space out launches; known limitation"),
            [E07_TwoFactorPrompt] = ("2SV prompt", "Panggil cikgu.", "Escalate — this may end the product"),
            [E08_AccountSuspended] = ("Account suspended / password expired", "Panggil cikgu.", "MOE admin task"),
            [E09_StoreDecryptFailure] = ("Store decrypt failure", "Alamak, ada masalah. Panggil cikgu.", "Re-provision this PC"),
            [E10_StoreStale] = ("Store stale beyond store_max_age_days", "Panggil cikgu.", "Re-provision this PC"),
            [E11_NoPasswordStored] = ("No password stored for this pupil", "Panggil cikgu.", "Complete wizard Step 4"),
            [E12_PicturePasswordLocked] = ("Picture password locked (5 failures)", "Tunggu 5 minit.", "Reset via Mod Guru"),
            [E13_NetworkUnreachable] = ("Network unreachable", "Tiada internet. Panggil cikgu.", "Network"),
            [E14_PasswordRejected] = ("Password rejected by Google (stale credential)", "Kata laluan tidak diterima. Beritahu cikgu.", "Re-import in Delima.Admin")
        };

    public static string GetCondition(string errorCode) =>
        Taxonomy.TryGetValue(errorCode, out var info) ? info.Condition : "Unknown failure";

    public static string GetPupilMessageBm(string errorCode) =>
        Taxonomy.TryGetValue(errorCode, out var info) ? info.PupilMessageBm : "Alamak, ada masalah. Panggil cikgu.";

    public static string GetTeacherAction(string errorCode) =>
        Taxonomy.TryGetValue(errorCode, out var info) ? info.TeacherAction : "Investigate failure";

    public static bool IsKnownCode(string errorCode) => Taxonomy.ContainsKey(errorCode);
}
