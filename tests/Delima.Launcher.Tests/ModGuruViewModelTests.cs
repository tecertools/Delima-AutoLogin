using System.IO;
using Delima.Core.Audit;
using Delima.Core.Crypto;
using Delima.Core.Roster;
using Delima.Core.Security;
using Delima.Launcher.Services;
using Delima.Launcher.ViewModels;
using Xunit;

namespace Delima.Launcher.Tests;

public class ModGuruViewModelTests : IDisposable
{
    private readonly string _testAuditDir;
    private readonly School _school;
    private readonly List<ClassInfo> _classes;
    private readonly List<Student> _students;

    public ModGuruViewModelTests()
    {
        _testAuditDir = Path.Combine(Path.GetTempPath(), "Delima_ModGuruTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testAuditDir);

        _school = SampleDataService.CreateSampleSchool();
        _classes = SampleDataService.CreateSampleClasses();
        _students = SampleDataService.CreateSampleClassStudents("2_cemerlang");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testAuditDir))
            {
                Directory.Delete(_testAuditDir, true);
            }
        }
        catch
        {
            // Ignore
        }
    }

    // ==========================================
    // ModGuruPinViewModel Tests
    // ==========================================

    [Fact]
    public void ModGuruPinViewModel_Keypad_AppendsAndRemovesDigits()
    {
        var pinService = new TeacherPinService("1234");
        var vm = new ModGuruPinViewModel(_school, () => { }, () => { }, pinService, _testAuditDir);

        Assert.Equal(0, vm.PinLength);
        Assert.False(vm.Dot1Filled);

        vm.AppendDigit("1");
        Assert.Equal(1, vm.PinLength);
        Assert.True(vm.Dot1Filled);
        Assert.False(vm.Dot2Filled);

        vm.AppendDigit("2");
        Assert.Equal(2, vm.PinLength);
        Assert.True(vm.Dot1Filled);
        Assert.True(vm.Dot2Filled);

        vm.Backspace();
        Assert.Equal(1, vm.PinLength);
        Assert.False(vm.Dot2Filled);

        vm.Clear();
        Assert.Equal(0, vm.PinLength);
        Assert.False(vm.Dot1Filled);
    }

    [Fact]
    public void ModGuruPinViewModel_CorrectPin_TriggersOnSuccess()
    {
        var pinService = new TeacherPinService("1234");
        bool successCalled = false;
        var vm = new ModGuruPinViewModel(_school, () => { }, () => successCalled = true, pinService, _testAuditDir);

        vm.AppendDigit("1");
        vm.AppendDigit("2");
        vm.AppendDigit("3");
        vm.AppendDigit("4");

        Assert.True(successCalled);
        Assert.False(vm.IsError);
    }

    [Fact]
    public void ModGuruPinViewModel_WrongPin_DecrementsAttemptsAndShowsError()
    {
        var pinService = new TeacherPinService("1234");
        bool successCalled = false;
        var vm = new ModGuruPinViewModel(_school, () => { }, () => successCalled = true, pinService, _testAuditDir);

        vm.AppendDigit("0");
        vm.AppendDigit("0");
        vm.AppendDigit("0");
        vm.AppendDigit("0");

        Assert.False(successCalled);
        Assert.True(vm.IsError);
        Assert.Equal(4, vm.RemainingAttempts);
        Assert.Equal("Percubaan yang tinggal: 4", vm.AttemptsRemainingText);
    }

    [Fact]
    public void ModGuruPinViewModel_FiveWrongPins_LocksOut()
    {
        var pinService = new TeacherPinService("1234", maxFailedAttempts: 5);
        var vm = new ModGuruPinViewModel(_school, () => { }, () => { }, pinService, _testAuditDir);

        for (int i = 0; i < 5; i++)
        {
            vm.AppendDigit("9");
            vm.AppendDigit("9");
            vm.AppendDigit("9");
            vm.AppendDigit("9");
        }

        Assert.True(vm.IsLockedOut);
        Assert.Equal(0, vm.RemainingAttempts);
        Assert.NotEmpty(vm.LockoutMessage);
    }

    [Fact]
    public void ModGuruPinViewModel_CancelCommand_TriggersOnBackRequested()
    {
        bool backCalled = false;
        var vm = new ModGuruPinViewModel(_school, () => backCalled = true, () => { }, null, _testAuditDir);

        vm.CancelCommand.Execute(null);
        Assert.True(backCalled);
    }

    // ==========================================
    // ModGuruDashboardViewModel Tests
    // ==========================================

    [Fact]
    public void ModGuruDashboardViewModel_UpdatePassword_UpdatesVersionAndLogsAudit()
    {
        var vm = new ModGuruDashboardViewModel(_school, _classes, _students, () => { }, null, null, _testAuditDir);

        var targetStudent = _students[0];
        int originalVersion = targetStudent.PasswordVersion;

        vm.SelectedPasswordStudent = targetStudent;
        vm.NewPasswordText = "NewSecurePassword456!";
        vm.UpdatePasswordCommand.Execute(null);

        Assert.Equal(originalVersion + 1, targetStudent.PasswordVersion);
        Assert.True(vm.HasFeedback);
        Assert.False(vm.IsFeedbackError);

        string logFile = AuditLogger.GetAuditLogFilePath(DateTimeOffset.UtcNow, _testAuditDir);
        Assert.True(File.Exists(logFile));
        string logContent = File.ReadAllText(logFile);
        Assert.Contains("teacher_password_update", logContent);
        Assert.Contains(targetStudent.Id, logContent);
        Assert.DoesNotContain("NewSecurePassword456!", logContent); // Never log the password!
    }

    [Fact]
    public void ModGuruDashboardViewModel_ResetPicturePassword_ClearsLockoutAndLogsAudit()
    {
        var lockoutService = new PicturePasswordLockoutService();
        var targetStudent = _students[0];

        // Simulate 5 picture password failures
        for (int i = 0; i < 5; i++)
        {
            lockoutService.RecordFailedAttempt(targetStudent.Id, out _, out _);
        }

        Assert.True(lockoutService.IsLockedOut(targetStudent.Id, out _));

        var vm = new ModGuruDashboardViewModel(_school, _classes, _students, () => { }, null, lockoutService, _testAuditDir);
        vm.SelectedPictureStudent = targetStudent;

        vm.ResetPicturePasswordCommand.Execute(null);

        Assert.False(lockoutService.IsLockedOut(targetStudent.Id, out _));
        Assert.NotNull(targetStudent.PicturePassword);
        Assert.True(vm.HasFeedback);
        Assert.False(vm.IsFeedbackError);

        string logFile = AuditLogger.GetAuditLogFilePath(DateTimeOffset.UtcNow, _testAuditDir);
        Assert.True(File.Exists(logFile));
        string logContent = File.ReadAllText(logFile);
        Assert.Contains("teacher_picture_reset", logContent);
        Assert.Contains(targetStudent.Id, logContent);
    }

    [Fact]
    public void ModGuruDashboardViewModel_AddNewPupil_AddsToRosterAndLogsAudit()
    {
        var vm = new ModGuruDashboardViewModel(_school, _classes, _students, () => { }, null, null, _testAuditDir);

        int initialCount = _students.Count;
        vm.NewPupilName = "Muhammad Haziq";
        vm.NewPupilId = "m-99887766";
        vm.NewPupilClass = _classes[0];
        vm.NewPupilPassword = "InitialPassword123!";
        vm.NewPupilAvatar = "avatar3";

        vm.AddNewPupilCommand.Execute(null);

        Assert.Equal(initialCount + 1, _students.Count);
        var added = _students.FirstOrDefault(s => s.Id == "m-99887766");
        Assert.NotNull(added);
        Assert.Equal("Muhammad Haziq", added.Name);
        Assert.Equal(_classes[0].Id, added.ClassId);
        Assert.Equal("avatar3", added.Avatar);

        string logFile = AuditLogger.GetAuditLogFilePath(DateTimeOffset.UtcNow, _testAuditDir);
        string logContent = File.ReadAllText(logFile);
        Assert.Contains("teacher_add_pupil", logContent);
        Assert.Contains("m-99887766", logContent);
        Assert.DoesNotContain("InitialPassword123!", logContent);
    }

    [Fact]
    public void ModGuruDashboardViewModel_ResetAllLockouts_ClearsAllAndLogsAudit()
    {
        var lockoutService = new PicturePasswordLockoutService();
        lockoutService.RecordFailedAttempt(_students[0].Id, out _, out _);
        lockoutService.RecordFailedAttempt(_students[1].Id, out _, out _);

        var vm = new ModGuruDashboardViewModel(_school, _classes, _students, () => { }, null, lockoutService, _testAuditDir);

        vm.ResetAllLockoutsCommand.Execute(null);

        Assert.Equal(5, lockoutService.GetRemainingAttempts(_students[0].Id));
        Assert.Equal(5, lockoutService.GetRemainingAttempts(_students[1].Id));

        string logFile = AuditLogger.GetAuditLogFilePath(DateTimeOffset.UtcNow, _testAuditDir);
        string logContent = File.ReadAllText(logFile);
        Assert.Contains("teacher_reset_all", logContent);
    }

    [Fact]
    public void ModGuruDashboardViewModel_ExportDiagnostics_WritesRedactedBundleAndLogsAudit()
    {
        var vm = new ModGuruDashboardViewModel(_school, _classes, _students, () => { }, null, null, _testAuditDir);

        vm.ExportDiagnosticsCommand.Execute(null);

        Assert.NotEmpty(vm.LastExportFilePath);
        Assert.True(File.Exists(vm.LastExportFilePath));

        string content = File.ReadAllText(vm.LastExportFilePath);
        Assert.Contains("DELIMa Smart Launcher", content);
        Assert.Contains(_school.Code, content);
        Assert.Contains("Tiada kata laluan", content);

        string logFile = AuditLogger.GetAuditLogFilePath(DateTimeOffset.UtcNow, _testAuditDir);
        string logContent = File.ReadAllText(logFile);
        Assert.Contains("teacher_diagnostics_export", logContent);
    }
}
