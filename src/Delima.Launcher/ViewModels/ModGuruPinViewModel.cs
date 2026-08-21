using System.Security.Cryptography;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Delima.Core.Roster;
using Delima.Core.Security;

namespace Delima.Launcher.ViewModels;

/// <summary>
/// ViewModel for Skrin 8: Mod Guru PIN Entry.
/// Enforces 4-digit teacher PIN verification and 5-attempt lockout per PRD §7.4 and Appendix B.
/// </summary>
public sealed partial class ModGuruPinViewModel : ObservableObject
{
    private readonly School _school;
    private readonly Action _onBackRequested;
    private readonly Action _onSuccess;
    private readonly ITeacherPinService _pinService;
    private readonly string? _auditDirectory;
    private DispatcherTimer? _lockoutTimer;

    [ObservableProperty]
    private string _schoolName;

    [ObservableProperty]
    private string _schoolCode;

    [ObservableProperty]
    private string _enteredPin = "";

    [ObservableProperty]
    private int _pinLength;

    [ObservableProperty]
    private bool _dot1Filled;

    [ObservableProperty]
    private bool _dot2Filled;

    [ObservableProperty]
    private bool _dot3Filled;

    [ObservableProperty]
    private bool _dot4Filled;

    [ObservableProperty]
    private int _remainingAttempts = 5;

    [ObservableProperty]
    private string _attemptsRemainingText = "Percubaan yang tinggal: 5";

    [ObservableProperty]
    private bool _isLockedOut;

    [ObservableProperty]
    private string _lockoutMessage = "";

    [ObservableProperty]
    private string _lockoutTimeRemainingText = "";

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private bool _isError;

    [ObservableProperty]
    private bool _isBusy;

    public ModGuruPinViewModel(
        School school,
        Action onBackRequested,
        Action onSuccess,
        ITeacherPinService? pinService = null,
        string? auditDirectory = null)
    {
        _school = school;
        _schoolName = school.Name;
        _schoolCode = string.IsNullOrWhiteSpace(school.Code) ? "SK" : school.Code;
        _onBackRequested = onBackRequested;
        _onSuccess = onSuccess;
        _pinService = pinService ?? TeacherPinService.Instance;
        _auditDirectory = auditDirectory;

        CheckLockoutState();
    }

    public void CheckLockoutState()
    {
        if (_pinService.IsLockedOut(out TimeSpan remaining))
        {
            SetLockedOutState(remaining);
        }
        else
        {
            IsLockedOut = false;
            RemainingAttempts = _pinService.GetRemainingAttempts();
            AttemptsRemainingText = $"Percubaan yang tinggal: {RemainingAttempts}";
            StopLockoutTimer();
        }
    }

    private void SetLockedOutState(TimeSpan remaining)
    {
        IsLockedOut = true;
        RemainingAttempts = 0;
        AttemptsRemainingText = "Percubaan yang tinggal: 0";
        LockoutMessage = "Akses dikunci sementara kerana melebihi had percubaan. Sila tunggu.";
        UpdateLockoutCountdownText(remaining);
        StartLockoutTimer();
    }

    private void UpdateLockoutCountdownText(TimeSpan remaining)
    {
        int minutes = Math.Max(0, (int)remaining.TotalMinutes);
        int seconds = Math.Max(0, remaining.Seconds);
        LockoutTimeRemainingText = $"{minutes:D2}:{seconds:D2}";
    }

    private void StartLockoutTimer()
    {
        if (_lockoutTimer == null)
        {
            _lockoutTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _lockoutTimer.Tick += OnLockoutTimerTick;
        }

        if (!_lockoutTimer.IsEnabled)
        {
            _lockoutTimer.Start();
        }
    }

    private void StopLockoutTimer()
    {
        if (_lockoutTimer != null && _lockoutTimer.IsEnabled)
        {
            _lockoutTimer.Stop();
        }
    }

    private void OnLockoutTimerTick(object? sender, EventArgs e)
    {
        if (_pinService.IsLockedOut(out TimeSpan remaining))
        {
            UpdateLockoutCountdownText(remaining);
        }
        else
        {
            StopLockoutTimer();
            IsLockedOut = false;
            RemainingAttempts = _pinService.GetRemainingAttempts();
            AttemptsRemainingText = $"Percubaan yang tinggal: {RemainingAttempts}";
            LockoutMessage = "";
            LockoutTimeRemainingText = "";
            ErrorMessage = "";
            IsError = false;
        }
    }

    [RelayCommand]
    public void AppendDigit(string digit)
    {
        if (IsLockedOut || IsBusy)
            return;

        if (string.IsNullOrEmpty(digit) || digit.Length != 1 || !char.IsAsciiDigit(digit[0]))
            return;

        if (EnteredPin.Length < 4)
        {
            EnteredPin += digit;
            UpdatePinDots();

            if (EnteredPin.Length == 4)
            {
                VerifyPin();
            }
        }
    }

    [RelayCommand]
    public void Backspace()
    {
        if (IsLockedOut || IsBusy)
            return;

        if (EnteredPin.Length > 0)
        {
            EnteredPin = EnteredPin[..^1];
            UpdatePinDots();
            ErrorMessage = "";
            IsError = false;
        }
    }

    [RelayCommand]
    public void Clear()
    {
        if (IsLockedOut || IsBusy)
            return;

        EnteredPin = "";
        UpdatePinDots();
        ErrorMessage = "";
        IsError = false;
    }

    [RelayCommand]
    public void Cancel()
    {
        StopLockoutTimer();
        _onBackRequested();
    }

    private void UpdatePinDots()
    {
        PinLength = EnteredPin.Length;
        Dot1Filled = PinLength >= 1;
        Dot2Filled = PinLength >= 2;
        Dot3Filled = PinLength >= 3;
        Dot4Filled = PinLength >= 4;
    }

    public void VerifyPin()
    {
        if (EnteredPin.Length != 4 || IsLockedOut)
            return;

        IsBusy = true;
        try
        {
            bool success = _pinService.VerifyPin(EnteredPin, _school.Code, _auditDirectory);

            if (success)
            {
                StopLockoutTimer();
                EnteredPin = "";
                UpdatePinDots();
                _onSuccess();
            }
            else
            {
                EnteredPin = "";
                UpdatePinDots();

                if (_pinService.IsLockedOut(out TimeSpan remaining))
                {
                    SetLockedOutState(remaining);
                }
                else
                {
                    RemainingAttempts = _pinService.GetRemainingAttempts();
                    AttemptsRemainingText = $"Percubaan yang tinggal: {RemainingAttempts}";
                    ErrorMessage = "PIN tidak tepat. Sila cuba lagi.";
                    IsError = true;
                }
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
