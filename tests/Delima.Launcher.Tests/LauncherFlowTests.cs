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
}
