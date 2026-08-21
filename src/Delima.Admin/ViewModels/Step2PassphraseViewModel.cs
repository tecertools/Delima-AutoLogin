using CommunityToolkit.Mvvm.ComponentModel;
using Delima.Admin.Models;

namespace Delima.Admin.ViewModels;

public sealed partial class Step2PassphraseViewModel : ObservableObject
{
    private readonly AdminWizardState _state;

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
                Instructions = "Simpan helaian ini di tempat berkunci selamat. Jika kata laluan pentadbir hilang, bungkusan sekolah mesti diimport semula daripada APDM."
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
