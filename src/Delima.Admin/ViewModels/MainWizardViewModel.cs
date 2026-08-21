using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Delima.Admin.Models;
using Delima.Core.Audit;

namespace Delima.Admin.ViewModels;

public sealed partial class MainWizardViewModel : ObservableObject
{
    public AdminWizardState State { get; }

    [ObservableProperty]
    private int _currentStepIndex = 1; // 0 = Disclaimer, 1..7 = Wizard Steps

    [ObservableProperty]
    private ObservableObject _currentStepViewModel;

    public ObservableCollection<StepNavigationItem> Steps { get; } = [];

    // Step ViewModels
    public FirstRunDisclaimerViewModel DisclaimerVm { get; }
    public Step1IdentityViewModel Step1Vm { get; }
    public Step2PassphraseViewModel Step2Vm { get; }
    public Step3RosterImportViewModel Step3Vm { get; }
    public Step4PasswordImportViewModel Step4Vm { get; }
    public Step5AvatarsViewModel Step5Vm { get; }
    public Step6SettingsViewModel Step6Vm { get; }
    public Step7ProvisionViewModel Step7Vm { get; }

    [ObservableProperty]
    private string _schoolCodeDisplay = "SKS24";

    [ObservableProperty]
    private string _schoolNameDisplay = "SK Seksyen 24";

    public bool IsFirstRun => !State.IsSetupCompletedOnce;

    public bool CanGoBack => CurrentStepIndex > 1;

    public bool CanGoNext
    {
        get
        {
            return CurrentStepIndex switch
            {
                0 => DisclaimerVm.CanProceed,
                1 => Step1Vm.CanProceed,
                2 => Step2Vm.CanProceed,
                3 => Step3Vm.ActiveSubView == "DryRun" ? Step3Vm.CanApplyImport : Step3Vm.CanProceedToDryRun,
                4 => Step4Vm.CanProceed,
                5 => Step5Vm.CanProceed,
                6 => Step6Vm.CanProceed,
                7 => true,
                _ => false
            };
        }
    }

    public string BlockedReason
    {
        get
        {
            return CurrentStepIndex switch
            {
                0 => DisclaimerVm.CanProceed ? "" : "Sila baca dan sahkan makluman tanggungjawab.",
                1 => Step1Vm.ValidationMessage,
                2 => Step2Vm.ValidationMessage,
                3 => Step3Vm.ValidationMessage,
                4 => Step4Vm.ActiveSubView == "Consent" ? Step4Vm.ConsentValidationMessage : "",
                5 => "",
                6 => Step6Vm.Destinations.Count == 0 ? "Sekurang-kurangnya satu destinasi diperlukan." : "",
                7 => "",
                _ => ""
            };
        }
    }

    public string NextButtonText => CurrentStepIndex switch
    {
        0 => "Teruskan ke Wizard →",
        3 when Step3Vm.ActiveSubView == "Mapping" => "Laporan Percubaan →",
        3 when Step3Vm.ActiveSubView == "DryRun" => "Import & Sahkan →",
        4 when Step4Vm.ActiveSubView == "Consent" => "Teruskan →",
        7 => "Selesai",
        _ => "Seterusnya →"
    };

    public MainWizardViewModel(AdminWizardState? initialState = null)
    {
        State = initialState ?? new AdminWizardState();

        DisclaimerVm = new FirstRunDisclaimerViewModel(State);
        Step1Vm = new Step1IdentityViewModel(State);
        Step2Vm = new Step2PassphraseViewModel(State);
        Step3Vm = new Step3RosterImportViewModel(State);
        Step4Vm = new Step4PasswordImportViewModel(State);
        Step5Vm = new Step5AvatarsViewModel(State);
        Step6Vm = new Step6SettingsViewModel(State);
        Step7Vm = new Step7ProvisionViewModel(State);

        InitializeStepNavigationItems();

        if (!State.HasAcknowledgedDisclaimer)
        {
            _currentStepIndex = 0;
            _currentStepViewModel = DisclaimerVm;
        }
        else
        {
            _currentStepIndex = 1;
            _currentStepViewModel = Step1Vm;
        }

        UpdateNavigationState();
    }

    private void InitializeStepNavigationItems()
    {
        Steps.Clear();
        Steps.Add(new StepNavigationItem { StepNumber = 1, Title = "Identiti Sekolah" });
        Steps.Add(new StepNavigationItem { StepNumber = 2, Title = "Kata Laluan Pentadbir" });
        Steps.Add(new StepNavigationItem { StepNumber = 3, Title = "Import Senarai Murid" });
        Steps.Add(new StepNavigationItem { StepNumber = 4, Title = "Import Kata Laluan" });
        Steps.Add(new StepNavigationItem { StepNumber = 5, Title = "Avatar & Kata Laluan Gambar" });
        Steps.Add(new StepNavigationItem { StepNumber = 6, Title = "Destinasi & Tetapan" });
        Steps.Add(new StepNavigationItem { StepNumber = 7, Title = "Bina & Sediakan" });
    }

