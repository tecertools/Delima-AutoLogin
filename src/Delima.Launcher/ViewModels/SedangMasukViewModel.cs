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

    public DestinationConfig? Destination { get; }
    public RouteCOptions Options { get; }

    [ObservableProperty]
    private string _statusMessage;

    [ObservableProperty]
    private bool _isBusy = true;

    private readonly Action<BrowserSession> _onSuccess;
    private readonly Action<RouteCResult> _onFailure;
    private readonly Action _onCancel;

    public SedangMasukViewModel(
        School school,
        Student student,
        ICredential credential,
        Action<BrowserSession> onSuccess,
        Action<RouteCResult> onFailure,
        Action onCancel,
        string? customEmail = null,
        RouteCOptions? options = null,
        DestinationConfig? destination = null)
    {
        School = school;
        Student = student;
        Destination = destination;
        Options = options ?? new RouteCOptions();
        _onSuccess = onSuccess;
        _onFailure = onFailure;
        _onCancel = onCancel;
        _statusMessage = $"Sedang membuka {destination?.Label ?? "DELIMa"}...";

        string email = customEmail ?? (student.EmailLocal.Contains('@')
            ? student.EmailLocal
            : $"{student.EmailLocal}@{school.Domain}");

        _ = StartLoginFlowAsync(email, credential, options);
    }

    private static void RunOnDispatcher(Action action)
    {
        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(action);
        }
        else
        {
            action();
        }
    }

    private async Task StartLoginFlowAsync(string email, ICredential credential, RouteCOptions? options)
    {
        try
        {
            var result = await RouteCLoginOrchestrator.ExecuteAsync(
                browserPath: null,
                email: email,
                credential: credential,
                options: options,
                onStateChanged: UpdateStateMessage,
                cancellationToken: _cts.Token);

            RunOnDispatcher(() => IsBusy = false);

            if (result.Success && result.Session != null)
            {
                RunOnDispatcher(() => _onSuccess(result.Session));
            }
            else if (result.ErrorCode == FailureCodes.E03_InjectionAborted || _cts.IsCancellationRequested)
            {
                RunOnDispatcher(_onCancel);
            }
            else
            {
                RunOnDispatcher(() => _onFailure(result));
            }
        }
        catch (OperationCanceledException)
        {
            RunOnDispatcher(() =>
            {
                IsBusy = false;
                _onCancel();
            });
        }
        catch (Exception ex)
        {
            RunOnDispatcher(() =>
            {
                IsBusy = false;
                var failureResult = RouteCResult.Failure(
                    FailureCodes.E02_WindowNotVerified,
                    FailureCodes.GetPupilMessageBm(FailureCodes.E02_WindowNotVerified),
                    FailureCodes.GetTeacherAction(FailureCodes.E02_WindowNotVerified) + $": {ex.Message}",
                    null, 0, TimeSpan.Zero);
                _onFailure(failureResult);
            });
        }
    }

    public static string GetStateMessage(LoginFlowState state, string? destinationLabel = null) => state switch
    {
        LoginFlowState.LaunchingBrowser => $"Sedang membuka {destinationLabel ?? "DELIMa"}...",
        LoginFlowState.WaitingForIdentifierPage => "Menunggu skrin masuk...",
        LoginFlowState.InjectingIdentifier => "Mengisi maklumat...",
        LoginFlowState.WaitingForTransition => "Menyambung...",
        LoginFlowState.WaitingForPasswordPage => "Menyediakan akaun...",
        LoginFlowState.InjectingPassword => "Hampir siap...",
        LoginFlowState.WaitingForConsentPage => "Mengesahkan akaun...",
        LoginFlowState.Completed => "Berjaya!",
        LoginFlowState.Aborted => "Dibatalkan.",
        LoginFlowState.Failed => "Ada masalah teknikal.",
        _ => "Sedang diproses..."
    };

    private void UpdateStateMessage(LoginFlowState state)
    {
        string msg = GetStateMessage(state, Destination?.Label);
        RunOnDispatcher(() => StatusMessage = msg);
    }

    [RelayCommand]
    private void Cancel()
    {
        _cts.Cancel();
        _onCancel();
    }
}
