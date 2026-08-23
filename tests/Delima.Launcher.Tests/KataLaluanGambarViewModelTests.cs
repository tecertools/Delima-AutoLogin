using System.Windows.Threading;
using Delima.Core.Crypto;
using Delima.Core.Roster;
using Delima.Launcher.ViewModels;

namespace Delima.Launcher.Tests;

public class KataLaluanGambarViewModelTests
{
    private static School CreateTestSchool() => new()
    {
        Code = "BBA1234",
        Name = "Sekolah Kebangsaan Contoh",
        Domain = "moe-dl.edu.my"
    };

    private static ClassInfo CreateTestClass() => new()
    {
        Id = "2_cemerlang",
        Name = "2 Cemerlang",
        Grade = 2,
        ColourIndex = 0
    };

    private static Student CreateTestStudent(string[]? picturePassword = null)
    {
        var icons = picturePassword ?? ["kucing", "bunga", "kereta"];
        var picPw = PicturePasswordHasher.CreatePicturePassword(icons, Argon2Parameters.FastTest);

        return new Student
        {
            Id = "s_0001",
            Name = "Nur Aishah Binti Ahmad",
            DisplayName = "Nur Aishah",
            ClassId = "2_cemerlang",
            EmailLocal = "m-12345678",
            Avatar = "kucing",
            PicturePassword = picPw,
            Active = true
        };
    }

    [Fact]
    public void Constructor_Initializes16DistinctIcons()
    {
        var school = CreateTestSchool();
        var classInfo = CreateTestClass();
        var student = CreateTestStudent();
        var lockoutService = new PicturePasswordLockoutService();

        var vm = new KataLaluanGambarViewModel(
            school,
            classInfo,
            student,
            onBackRequested: () => { },
            onSuccess: _ => { },
            lockoutService: lockoutService,
            argon2Parameters: Argon2Parameters.FastTest
        );

        Assert.Equal(16, vm.ShuffledIcons.Count);
        Assert.Equal(16, vm.ShuffledIcons.Select(i => i.Id).Distinct().Count());
        Assert.Equal(5, vm.RemainingAttempts);
        Assert.False(vm.IsLockedOut);
        Assert.Equal(0, vm.SelectedCount);
        Assert.False(vm.Dot1Filled);
        Assert.False(vm.Dot2Filled);
        Assert.False(vm.Dot3Filled);
    }

    [Fact]
    public void ReshuffleIcons_ContainsAllStandardIcons()
    {
        var school = CreateTestSchool();
        var classInfo = CreateTestClass();
        var student = CreateTestStudent();

        var vm = new KataLaluanGambarViewModel(
            school,
            classInfo,
            student,
            onBackRequested: () => { },
            onSuccess: _ => { },
            lockoutService: new PicturePasswordLockoutService(),
            argon2Parameters: Argon2Parameters.FastTest
        );

        var standardIds = PicturePasswordIconViewModel.GetAllStandardIcons().Select(i => i.Id).OrderBy(x => x).ToList();
        var vmIds = vm.ShuffledIcons.Select(i => i.Id).OrderBy(x => x).ToList();

        Assert.Equal(standardIds, vmIds);
    }

    [Fact]
    public void SelectIcon_ProgressivelyUpdatesDots()
    {
        var school = CreateTestSchool();
        var classInfo = CreateTestClass();
        var student = CreateTestStudent();

        var vm = new KataLaluanGambarViewModel(
            school,
            classInfo,
            student,
            onBackRequested: () => { },
            onSuccess: _ => { },
            lockoutService: new PicturePasswordLockoutService(),
            argon2Parameters: Argon2Parameters.FastTest
        );

        var icon1 = vm.ShuffledIcons[0];
        var icon2 = vm.ShuffledIcons[1];

        vm.SelectIconCommand.Execute(icon1);
        Assert.Equal(1, vm.SelectedCount);
        Assert.True(vm.Dot1Filled);
        Assert.False(vm.Dot2Filled);
        Assert.False(vm.Dot3Filled);

        vm.SelectIconCommand.Execute(icon2);
        Assert.Equal(2, vm.SelectedCount);
        Assert.True(vm.Dot1Filled);
        Assert.True(vm.Dot2Filled);
        Assert.False(vm.Dot3Filled);
    }

    [Fact]
    public async Task SelectIcon_CorrectThreeIcons_CallsOnSuccess()
    {
        var school = CreateTestSchool();
        var classInfo = CreateTestClass();
        var student = CreateTestStudent(["kucing", "bunga", "kereta"]);

        Student? successStudent = null;
        var vm = new KataLaluanGambarViewModel(
            school,
            classInfo,
            student,
            onBackRequested: () => { },
            onSuccess: s => successStudent = s,
            lockoutService: new PicturePasswordLockoutService(),
            argon2Parameters: Argon2Parameters.FastTest
        );

        var kucingIcon = vm.ShuffledIcons.First(i => i.Id == "kucing");
        var bungaIcon = vm.ShuffledIcons.First(i => i.Id == "bunga");
        var keretaIcon = vm.ShuffledIcons.First(i => i.Id == "kereta");

        await vm.SelectIconCommand.ExecuteAsync(kucingIcon);
        await vm.SelectIconCommand.ExecuteAsync(bungaIcon);
        await vm.SelectIconCommand.ExecuteAsync(keretaIcon);

        Assert.NotNull(successStudent);
        Assert.Equal(student.Id, successStudent.Id);
        Assert.False(vm.IsError);
    }

