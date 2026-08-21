using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Delima.Core.Roster;
using Delima.Win32;

namespace Delima.Launcher.ViewModels;

/// <summary>
/// ViewModel for the "Ralat" failure screen presenting a calm BM message to the pupil
/// and an actionable error code/description for the teacher per Technical Architecture §7.
/// </summary>
public sealed partial class RalatViewModel : ObservableObject
{
    public School School { get; }
    public Student? Student { get; }

    [ObservableProperty]
    private string _errorCode;

    [ObservableProperty]
    private string _pupilMessage;

    [ObservableProperty]
    private string _teacherAction;

    [ObservableProperty]
    private string _conditionDescription;

    private readonly Action _onRetry;
    private readonly Action? _onTeacherModeRequested;

    public RalatViewModel(
        School school,
        string errorCode,
        Action onRetry,
        Student? student = null,
        string? customPupilMessage = null,
        string? customTeacherAction = null,
        Action? onTeacherModeRequested = null)
    {
        School = school;
        Student = student;
        _errorCode = errorCode;
        _onRetry = onRetry;
        _onTeacherModeRequested = onTeacherModeRequested;

        _pupilMessage = customPupilMessage ?? FailureCodes.GetPupilMessageBm(errorCode);
        _teacherAction = customTeacherAction ?? FailureCodes.GetTeacherAction(errorCode);
        _conditionDescription = FailureCodes.GetCondition(errorCode);
    }

    [RelayCommand]
    private void Retry()
    {
        _onRetry();
    }

    [RelayCommand]
    private void OpenTeacherMode()
    {
        _onTeacherModeRequested?.Invoke();
    }
}
