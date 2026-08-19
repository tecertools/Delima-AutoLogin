using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Delima.Core.Crypto;
using Delima.Core.Roster;
using Delima.Launcher.Theming;

namespace Delima.Launcher.ViewModels;

/// <summary>
/// ViewModel for Skrin 3: Kata Laluan Gambar.
/// Implements 3-of-16 shuffled icon verification via Argon2id, 5-attempt lockout, and non-animated CSPRNG shuffle.
/// Adheres to PRD §7.3, Technical Architecture §3.2, §6.6, and §7 (E12).
/// </summary>
public sealed partial class KataLaluanGambarViewModel : ObservableObject
{
    private readonly Action _onBackRequested;
    private readonly Action<Student> _onSuccess;
    private readonly IPicturePasswordLockoutService _lockoutService;
    private readonly Argon2Parameters? _argon2Parameters;
    private readonly List<string> _selectedSequence = [];
    private DispatcherTimer? _lockoutTimer;

    [ObservableProperty]
    private Student _student;

    [ObservableProperty]
    private ClassInfo _currentClass;

    [ObservableProperty]
    private string _className;

    [ObservableProperty]
    private string _schoolCode;

    [ObservableProperty]
    private string _pupilDisplayName;

    [ObservableProperty]
    private Brush _classColourBrush;

    [ObservableProperty]
    private string _updatedDateText;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private bool _dot1Filled;

    [ObservableProperty]
    private bool _dot2Filled;

    [ObservableProperty]
    private bool _dot3Filled;

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
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _isError;

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<PicturePasswordIconViewModel> ShuffledIcons { get; } = [];

    public KataLaluanGambarViewModel(
        School school,
        ClassInfo classInfo,
        Student student,
        Action onBackRequested,
        Action<Student> onSuccess,
        IPicturePasswordLockoutService? lockoutService = null,
        Argon2Parameters? argon2Parameters = null,
        DateTimeOffset? updatedDate = null)
    {
        _student = student;
        _currentClass = classInfo;
        _schoolCode = string.IsNullOrWhiteSpace(school.Code) ? "SK" : school.Code;
        _className = $"Tahun {classInfo.Grade} {classInfo.Name}";
        _pupilDisplayName = string.IsNullOrWhiteSpace(student.DisplayName) ? student.Name : student.DisplayName;
        _onBackRequested = onBackRequested;
        _onSuccess = onSuccess;
        _lockoutService = lockoutService ?? PicturePasswordLockoutService.Instance;
        _argon2Parameters = argon2Parameters;

        DateTimeOffset date = updatedDate ?? DateTimeOffset.UtcNow;
        _updatedDateText = $"Senarai dikemas kini {date:d MMMM yyyy}";

        _classColourBrush = ThemeBuilder.CreateFrozenBrush(
            ThemeBuilder.DefaultClassColours[Math.Clamp(classInfo.ColourIndex, 0, ThemeBuilder.DefaultClassColours.Length - 1)]);

        // Check initial lockout state
        CheckLockoutState();

        // Load and CSPRNG shuffle 16 icons without animation
        ReshuffleIcons();
    }

    private void CheckLockoutState()
    {
        if (_lockoutService.IsLockedOut(Student.Id, out TimeSpan remainingTime))
        {
            SetLockedOutState(remainingTime);
        }
        else
        {
            IsLockedOut = false;
            RemainingAttempts = _lockoutService.GetRemainingAttempts(Student.Id);
            AttemptsRemainingText = $"Percubaan yang tinggal: {RemainingAttempts}";
        }
    }

    private void SetLockedOutState(TimeSpan remainingTime)
    {
        IsLockedOut = true;
        RemainingAttempts = 0;
        AttemptsRemainingText = "Percubaan yang tinggal: 0";
        LockoutMessage = "Terkunci. Sila tunggu 5 minit atau panggil cikgu.";
        UpdateLockoutCountdownText(remainingTime);

        StartLockoutTimer();
    }

    private void UpdateLockoutCountdownText(TimeSpan remainingTime)
    {
        int minutes = Math.Max(0, (int)remainingTime.TotalMinutes);
        int seconds = Math.Max(0, remainingTime.Seconds);
        LockoutTimeRemainingText = $"{minutes:D2}:{seconds:D2}";
    }