    public void UpdateNavigationState()
    {
        SchoolCodeDisplay = string.IsNullOrEmpty(State.School.Code) ? "SK" : State.School.Code;
        SchoolNameDisplay = string.IsNullOrEmpty(State.School.Name) ? "DELIMa Admin" : State.School.Name;

        for (int i = 0; i < Steps.Count; i++)
        {
            int stepNum = i + 1;
            var item = Steps[i];
            item.IsCurrent = (CurrentStepIndex == stepNum);

            if (State.IsSetupCompletedOnce)
            {
                // Unlocked for direct navigation once setup has been run at least once!
                item.CanNavigate = true;
                item.Status = (CurrentStepIndex == stepNum) ? StepStatus.InProgress : StepStatus.Done;
            }
            else
            {
                // First run: sequential locking
                if (stepNum < CurrentStepIndex || stepNum <= State.LastCompletedStep)
                {
                    item.Status = StepStatus.Done;
                    item.CanNavigate = true;
                }
                else if (stepNum == CurrentStepIndex)
                {
                    item.Status = StepStatus.InProgress;
                    item.CanNavigate = true;
                }
                else if (stepNum == State.LastCompletedStep + 1)
                {
                    item.Status = StepStatus.NotStarted;
                    item.CanNavigate = true;
                }
                else
                {
                    item.Status = StepStatus.Locked;
                    item.CanNavigate = false;
                }
            }
        }

        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(BlockedReason));
        OnPropertyChanged(nameof(NextButtonText));
    }

    [RelayCommand]
    public void NavigateToStep(int stepNum)
    {
        if (stepNum < 1 || stepNum > 7) return;

        var target = Steps.FirstOrDefault(s => s.StepNumber == stepNum);
        if (target == null || !target.CanNavigate) return;

        SaveCurrentStepState();
        CurrentStepIndex = stepNum;
        CurrentStepViewModel = GetViewModelForStep(stepNum);
        UpdateNavigationState();
    }

    [RelayCommand]
    public void GoNext()
    {
        if (CurrentStepIndex == 0)
        {
            DisclaimerVm.Acknowledge();
            CurrentStepIndex = 1;
            CurrentStepViewModel = Step1Vm;
            UpdateNavigationState();
            return;
        }

        if (CurrentStepIndex == 3 && Step3Vm.ActiveSubView == "Mapping")
        {
            Step3Vm.RunDryRunAnalysis();
            UpdateNavigationState();
            return;
        }

        if (CurrentStepIndex == 4 && Step4Vm.ActiveSubView == "Consent")
        {
            Step4Vm.AcknowledgeConsent();
            UpdateNavigationState();
            return;
        }

        if (!CanGoNext) return;

        SaveCurrentStepState();

        // Mark completion
        if (CurrentStepIndex > State.LastCompletedStep)
            State.LastCompletedStep = CurrentStepIndex;

        AuditLogger.RecordEntry(new AuditLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Event = "wizard_step_completed",
            Outcome = "SUCCESS",
            SchoolCode = State.School.Code,
            Details = $"Wizard Step {CurrentStepIndex} completed."
        });

        if (CurrentStepIndex < 7)
        {
            CurrentStepIndex++;
            CurrentStepViewModel = GetViewModelForStep(CurrentStepIndex);
        }
        else
        {
            // Step 7 complete -> setup has completed once!
            State.IsSetupCompletedOnce = true;
        }

        UpdateNavigationState();
    }

    [RelayCommand]
    public void GoBack()
    {
        if (CurrentStepIndex == 3 && Step3Vm.ActiveSubView == "DryRun")
        {
            Step3Vm.ActiveSubView = "Mapping";
            UpdateNavigationState();
            return;
        }

        if (CurrentStepIndex == 4 && Step4Vm.ActiveSubView == "Grid" && !State.HasAcknowledgedConsent)
        {
            Step4Vm.ActiveSubView = "Consent";
            UpdateNavigationState();
            return;
        }

        if (CurrentStepIndex > 1)
        {
            SaveCurrentStepState();
            CurrentStepIndex--;
            CurrentStepViewModel = GetViewModelForStep(CurrentStepIndex);
            UpdateNavigationState();
        }
    }

    private void SaveCurrentStepState()
    {
        switch (CurrentStepIndex)
        {
            case 1:
                Step1Vm.SaveToState();
                break;
            case 2:
                Step2Vm.SaveToState();
                break;
            case 3:
                Step3Vm.ApplyImport();
                Step4Vm.InitializeGridFromRoster();
                Step5Vm.InitializeAvatars();
                Step7Vm.InitializeLabChecklist();
                break;
            case 4:
                Step4Vm.SaveToState();
                break;
            case 5:
                Step5Vm.SaveToState();
                break;
            case 6:
                Step6Vm.SaveToState();
                break;
        }
    }

    private ObservableObject GetViewModelForStep(int stepNum) => stepNum switch
    {
        0 => DisclaimerVm,
        1 => Step1Vm,
        2 => Step2Vm,
        3 => Step3Vm,
        4 => Step4Vm,
        5 => Step5Vm,
        6 => Step6Vm,
        7 => Step7Vm,
        _ => Step1Vm
    };
}
