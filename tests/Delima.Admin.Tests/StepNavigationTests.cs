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
        Assert.True(wizard.CanGoNext);
    }

    [Fact]
    public void FirstRun_Step0_Disclaimer_InitiallyBlocked_EnabledOnCheckboxCheck_AdvancesToStep1()
    {
        var state = new AdminWizardState
        {
            HasAcknowledgedDisclaimer = false,
            IsSetupCompletedOnce = false,
            LastCompletedStep = 0
        };

        var wizard = new MainWizardViewModel(state);

        Assert.Equal(0, wizard.CurrentStepIndex);
        Assert.Equal("Saya Faham →", wizard.NextButtonText);
        Assert.False(wizard.CanGoNext);
        Assert.Contains("makluman tanggungjawab", wizard.BlockedReason);

        // Clicking GoNext while blocked does not advance
        wizard.GoNext();
        Assert.Equal(0, wizard.CurrentStepIndex);

        // User ticks checkbox
        wizard.DisclaimerVm.HasReadNotice = true;
        Assert.True(wizard.CanGoNext);
        Assert.Equal("", wizard.BlockedReason);

        // Now GoNext advances to Step 1
        wizard.GoNext();
        Assert.Equal(1, wizard.CurrentStepIndex);
        Assert.True(state.HasAcknowledgedDisclaimer);
        Assert.Equal("Seterusnya →", wizard.NextButtonText);
        Assert.True(wizard.CanGoNext);
    }

    [Fact]
    public void Step4_Consent_TypingSchoolCodeDynamicallyEnablesNextButtonAndTransitionsToGrid()
    {
        var state = new AdminWizardState
        {
            HasAcknowledgedDisclaimer = true,
            HasAcknowledgedConsent = false,
            LastCompletedStep = 3,
            School = new Delima.Core.Store.SchoolInfo { Code = "SKS24" }
        };

        var wizard = new MainWizardViewModel(state);
        wizard.NavigateToStep(4);
        Assert.Equal(4, wizard.CurrentStepIndex);
        Assert.Equal("Consent", wizard.Step4Vm.ActiveSubView);
        Assert.Equal("Teruskan →", wizard.NextButtonText);
        Assert.False(wizard.CanGoNext);
        Assert.Contains("SKS24", wizard.BlockedReason);

        // Typing partial / wrong code keeps button disabled
        wizard.Step4Vm.ConsentTypedCode = "SKS";
        Assert.False(wizard.CanGoNext);

        // Typing correct code enables button dynamically
        wizard.Step4Vm.ConsentTypedCode = "SKS24";
        Assert.True(wizard.CanGoNext);
        Assert.Equal("", wizard.BlockedReason);

        // Clicking GoNext acknowledges consent and transitions to Grid view
        wizard.GoNext();
        Assert.Equal(4, wizard.CurrentStepIndex);
        Assert.Equal("Grid", wizard.Step4Vm.ActiveSubView);
        Assert.Equal("Seterusnya →", wizard.NextButtonText);
        Assert.True(wizard.CanGoNext);
        Assert.True(state.HasAcknowledgedConsent);
    }

    [Fact]
    public void Step6_DestinationModification_UpdatesCanGoNext()
    {
        var state = new AdminWizardState
        {
            HasAcknowledgedDisclaimer = true,
            LastCompletedStep = 5
        };
        var wizard = new MainWizardViewModel(state);
        wizard.NavigateToStep(6);
        Assert.Equal(6, wizard.CurrentStepIndex);
        Assert.True(wizard.CanGoNext);

        // Remove all destinations
        wizard.Step6Vm.Destinations.Clear();
        Assert.False(wizard.CanGoNext);
        Assert.Contains("destinasi diperlukan", wizard.BlockedReason);

        // Add a destination
        wizard.Step6Vm.NewDestLabel = "Frog VLE";
        wizard.Step6Vm.NewDestUrl = "https://frogvle.my";
        wizard.Step6Vm.AddDestination();
        Assert.True(wizard.CanGoNext);
        Assert.Equal("", wizard.BlockedReason);
    }

    [Fact]
    public void FullWizard_HappyPath_Step0ThroughStep7_CompletesSetup()
    {
        var state = new AdminWizardState
        {
            HasAcknowledgedDisclaimer = false,
            IsSetupCompletedOnce = false
        };

        var wizard = new MainWizardViewModel(state);

        // Step 0 -> Check disclaimer
        Assert.Equal(0, wizard.CurrentStepIndex);
        wizard.DisclaimerVm.HasReadNotice = true;
        wizard.GoNext();

        // Step 1 -> Identiti Sekolah
        Assert.Equal(1, wizard.CurrentStepIndex);
        wizard.Step1Vm.SchoolCode = "UJIAN";
        wizard.Step1Vm.SchoolName = "SK Ujian";
        wizard.GoNext();

        // Step 2 -> Kata Laluan Pentadbir
        Assert.Equal(2, wizard.CurrentStepIndex);
        Assert.False(wizard.CanGoNext);
        wizard.Step2Vm.Passphrase = "admin_delimasks24";
        wizard.Step2Vm.ConfirmPassphrase = "admin_delimasks24";
        wizard.Step2Vm.HasAgreedNoRecovery = true;
        Assert.True(wizard.CanGoNext);
        wizard.GoNext();

        // Step 3 -> Import Roster
        Assert.Equal(3, wizard.CurrentStepIndex);
        Assert.Equal("Mapping", wizard.Step3Vm.ActiveSubView);
        Assert.Equal("Laporan Percubaan →", wizard.NextButtonText);

        // Create temporary sample CSV
        string csvPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"roster_{System.Guid.NewGuid():N}.csv");
        try
        {
            System.IO.File.WriteAllText(csvPath, "BIL,NAMA MURID,NO. KAD PENGENALAN,TAHUN,KELAS,ID PENGGUNA DELIMA\n1,Ahmad Bin Ali,150101-10-1234,2,2 Ujian,m-12345678\n");
            wizard.Step3Vm.LoadFile(csvPath);
            Assert.True(wizard.CanGoNext);

            // Step 3 Mapping -> Run Dry Run
            wizard.GoNext();
            Assert.Equal(3, wizard.CurrentStepIndex);
            Assert.Equal("DryRun", wizard.Step3Vm.ActiveSubView);
            Assert.Equal("Import & Sahkan →", wizard.NextButtonText);
            Assert.True(wizard.CanGoNext);

            // Step 3 Dry Run -> Step 4
            wizard.GoNext();
            Assert.Equal(4, wizard.CurrentStepIndex);

            // Step 4 -> Consent
            Assert.Equal("Consent", wizard.Step4Vm.ActiveSubView);
            Assert.Equal("Teruskan →", wizard.NextButtonText);
            Assert.False(wizard.CanGoNext);
            wizard.Step4Vm.ConsentTypedCode = "UJIAN";
            Assert.True(wizard.CanGoNext);

            // Step 4 Consent -> Grid
            wizard.GoNext();
            Assert.Equal(4, wizard.CurrentStepIndex);
            Assert.Equal("Grid", wizard.Step4Vm.ActiveSubView);
            Assert.Equal("Seterusnya →", wizard.NextButtonText);
            Assert.True(wizard.CanGoNext);

            // Step 4 Grid -> Step 5
            wizard.GoNext();
            Assert.Equal(5, wizard.CurrentStepIndex);

            // Step 5 -> Step 6
            wizard.GoNext();
            Assert.Equal(6, wizard.CurrentStepIndex);

            // Step 6 -> Step 7
            wizard.GoNext();
            Assert.Equal(7, wizard.CurrentStepIndex);
            Assert.Equal("Selesai", wizard.NextButtonText);

            // Step 7 -> Finish
            bool closeRequested = false;
            string? messageShown = null;
            wizard.RequestClose = () => closeRequested = true;
            wizard.ShowCompletionMessage = (msg, title) => messageShown = msg;

            wizard.GoNext();
            Assert.True(state.IsSetupCompletedOnce);
            Assert.True(closeRequested);
            Assert.NotNull(messageShown);
            Assert.Contains("Tahniah", messageShown);
            foreach (var step in wizard.Steps)
            {
                Assert.True(step.CanNavigate);
            }
        }
        finally
        {
            if (System.IO.File.Exists(csvPath)) System.IO.File.Delete(csvPath);
        }
    }

    [Fact]
    public void Step7_Selesai_InvokesFriendlyMessageAndCloseAction()
    {
        var state = new AdminWizardState
        {
            HasAcknowledgedDisclaimer = true,
            IsSetupCompletedOnce = true,
            LastCompletedStep = 7
        };

        var wizard = new MainWizardViewModel(state);
        wizard.NavigateToStep(7);
        Assert.Equal(7, wizard.CurrentStepIndex);
        Assert.Equal("Selesai", wizard.NextButtonText);
        Assert.True(wizard.CanGoNext);

        bool closeInvoked = false;
        string? messageText = null;
        string? messageTitle = null;

        wizard.RequestClose = () => closeInvoked = true;
        wizard.ShowCompletionMessage = (msg, title) =>
        {
            messageText = msg;
            messageTitle = title;
        };

        wizard.GoNext();

        Assert.True(closeInvoked);
        Assert.NotNull(messageText);
        Assert.Equal("Konfigurasi Selesai", messageTitle);
        Assert.Contains("Tahniah", messageText);
        Assert.True(state.IsSetupCompletedOnce);
    }
}