    private void StartLockoutTimer()
    {
        _lockoutTimer?.Stop();
        _lockoutTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _lockoutTimer.Tick += (s, e) =>
        {
            if (_lockoutService.IsLockedOut(Student.Id, out TimeSpan remaining))
            {
                UpdateLockoutCountdownText(remaining);
            }
            else
            {
                _lockoutTimer?.Stop();
                _lockoutTimer = null;
                IsLockedOut = false;
                StatusMessage = "";
                IsError = false;
                RemainingAttempts = _lockoutService.GetRemainingAttempts(Student.Id);
                AttemptsRemainingText = $"Percubaan yang tinggal: {RemainingAttempts}";
            }
        };
        _lockoutTimer.Start();
    }

    /// <summary>
    /// Instantly reshuffles the 16 picture-password icons using CSPRNG.
    /// Crucial requirement (§6.6): Must not animate so observers cannot track icon movements.
    /// </summary>
    public void ReshuffleIcons()
    {
        var icons = PicturePasswordIconViewModel.GetAllStandardIcons();

        // CSPRNG Fisher-Yates shuffle
        for (int i = icons.Count - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (icons[i], icons[j]) = (icons[j], icons[i]);
        }

        ShuffledIcons.Clear();
        foreach (var icon in icons)
        {
            ShuffledIcons.Add(icon);
        }
    }

    [RelayCommand]
    private async Task SelectIcon(PicturePasswordIconViewModel icon)
    {
        if (IsLockedOut || IsBusy || icon == null || SelectedCount >= 3)
        {
            return;
        }

        _selectedSequence.Add(icon.Id);
        UpdateProgressDots();

        if (SelectedCount == 3)
        {
            await EvaluateSequenceAsync();
        }
    }

    private void UpdateProgressDots()
    {
        SelectedCount = _selectedSequence.Count;
        Dot1Filled = SelectedCount >= 1;
        Dot2Filled = SelectedCount >= 2;
        Dot3Filled = SelectedCount >= 3;
    }

    private async Task EvaluateSequenceAsync()
    {
        IsBusy = true;
        StatusMessage = "";
        IsError = false;

        try
        {
            var chosen = _selectedSequence.ToList();

            // Run verification asynchronously to prevent UI freeze during Argon2 computation
            bool isCorrect = await Task.Run(() =>
            {
                if (Student.PicturePassword != null)
                {
                    return PicturePasswordHasher.VerifyPicturePassword(chosen, Student.PicturePassword, _argon2Parameters);
                }

                // If no picture password configured in store, treat default ("kucing", "bunga", "kereta") as fallback
                var fallbackInfo = PicturePasswordHasher.CreatePicturePassword(["kucing", "bunga", "kereta"], _argon2Parameters);
                return PicturePasswordHasher.VerifyPicturePassword(chosen, fallbackInfo, _argon2Parameters);
            });

            if (isCorrect)
            {
                // Reset failed attempts on success
                _lockoutService.ResetAttempts(Student.Id);
                _selectedSequence.Clear();
                UpdateProgressDots();

                _onSuccess(Student);
            }
            else
            {
                // Handle failed attempt
                int remaining = _lockoutService.RecordFailedAttempt(Student.Id, out bool isLocked, out TimeSpan lockoutTime);
                RemainingAttempts = remaining;
                AttemptsRemainingText = $"Percubaan yang tinggal: {remaining}";

                _selectedSequence.Clear();
                UpdateProgressDots();

                if (isLocked)
                {
                    SetLockedOutState(lockoutTime);
                }
                else
                {
                    IsError = true;
                    StatusMessage = "Gambar tidak betul. Cuba lagi.";
                    // Instantly reshuffle 16-icon grid without animation per §6.6
                    ReshuffleIcons();
                }
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ClearSelection()
    {
        if (IsLockedOut || IsBusy)
        {
            return;
        }

        _selectedSequence.Clear();
        UpdateProgressDots();
        StatusMessage = "";
        IsError = false;
    }

    [RelayCommand]
    private void Back()
    {
        _lockoutTimer?.Stop();
        _lockoutTimer = null;
        _selectedSequence.Clear();
        _onBackRequested();
    }
}
