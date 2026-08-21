using Delima.Core.Roster;
using Delima.Core.Store;
using Delima.Launcher.Services;
using Delima.Launcher.ViewModels;
using Delima.Win32;
using Xunit;

namespace Delima.Launcher.Tests;

public class LauncherFlowTests
{
    [Fact]
    public void SedangMasukViewModel_Initializes_With_Pupil_And_School()
    {
        var school = SampleDataService.CreateSampleSchool();
        var student = SampleDataService.CreateSampleClassStudents("2_cemerlang")[0];
        using var cred = new SecurePasswordBuffer("TestPassword123!"u8);

        var vm = new SedangMasukViewModel(
            school,
            student,
            cred,
            onSuccess: _ => { },
            onFailure: _ => { },
            onCancel: () => { });

        Assert.Equal(student, vm.Student);
        Assert.Equal(school, vm.School);
        Assert.NotEmpty(vm.StatusMessage);
    }

    [Fact]
    public void SedangMasukViewModel_CancelCommand_TriggersCancelCallback()
    {
        var school = SampleDataService.CreateSampleSchool();
        var student = SampleDataService.CreateSampleClassStudents("2_cemerlang")[0];
        using var cred = new SecurePasswordBuffer("TestPassword123!"u8);

        bool cancelCalled = false;

        var vm = new SedangMasukViewModel(
            school,
            student,
            cred,
            onSuccess: _ => { },
            onFailure: _ => { },
            onCancel: () => cancelCalled = true);

        vm.CancelCommand.Execute(null);
        Assert.True(cancelCalled);
    }

    [Fact]
    public void RalatViewModel_Populates_From_FailureTaxonomy()
    {
        var school = SampleDataService.CreateSampleSchool();
        var student = SampleDataService.CreateSampleClassStudents("2_cemerlang")[0];

        bool retryCalled = false;

        var vm = new RalatViewModel(
            school,
            FailureCodes.E01_ChromeNotInstalled,
            onRetry: () => retryCalled = true,
            student: student);

        Assert.Equal(FailureCodes.E01_ChromeNotInstalled, vm.ErrorCode);
        Assert.Equal("Alamak, ada masalah. Panggil cikgu.", vm.PupilMessage);
        Assert.Equal("Install Chrome", vm.TeacherAction);
        Assert.Equal("Chrome not installed / path unresolvable", vm.ConditionDescription);

        vm.RetryCommand.Execute(null);
        Assert.True(retryCalled);
    }

    [Fact]
    public void MainViewModel_NavigateToRalat_Sets_RalatViewModel()
    {
        var mainVm = new MainViewModel();
        var student = mainVm.Students[0];

        mainVm.NavigateToRalat(student, FailureCodes.E09_StoreDecryptFailure);

        Assert.IsType<RalatViewModel>(mainVm.CurrentView);
        var ralatVm = (RalatViewModel)mainVm.CurrentView!;
        Assert.Equal(FailureCodes.E09_StoreDecryptFailure, ralatVm.ErrorCode);
    }

    [Theory]
    [InlineData(FailureCodes.E01_ChromeNotInstalled, "Alamak, ada masalah. Panggil cikgu.", "Install Chrome")]
    [InlineData(FailureCodes.E02_WindowNotVerified, "Cuba lagi.", "Slow PC — raise window_wait_timeout_ms")]
    [InlineData(FailureCodes.E04_WrongPassword, "Kata laluan tidak betul. Panggil cikgu.", "Update via Mod Guru; check password_version")]
    [InlineData(FailureCodes.E05_PasswordStale, "Kata laluan sudah tukar. Panggil cikgu.", "Re-import + re-provision")]
    [InlineData(FailureCodes.E06_GoogleCaptcha, "Tunggu sekejap, cuba lagi.", "Space out launches; known limitation")]
    [InlineData(FailureCodes.E07_TwoFactorPrompt, "Panggil cikgu.", "Escalate — this may end the product")]
    [InlineData(FailureCodes.E08_AccountSuspended, "Panggil cikgu.", "MOE admin task")]
    [InlineData(FailureCodes.E09_StoreDecryptFailure, "Alamak, ada masalah. Panggil cikgu.", "Re-provision this PC")]
    [InlineData(FailureCodes.E10_StoreStale, "Panggil cikgu.", "Re-provision this PC")]
    [InlineData(FailureCodes.E11_NoPasswordStored, "Panggil cikgu.", "Complete wizard Step 4")]
    [InlineData(FailureCodes.E12_PicturePasswordLocked, "Tunggu 5 minit.", "Reset via Mod Guru")]
    [InlineData(FailureCodes.E13_NetworkUnreachable, "Tiada internet. Panggil cikgu.", "Network")]
    public void RalatViewModel_AllTaxonomyCodes_HaveExpectedMessages(string code, string expectedPupilBm, string expectedTeacher)
    {
        var school = SampleDataService.CreateSampleSchool();
        var vm = new RalatViewModel(school, code, () => { });

        Assert.Equal(code, vm.ErrorCode);
        Assert.Equal(expectedPupilBm, vm.PupilMessage);
        Assert.Equal(expectedTeacher, vm.TeacherAction);
        Assert.NotEmpty(vm.ConditionDescription);
    }

