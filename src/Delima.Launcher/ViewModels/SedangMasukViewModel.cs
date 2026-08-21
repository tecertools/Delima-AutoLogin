using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Delima.Core.Roster;
using Delima.Core.Store;
using Delima.Win32;

namespace Delima.Launcher.ViewModels;

/// <summary>
/// ViewModel for the "Sedang Masuk" progress screen during visual keystroke injection.
/// </summary>
public sealed partial class SedangMasukViewModel : ObservableObject
{
    private readonly CancellationTokenSource _cts = new();

    public Student Student { get; }
    public School School { get; }

    [ObservableProperty]
    private string _statusMessage = "Sedang membuka DELIMa...";

    [ObservableProperty]
    private bool _isBusy = true;

    private readonly Action<ChromeSession> _onSuccess;
    private readonly Action<RouteCResult> _onFailure;
    private readonly Action _onCancel;

    public SedangMasukViewModel(
        School school,
        Student student,
        ICredential credential,
        Action<ChromeSession> onSuccess,
        Action<RouteCResult> onFailure,
        Action onCancel,
        string? customEmail = null,
        RouteCOptions? options = null)
    {
        School = school;
        Student = student;
        _onSuccess = onSuccess;
        _onFailure = onFailure;
        _onCancel = onCancel;

        string email = customEmail ?? (student.EmailLocal.Contains('@')
            ? student.EmailLocal
            : $"{student.EmailLocal}@{school.Domain}");

        _ = StartLoginFlowAsync(email, credential, options);
    }

    private async Task StartLoginFlowAsync(string email, ICredential credential, RouteCOptions? options)
    {
        try
        {
            var result = await RouteCLoginOrchestrator.ExecuteAsync(
                chromePath: null,
                email: email,
                credential: credential,
                options: options,
                onStateChanged: UpdateStateMessage,
                cancellationToken: _cts.Token);

            IsBusy = false;

            if (result.Success && result.Session != null)
            {
                _onSuccess(result.Session);
            }
            else if (result.ErrorCode == FailureCodes.E03_InjectionAborted || _cts.IsCancellationRequested)
            {
                _onCancel();
            }
            else
            {
                _onFailure(result);
            }
        }
        catch (OperationCanceledException)
        {
            IsBusy = false;
            _onCancel();
        }
        catch (Exception ex)
        {
            IsBusy = false;
            var failureResult = RouteCResult.Failure(
                FailureCodes.E02_WindowNotVerified,
                FailureCodes.GetPupilMessageBm(FailureCodes.E02_WindowNotVerified),
                FailureCodes.GetTeacherAction(FailureCodes.E02_WindowNotVerified) + $": {ex.Message}",
                null, 0, TimeSpan.Zero);
            _onFailure(failureResult);
        }
    }

    private void UpdateStateMessage(LoginFlowState state)
    {
        StatusMessage = state switch
        {
            LoginFlowState.LaunchingBrowser => "Sedang membuka DELIMa...",
            LoginFlowState.WaitingForIdentifierPage => "Menunggu skrin masuk...",
            LoginFlowState.InjectingIdentifier => "Mengisi maklumat...",
            LoginFlowState.WaitingForTransition => "Menyambung...",
            LoginFlowState.WaitingForPasswordPage => "Menyediakan akaun...",
            LoginFlowState.InjectingPassword => "Hampir siap...",
            LoginFlowState.Completed => "Berjaya!",
            LoginFlowState.Aborted => "Dibatalkan.",
            LoginFlowState.Failed => "Ada masalah teknikal.",
            _ => "Sedang diproses..."
        };
    }

    [RelayCommand]
    private void Cancel()
    {
        _cts.Cancel();
        _onCancel();
    }
}
