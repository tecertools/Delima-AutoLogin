using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Delima.Admin.Models;

namespace Delima.Admin.ViewModels;

public sealed partial class Step2PassphraseViewModel : ObservableObject
{
    private readonly AdminWizardState _state;

    private static readonly string[] WordBank =
    [
        "bintang", "harimau", "mentari", "cemerlang", "gemilang",
        "wawasan", "pelangi", "samudera", "angkasa", "mutiara",
        "kesuma", "pendekar", "kasturi", "saujana", "selatan",
        "hebat", "perwira", "seroja", "pustaka", "kencana",
        "delima", "satria", "lestari", "bijaksana", "suria"
    ];

    private static readonly string[] Symbols = ["!", "#", "@", "$", "*"];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StrengthResult))]
    [NotifyPropertyChangedFor(nameof(CanProceed))]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    [NotifyPropertyChangedFor(nameof(RecoverySheet))]
    private string _passphrase = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProceed))]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    private string _confirmPassphrase = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProceed))]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    private bool _hasAgreedNoRecovery;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PasswordEyeIcon))]
    private bool _isPasswordVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfirmPasswordEyeIcon))]
    private bool _isConfirmPasswordVisible;

    [ObservableProperty]
    private string? _copyFeedbackMessage;

    public string PasswordEyeIcon => IsPasswordVisible ? "🙈 Sembunyi" : "👁️ Papar";
    public string ConfirmPasswordEyeIcon => IsConfirmPasswordVisible ? "🙈 Sembunyi" : "👁️ Papar";

    public PasswordStrengthResult StrengthResult => PasswordStrengthEvaluator.Evaluate(Passphrase);

    public RecoverySheetInfo? RecoverySheet
    {
        get
        {
            if (string.IsNullOrEmpty(Passphrase)) return null;
            return new RecoverySheetInfo
            {
                SchoolCode = _state.School.Code,
                SchoolName = _state.School.Name,
                CreationDate = DateTimeOffset.UtcNow,
                KeyCheckValue = RecoverySheetInfo.ComputeKeyCheckValue(Passphrase, _state.School.Code),
                Instructions = "Simpan helaian ini di tempat berkunci selamat. Jika kata laluan pentadbir hilang, bungkusan sekolah mesti diimport semula daripada fail senarai murid."
            };
        }
    }

    public bool CanProceed => ValidateInternal(out _);

    public string ValidationMessage
    {
        get
        {
            ValidateInternal(out string msg);
            return msg;
        }
    }

    public Step2PassphraseViewModel(AdminWizardState state)
    {
        _state = state;
        _passphrase = state.AdminPassphrase ?? "";
        _confirmPassphrase = state.AdminPassphrase ?? "";
        _hasAgreedNoRecovery = state.HasAgreedNoRecovery;
    }

    [RelayCommand]
    public void TogglePasswordVisibility()
    {
        IsPasswordVisible = !IsPasswordVisible;
    }

    [RelayCommand]
    public void ToggleConfirmPasswordVisibility()
    {
        IsConfirmPasswordVisible = !IsConfirmPasswordVisible;
    }

    [RelayCommand]
    public void GenerateMemorablePassphrase()
    {
        var rand = Random.Shared;
        string w1 = WordBank[rand.Next(WordBank.Length)];
        string w2;
        do { w2 = WordBank[rand.Next(WordBank.Length)]; } while (w2 == w1);
        int num = rand.Next(10, 9999);
        string sym = Symbols[rand.Next(Symbols.Length)];

        string generated = $"{w1}-{w2}-{num}{sym}";
        Passphrase = generated;
        ConfirmPassphrase = generated;
        IsPasswordVisible = true;
        IsConfirmPasswordVisible = true;
    }

    public string GetFormattedRecoverySheetText()
    {
        var sheet = RecoverySheet;
        if (sheet == null) return "";
        return $"""
        ===================================================================
        HELAIAN PEMULIHAN PENTADBIR DELIMA (RECOVERY SHEET)
        ===================================================================
        Sekolah          : {sheet.SchoolName} ({sheet.SchoolCode})
        Tarikh Dijana    : {sheet.CreationDate:yyyy-MM-dd HH:mm:ss 'UTC'}
        KCV (Fingerprint): {sheet.KeyCheckValue}
        -------------------------------------------------------------------
        PERHATIAN & ARAHAN KESELAMATAN:
        {sheet.Instructions}

        * Kunci induk kata laluan sebenar TIDAK DICETAK di sini demi keselamatan.
        * KCV (Key-Check Value) digunakan untuk mengesahkan bahawa kata laluan
          yang dimasukkan adalah sepadan dengan fail bungkusan disulitkan.
        ===================================================================
        """;
    }

    public bool ValidateInternal(out string message)
    {
        if (string.IsNullOrEmpty(Passphrase))
        {
            message = "Sila masukkan kata laluan pentadbir.";
            return false;
        }

        if (Passphrase.Length < PasswordStrengthEvaluator.MinimumLength)
        {
            message = $"Kata laluan mesti sekurang-kurangnya {PasswordStrengthEvaluator.MinimumLength} aksara.";
            return false;
        }

        if (!StrengthResult.IsAcceptable)
        {
            message = StrengthResult.HintText;
            return false;
        }

        if (Passphrase != ConfirmPassphrase)
        {
            message = "Pengesahan kata laluan tidak sepadan.";
            return false;
        }

        if (!HasAgreedNoRecovery)
        {
            message = "Sila tandakan kotak pengesahan 'tiada pemulihan'.";
            return false;
        }

        message = "";
        return true;
    }

    public void SaveToState()
    {
        _state.AdminPassphrase = Passphrase;
        _state.HasAgreedNoRecovery = HasAgreedNoRecovery;
    }
}