    [Fact]
    public void MainViewModel_NavigateToModGuruPin_AndThen_ToDashboard_AndReturn()
    {
        var mainVm = new MainViewModel();
        Assert.IsType<PilihKelasViewModel>(mainVm.CurrentView);

        // Navigate to Mod Guru PIN
        mainVm.NavigateToModGuruPin();
        Assert.IsType<ModGuruPinViewModel>(mainVm.CurrentView);
        var pinVm = (ModGuruPinViewModel)mainVm.CurrentView;

        // Enter valid default PIN
        pinVm.AppendDigit("1");
        pinVm.AppendDigit("2");
        pinVm.AppendDigit("3");
        pinVm.AppendDigit("4");

        // Should have transitioned to Mod Guru Dashboard
        Assert.IsType<ModGuruDashboardViewModel>(mainVm.CurrentView);
        var dashboardVm = (ModGuruDashboardViewModel)mainVm.CurrentView;

        // Exit dashboard -> returns to PilihKelasView
        dashboardVm.ExitDashboardCommand.Execute(null);
        Assert.IsType<PilihKelasViewModel>(mainVm.CurrentView);
    }

    [Fact]
    public void RalatViewModel_OpenTeacherModeCommand_NavigatesToModGuruPin()
    {
        var mainVm = new MainViewModel();
        var student = mainVm.Students[0];

        mainVm.NavigateToRalat(student, FailureCodes.E12_PicturePasswordLocked);
        Assert.IsType<RalatViewModel>(mainVm.CurrentView);
        var ralatVm = (RalatViewModel)mainVm.CurrentView;

        // Teacher clicks Mod Guru button on the error screen
        ralatVm.OpenTeacherModeCommand.Execute(null);
        Assert.IsType<ModGuruPinViewModel>(mainVm.CurrentView);
    }

    [Fact]
    public void MainViewModel_Initializes_With_AppConfig_And_IdleResetSeconds()
    {
        var customConfig = new AppConfig { IdleResetSeconds = 300 };
        var mainVm = new MainViewModel(
            SampleDataService.CreateSampleSchool(),
            SampleDataService.CreateSampleTheme(),
            SampleDataService.CreateSampleClasses(),
            SampleDataService.CreateSampleClassStudents("2_cemerlang"),
            config: customConfig);

        Assert.NotNull(mainVm.Config);
        Assert.Equal(300, mainVm.Config.IdleResetSeconds);
    }

    [Theory]
    [InlineData(LoginFlowState.LaunchingBrowser, "Sedang membuka DELIMa...")]
    [InlineData(LoginFlowState.WaitingForIdentifierPage, "Menunggu skrin masuk...")]
    [InlineData(LoginFlowState.InjectingIdentifier, "Mengisi maklumat...")]
    [InlineData(LoginFlowState.WaitingForTransition, "Menyambung...")]
    [InlineData(LoginFlowState.WaitingForPasswordPage, "Menyediakan akaun...")]
    [InlineData(LoginFlowState.InjectingPassword, "Hampir siap...")]
    [InlineData(LoginFlowState.WaitingForConsentPage, "Mengesahkan akaun...")]
    [InlineData(LoginFlowState.Completed, "Berjaya!")]
    [InlineData(LoginFlowState.Aborted, "Dibatalkan.")]
    [InlineData(LoginFlowState.Failed, "Ada masalah teknikal.")]
    public void SedangMasukViewModel_StateMessages_Map_Correctly(LoginFlowState state, string expectedMessage)
    {
        // Assert BM state messages match PRD §7 and Technical Architecture §4.5
        Assert.Equal(expectedMessage, SedangMasukViewModel.GetStateMessage(state));
    }

    [Fact]
    public void ConsentPrompt_ExactText_Matches_Architecture_Spec()
    {
        // §4.5 & PRD §7.4: Identity check prompt on floating reset bar
        const string expectedPrompt = "Lihat nama kamu. Kalau betul, tekan butang biru di bawah.";
        Assert.Equal("Lihat nama kamu. Kalau betul, tekan butang biru di bawah.", expectedPrompt);
    }
}
