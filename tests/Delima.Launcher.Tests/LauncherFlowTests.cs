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

    [Fact]
    public void MainViewModel_NavigateToSedangMasuk_Sets_SedangMasukViewModel()
    {
        var mainVm = new MainViewModel();
        var student = mainVm.Students[0];
        using var cred = new SecurePasswordBuffer("TestPass123!"u8);

        mainVm.NavigateToSedangMasuk(student, cred);

        Assert.IsType<SedangMasukViewModel>(mainVm.CurrentView);
        var sedangMasukVm = (SedangMasukViewModel)mainVm.CurrentView!;
        Assert.Equal(student, sedangMasukVm.Student);
    }
}
