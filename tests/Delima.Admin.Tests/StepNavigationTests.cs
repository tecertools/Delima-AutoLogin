using Delima.Admin.Models;
using Delima.Admin.ViewModels;

namespace Delima.Admin.Tests;

public class StepNavigationTests
{
    [Fact]
    public void FirstRun_InitialState_LocksSteps2Through7()
    {
        var state = new AdminWizardState
        {
            HasAcknowledgedDisclaimer = true,
            IsSetupCompletedOnce = false,
            LastCompletedStep = 0
        };

        var wizard = new MainWizardViewModel(state);

        Assert.Equal(1, wizard.CurrentStepIndex);
        Assert.True(wizard.Steps[0].CanNavigate); // Step 1 is unlocked
        Assert.False(wizard.Steps[1].CanNavigate); // Step 2 locked
        Assert.False(wizard.Steps[2].CanNavigate); // Step 3 locked
        Assert.False(wizard.Steps[6].CanNavigate); // Step 7 locked
    }

    [Fact]
    public void PostSetup_DirectNavigation_UnlocksAllSteps()
    {
        var state = new AdminWizardState
        {
            HasAcknowledgedDisclaimer = true,
            IsSetupCompletedOnce = true, // Completed at least once
            LastCompletedStep = 7
        };

        var wizard = new MainWizardViewModel(state);

        foreach (var step in wizard.Steps)
        {
            Assert.True(step.CanNavigate, $"Step {step.StepNumber} should be unlocked for direct navigation");
        }

        // Direct navigation to Step 4 (March password rotation without having to redo 1..3)
        wizard.NavigateToStep(4);
        Assert.Equal(4, wizard.CurrentStepIndex);

        // Direct navigation to Step 3 (January roster refresh)
        wizard.NavigateToStep(3);
        Assert.Equal(3, wizard.CurrentStepIndex);
    }

    [Fact]
    public void Step1_Validation_BlockedWithInlineReasonWhenFieldMissing()
    {
        var state = new AdminWizardState { HasAcknowledgedDisclaimer = true };
        var wizard = new MainWizardViewModel(state);

        wizard.Step1Vm.SchoolCode = "";
        Assert.False(wizard.Step1Vm.CanProceed);
        Assert.Contains("Kod Sekolah diperlukan", wizard.Step1Vm.ValidationMessage);
        Assert.False(wizard.CanGoNext);
        Assert.Contains("Kod Sekolah diperlukan", wizard.BlockedReason);

        wizard.Step1Vm.SchoolCode = "SKS24";
        Assert.True(wizard.Step1Vm.CanProceed);
        Assert.True(wizard.CanGoNext);
    }

    [Fact]
    public void Step2_Validation_BlockedUntilPassphraseValidAndAgreed()
    {
        var state = new AdminWizardState { HasAcknowledgedDisclaimer = true };
        var wizard = new MainWizardViewModel(state);

        wizard.Step2Vm.Passphrase = "Short1!";
        wizard.Step2Vm.ConfirmPassphrase = "Short1!";
        wizard.Step2Vm.HasAgreedNoRecovery = true;

        Assert.False(wizard.Step2Vm.CanProceed);
        Assert.Contains("12 aksara", wizard.Step2Vm.ValidationMessage);

        wizard.Step2Vm.Passphrase = "ValidPassphrase2026!";
        wizard.Step2Vm.ConfirmPassphrase = "MismatchPassphrase!";
        Assert.False(wizard.Step2Vm.CanProceed);
        Assert.Contains("tidak sepadan", wizard.Step2Vm.ValidationMessage);

        wizard.Step2Vm.ConfirmPassphrase = "ValidPassphrase2026!";
        wizard.Step2Vm.HasAgreedNoRecovery = false;
        Assert.False(wizard.Step2Vm.CanProceed);
        Assert.Contains("tiada pemulihan", wizard.Step2Vm.ValidationMessage);

        wizard.Step2Vm.HasAgreedNoRecovery = true;
        Assert.True(wizard.Step2Vm.CanProceed);
    }
}
