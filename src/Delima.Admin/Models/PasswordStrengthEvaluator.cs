namespace Delima.Admin.Models;

public sealed class PasswordStrengthResult
{
    public int ScorePercent { get; init; } // 0 to 100
    public string LevelLabel { get; init; } = "Lemah"; // Lemah, Sederhana, Kuat
    public string BarColorHex { get; init; } = "#A3312D"; // Red, Orange/Amber, Green
    public string HintText { get; init; } = "";
    public bool IsAcceptable { get; init; }
}

public static class PasswordStrengthEvaluator
{
    public const int MinimumLength = 12;

    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "123456789012",
        "password12345",
        "administrator",
        "admin12345678",
        "qwertyuiop12",
        "letmein123456",
        "password1234",
        "skseksyen2424",
        "delima1234567",
        "cikgupassword",
        "pentadbir1234"
    };

    public static PasswordStrengthResult Evaluate(string? passphrase)
    {
        if (string.IsNullOrEmpty(passphrase))
        {
            return new PasswordStrengthResult
            {
                ScorePercent = 0,
                LevelLabel = "Lemah",
                BarColorHex = "#DDE1DC",
                HintText = "Sekurang-kurangnya 12 aksara diperlukan.",
                IsAcceptable = false
            };
        }

        int length = passphrase.Length;
        bool isCommon = CommonPasswords.Contains(passphrase) || passphrase.All(c => c == passphrase[0]);

        if (isCommon)
        {
            return new PasswordStrengthResult
            {
                ScorePercent = 20,
                LevelLabel = "Lemah",
                BarColorHex = "#A3312D",
                HintText = "Kata laluan terlalu biasa atau mudah diteka.",
                IsAcceptable = false
            };
        }

        if (length < MinimumLength)
        {
            int percent = (int)Math.Round((double)length / MinimumLength * 40.0);
            return new PasswordStrengthResult
            {
                ScorePercent = percent,
                LevelLabel = "Lemah",
                BarColorHex = "#A3312D",
                HintText = $"Terlalu pendek ({length}/{MinimumLength} aksara).",
                IsAcceptable = false
            };
        }

        // Calculate score for length >= 12
        bool hasUpper = passphrase.Any(char.IsUpper);
        bool hasLower = passphrase.Any(char.IsLower);
        bool hasDigit = passphrase.Any(char.IsDigit);
        bool hasSpecial = passphrase.Any(c => !char.IsLetterOrDigit(c));

        int varietyCount = (hasUpper ? 1 : 0) + (hasLower ? 1 : 0) + (hasDigit ? 1 : 0) + (hasSpecial ? 1 : 0);

        int score = 50; // base score for 12+ chars
        score += Math.Min(25, (length - MinimumLength) * 5); // up to +25 for length
        score += varietyCount * 6; // up to +24 for variety

        score = Math.Min(100, Math.Max(0, score));

        if (score >= 80)
        {
            return new PasswordStrengthResult
            {
                ScorePercent = score,
                LevelLabel = "Kuat",
                BarColorHex = "#056839",
                HintText = $"Kuat. {length} aksara, tiada dalam senarai kata laluan biasa.",
                IsAcceptable = true
            };
        }
        else if (score >= 55)
        {
            return new PasswordStrengthResult
            {
                ScorePercent = score,
                LevelLabel = "Sederhana",
                BarColorHex = "#F7941D",
                HintText = $"Sederhana. {length} aksara. Boleh tambah simbol atau huruf besar.",
                IsAcceptable = true
            };
        }
        else
        {
            return new PasswordStrengthResult
            {
                ScorePercent = score,
                LevelLabel = "Lemah",
                BarColorHex = "#A3312D",
                HintText = "Boleh ditingkatkan dengan menggunakan aksara bercampur.",
                IsAcceptable = true
            };
        }
    }
}
