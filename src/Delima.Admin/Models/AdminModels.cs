using System.Security.Cryptography;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using Delima.Core.Store;
using Delima.Import;

namespace Delima.Admin.Models;

public enum StepStatus
{
    Locked,
    NotStarted,
    InProgress,
    Attention,
    Done
}

public sealed partial class StepNavigationItem : ObservableObject
{
    [ObservableProperty]
    private int _stepNumber;

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private StepStatus _status = StepStatus.Locked;

    [ObservableProperty]
    private bool _isCurrent;

    [ObservableProperty]
    private bool _canNavigate;
}

public sealed partial class PasswordGridItem : ObservableObject
{
    public string StudentId { get; init; } = "";
    public string StudentName { get; init; } = "";
    public string ClassName { get; init; } = "";
    public string DelimaDigits { get; init; } = "";
    public string EmailLocal { get; init; } = "";
    public string? RegisterNo { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaskedText))]
    [NotifyPropertyChangedFor(nameof(HasPassword))]
    [NotifyPropertyChangedFor(nameof(StatusBadge))]
    private string? _rawPassword;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusBadge))]
    private bool _isShared;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaskedText))]
    private bool _isRevealed;

    [ObservableProperty]
    private int _revealCountdownSeconds;

    public bool HasPassword => !string.IsNullOrEmpty(RawPassword);

    public string MaskedText
    {
        get
        {
            if (!HasPassword) return "—";
            if (IsRevealed) return RawPassword ?? "";
            return "••••••••";
        }
    }

    public string StatusBadge
    {
        get
        {
            if (!HasPassword) return "Tiada";
            if (IsShared) return "Dikongsi";
            return "";
        }
    }
}

public sealed partial class ColorSwatchItem : ObservableObject
{
    [ObservableProperty]
    private string _hexCode = "#056839";

    [ObservableProperty]
    private string _label = "Warna";

    [ObservableProperty]
    private double _contrastRatio = 7.1;

    [ObservableProperty]
    private bool _isPass = true;

    [ObservableProperty]
    private string _tagLabel = "OK 7.1:1";

    public void Recalculate()
    {
        var result = ColorContrastHelper.EvaluateBestContrast(HexCode);
        ContrastRatio = result.Ratio;
        IsPass = result.IsPass;
        TagLabel = result.Label;
    }

    partial void OnHexCodeChanged(string value)
    {
        Recalculate();
    }
}

public sealed class RecoverySheetInfo
{
    public string SchoolCode { get; init; } = "";
    public string SchoolName { get; init; } = "";
    public DateTimeOffset CreationDate { get; init; } = DateTimeOffset.UtcNow;
    public string KeyCheckValue { get; init; } = "";
    public string Instructions { get; init; } = "";

    public static string ComputeKeyCheckValue(string passphrase, string schoolCode)
    {
        if (string.IsNullOrEmpty(passphrase)) return "00000000";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(schoolCode));
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(passphrase));
        return Convert.ToHexString(hash)[..8].ToUpperInvariant();
    }
}

public sealed class LabChecklistItem
{
    public string PcName { get; set; } = "";
    public bool IsProvisioned { get; set; }
    public string Version { get; set; } = "2.0.2";
    public string StoreDate { get; set; } = "—";
    public bool AppLockerVerified { get; set; }
}

public sealed class AdminWizardState
{
    public SchoolInfo School { get; set; } = new()
    {
        Code = "",
        Name = "",
        Motto = "",
        Domain = "moe-dl.edu.my"
    };

    public ThemeInfo Theme { get; set; } = new()
    {
        Primary = "#056839",
        Accent = "#F7941D",
        ClassColours = ["#056839", "#F7941D", "#C41118", "#FFE9A8", "#8A6100", "#1B75BC", "#662D91", "#00A99D"]
    };

    public AppConfig Config { get; set; } = new()
    {
        Destinations =
        [
            new DestinationConfig { Id = "delima", Label = "DELIMa 3.0", Url = "https://d3.delima.edu.my/" },
            new DestinationConfig { Id = "classroom", Label = "Google Classroom", Url = "https://classroom.google.com/" }
        ],
        PicturePasswordRequired = true,
        IdleResetSeconds = 600,
        InjectionSettleMs = 700,
        WindowWaitTimeoutMs = 30000,
        StoreMaxAgeDays = 30
    };

    public string? AdminPassphrase { get; set; }
    public bool HasAgreedNoRecovery { get; set; }

    public List<ImportedStudent> RosterStudents { get; set; } = [];
    public DryRunReport? LastDryRunReport { get; set; }
    public Dictionary<string, string> StudentPasswords { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> StudentAvatars { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> StudentPicturePasswords { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool HasAcknowledgedDisclaimer { get; set; }
    public bool HasAcknowledgedConsent { get; set; }
    public bool IsSetupCompletedOnce { get; set; }
    public int LastCompletedStep { get; set; } = 0;
}