    [Fact]
    public async Task SelectIcon_WrongThreeIcons_DecrementsAttempts_Reshuffles_ResetsDots()
    {
        var school = CreateTestSchool();
        var classInfo = CreateTestClass();
        var student = CreateTestStudent(["kucing", "bunga", "kereta"]);
        var lockoutService = new PicturePasswordLockoutService();

        var vm = new KataLaluanGambarViewModel(
            school,
            classInfo,
            student,
            onBackRequested: () => { },
            onSuccess: _ => { },
            lockoutService: lockoutService,
            argon2Parameters: Argon2Parameters.FastTest
        );

        var wrong1 = vm.ShuffledIcons.First(i => i.Id == "bola");
        var wrong2 = vm.ShuffledIcons.First(i => i.Id == "buku");
        var wrong3 = vm.ShuffledIcons.First(i => i.Id == "ikan");

        await vm.SelectIconCommand.ExecuteAsync(wrong1);
        await vm.SelectIconCommand.ExecuteAsync(wrong2);
        await vm.SelectIconCommand.ExecuteAsync(wrong3);

        Assert.Equal(4, vm.RemainingAttempts);
        Assert.Equal("Percubaan yang tinggal: 4", vm.AttemptsRemainingText);
        Assert.True(vm.IsError);
        Assert.Equal("Gambar tidak betul. Cuba lagi.", vm.StatusMessage);
        Assert.Equal(0, vm.SelectedCount);
        Assert.False(vm.Dot1Filled);
        Assert.False(vm.Dot2Filled);
        Assert.False(vm.Dot3Filled);
    }

    [Fact]
    public async Task SelectIcon_FiveConsecutiveFailures_TriggersLockout()
    {
        var school = CreateTestSchool();
        var classInfo = CreateTestClass();
        var student = CreateTestStudent(["kucing", "bunga", "kereta"]);
        var lockoutService = new PicturePasswordLockoutService();

        var vm = new KataLaluanGambarViewModel(
            school,
            classInfo,
            student,
            onBackRequested: () => { },
            onSuccess: _ => { },
            lockoutService: lockoutService,
            argon2Parameters: Argon2Parameters.FastTest
        );

        for (int attempt = 0; attempt < 5; attempt++)
        {
            var wrong1 = vm.ShuffledIcons.First(i => i.Id == "bola");
            var wrong2 = vm.ShuffledIcons.First(i => i.Id == "buku");
            var wrong3 = vm.ShuffledIcons.First(i => i.Id == "ikan");

            await vm.SelectIconCommand.ExecuteAsync(wrong1);
            await vm.SelectIconCommand.ExecuteAsync(wrong2);
            await vm.SelectIconCommand.ExecuteAsync(wrong3);
        }

        Assert.True(vm.IsLockedOut);
        Assert.Equal(0, vm.RemainingAttempts);
        Assert.Equal("Percubaan yang tinggal: 0", vm.AttemptsRemainingText);
        Assert.Contains("Terkunci", vm.LockoutMessage);
        Assert.True(lockoutService.IsLockedOut(student.Id, out _));
    }

    [Fact]
    public void ClearSelectionCommand_ResetsSelectedDotsWithoutFailure()
    {
        var school = CreateTestSchool();
        var classInfo = CreateTestClass();
        var student = CreateTestStudent();
        var lockoutService = new PicturePasswordLockoutService();

        var vm = new KataLaluanGambarViewModel(
            school,
            classInfo,
            student,
            onBackRequested: () => { },
            onSuccess: _ => { },
            lockoutService: lockoutService,
            argon2Parameters: Argon2Parameters.FastTest
        );

        vm.SelectIconCommand.Execute(vm.ShuffledIcons[0]);
        vm.SelectIconCommand.Execute(vm.ShuffledIcons[1]);
        Assert.Equal(2, vm.SelectedCount);

        vm.ClearSelectionCommand.Execute(null);
        Assert.Equal(0, vm.SelectedCount);
        Assert.False(vm.Dot1Filled);
        Assert.Equal(5, vm.RemainingAttempts);
    }

    [Fact]
    public void BackCommand_CallsOnBackRequested()
    {
        var school = CreateTestSchool();
        var classInfo = CreateTestClass();
        var student = CreateTestStudent();

        bool backCalled = false;
        var vm = new KataLaluanGambarViewModel(
            school,
            classInfo,
            student,
            onBackRequested: () => backCalled = true,
            onSuccess: _ => { },
            lockoutService: new PicturePasswordLockoutService(),
            argon2Parameters: Argon2Parameters.FastTest
        );

        vm.BackCommand.Execute(null);
        Assert.True(backCalled);
    }
}
