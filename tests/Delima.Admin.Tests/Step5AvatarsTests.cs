using Delima.Admin.Models;
using Delima.Admin.ViewModels;
using Delima.Import;

namespace Delima.Admin.Tests;

public class Step5AvatarsTests
{
    [Fact]
    public void InitializeAvatars_DistributesUniqueAvatarsWithinClass()
    {
        var state = new AdminWizardState();
        for (int i = 1; i <= 22; i++)
        {
            state.RosterStudents.Add(new ImportedStudent
            {
                Id = $"s_{i:D8}",
                FullName = $"Pupil {i}",
                ClassName = "2 Cemerlang",
                DelimaDigits = $"{i:D8}",
                EmailLocal = $"m-{i:D8}"
            });
        }

        var vm = new Step5AvatarsViewModel(state);

        Assert.Equal(22, vm.AvatarItems.Count);
        // With DiceBear, each student's effective seed defaults to their unique StudentId
        // so all DiceBear seeds are distinct within the class.
        var distinctSeeds = vm.AvatarItems.Select(a => a.DiceBearSeed).Distinct().Count();
        Assert.Equal(22, distinctSeeds);
    }

    [Fact]
    public void TogglePicturePasswordPolicy_WhenDisabled_ShowsWarning()
    {
        var state = new AdminWizardState();
        var vm = new Step5AvatarsViewModel(state);

        Assert.True(vm.PicturePasswordRequired);
        Assert.False(vm.ShowWarning);

        vm.TogglePicturePasswordPolicy(false);

        Assert.False(vm.PicturePasswordRequired);
        Assert.True(vm.ShowWarning);
        Assert.False(state.Config.PicturePasswordRequired);
    }

    [Fact]
    public void ClassFilter_FiltersAvatarItemsCorrectly()
    {
        var state = new AdminWizardState();
        state.RosterStudents.Add(new ImportedStudent { Id = "s_1", FullName = "Ali", ClassName = "1 Amanah", EmailLocal = "m-1" });
        state.RosterStudents.Add(new ImportedStudent { Id = "s_2", FullName = "Abu", ClassName = "1 Amanah", EmailLocal = "m-2" });
        state.RosterStudents.Add(new ImportedStudent { Id = "s_3", FullName = "Siti", ClassName = "2 Bestari", EmailLocal = "m-3" });

        var vm = new Step5AvatarsViewModel(state);

        Assert.Equal(3, vm.AvatarItems.Count);
        Assert.Contains("Semua Kelas", vm.ClassNames);
        Assert.Contains("1 Amanah", vm.ClassNames);
        Assert.Contains("2 Bestari", vm.ClassNames);

        vm.SelectedClassFilter = "1 Amanah";
        Assert.Equal(2, vm.FilteredAvatarItems.Count);
        Assert.All(vm.FilteredAvatarItems, item => Assert.Equal("1 Amanah", item.ClassName));

        vm.SelectedClassFilter = "2 Bestari";
        Assert.Single(vm.FilteredAvatarItems);
        Assert.Equal("Siti", vm.FilteredAvatarItems[0].StudentName);

        vm.SelectedClassFilter = "Semua Kelas";
        Assert.Equal(3, vm.FilteredAvatarItems.Count);
    }

    [Fact]
    public void YearFilter_FiltersAvatarItemsAndUpdatesClassesDynamically()
    {
        var state = new AdminWizardState();
        state.RosterStudents.Add(new ImportedStudent { Id = "s_1", FullName = "Ahmad", ClassName = "1 Amanah", Grade = 1, EmailLocal = "m-1" });
        state.RosterStudents.Add(new ImportedStudent { Id = "s_2", FullName = "Badrul", ClassName = "1 Bakti", Grade = 1, EmailLocal = "m-2" });
        state.RosterStudents.Add(new ImportedStudent { Id = "s_3", FullName = "Chong", ClassName = "2 Bestari", Grade = 2, EmailLocal = "m-3" });
        state.RosterStudents.Add(new ImportedStudent { Id = "s_4", FullName = "Divya", ClassName = "3 Cemerlang", Grade = 3, EmailLocal = "m-4" });
        state.RosterStudents.Add(new ImportedStudent { Id = "s_5", FullName = "Ehsan", ClassName = "6 Maju", Grade = 6, EmailLocal = "m-5" });

        var vm = new Step5AvatarsViewModel(state);

        // Check Year options
        Assert.Contains("Semua Tahun", vm.YearNames);
        Assert.Contains("Tahun 1", vm.YearNames);
        Assert.Contains("Tahun 2", vm.YearNames);
        Assert.Contains("Tahun 3", vm.YearNames);
        Assert.Contains("Tahun 6", vm.YearNames);

        // Initially "Semua Tahun", all 5 items
        Assert.Equal(5, vm.FilteredAvatarItems.Count);

        // Filter by Tahun 1
        vm.SelectedYearFilter = "Tahun 1";
        Assert.Equal(2, vm.FilteredAvatarItems.Count);
        Assert.All(vm.FilteredAvatarItems, item => Assert.Equal(1, item.Grade));
        Assert.Contains("Semua Kelas", vm.ClassNames);
        Assert.Contains("1 Amanah", vm.ClassNames);
        Assert.Contains("1 Bakti", vm.ClassNames);
        Assert.DoesNotContain("2 Bestari", vm.ClassNames);

        // Further filter by 1 Amanah in Tahun 1
        vm.SelectedClassFilter = "1 Amanah";
        Assert.Single(vm.FilteredAvatarItems);
        Assert.Equal("Ahmad", vm.FilteredAvatarItems[0].StudentName);

        // Filter by Tahun 6
        vm.SelectedYearFilter = "Tahun 6";
        Assert.Single(vm.FilteredAvatarItems);
        Assert.Equal("Ehsan", vm.FilteredAvatarItems[0].StudentName);
        Assert.Contains("6 Maju", vm.ClassNames);

        // Reset to Semua Tahun
        vm.SelectedYearFilter = "Semua Tahun";
        Assert.Equal(5, vm.FilteredAvatarItems.Count);
    }

    [Fact]
    public void GenerateAvatarSheetHtml_ContainsSchoolCodeClassAndPupilCards()
    {
        var state = new AdminWizardState
        {
            School = new Delima.Core.Store.SchoolInfo { Code = "BBA1234", Name = "SK Contoh" }
        };
        state.RosterStudents.Add(new ImportedStudent { Id = "s_101", FullName = "Nur Aishah", ClassName = "2 Cemerlang", Grade = 2, EmailLocal = "m-12345678" });

        var vm = new Step5AvatarsViewModel(state);
        string html = vm.GenerateAvatarSheetHtml("2 Cemerlang");

        Assert.Contains("BBA1234", html);
        Assert.Contains("SK Contoh", html);
        Assert.Contains("2 Cemerlang", html);
        Assert.Contains("Tahun 2", html);
        Assert.Contains("Nur Aishah", html);
        Assert.Contains("m-12345678", html);
        Assert.Contains("pic-pw-tag", html);
        Assert.Contains("🔑", html);
        Assert.Contains("window.print()", html);
        Assert.Contains("api.dicebear.com", html);
        Assert.Contains("<img", html);
    }

    [Fact]
    public void GenerateAvatarSheetHtml_ByYearFilter_OnlyIncludesSelectedYear()
    {
        var state = new AdminWizardState
        {
            School = new Delima.Core.Store.SchoolInfo { Code = "BBA1234", Name = "SK SKS24" }
        };
        state.RosterStudents.Add(new ImportedStudent { Id = "s_1", FullName = "Murid Thn 1", ClassName = "1 Amanah", Grade = 1, EmailLocal = "m-1" });
        state.RosterStudents.Add(new ImportedStudent { Id = "s_2", FullName = "Murid Thn 2", ClassName = "2 Bestari", Grade = 2, EmailLocal = "m-2" });
        state.RosterStudents.Add(new ImportedStudent { Id = "s_3", FullName = "Murid Thn 3", ClassName = "3 Cemerlang", Grade = 3, EmailLocal = "m-3" });

        var vm = new Step5AvatarsViewModel(state);
        string htmlYear1 = vm.GenerateAvatarSheetHtml("Tahun 1", "Semua Kelas");

        Assert.Contains("1 Amanah", htmlYear1);
        Assert.Contains("Murid Thn 1", htmlYear1);
        Assert.DoesNotContain("Murid Thn 2", htmlYear1);
        Assert.DoesNotContain("Murid Thn 3", htmlYear1);
    }

    [Fact]
    public void GenerateAvatarSheetHtml_WhenAllClasses_DividesStudentsByClassAndDoesNotSayAllClassesInClassField()
    {
        var state = new AdminWizardState
        {
            School = new Delima.Core.Store.SchoolInfo { Code = "BBA1234", Name = "SK SKS24" }
        };
        state.RosterStudents.Add(new ImportedStudent { Id = "s_1", FullName = "Ahmad", ClassName = "1 Amanah", Grade = 1, EmailLocal = "m-1" });
        state.RosterStudents.Add(new ImportedStudent { Id = "s_2", FullName = "Badrul", ClassName = "1 Amanah", Grade = 1, EmailLocal = "m-2" });
        state.RosterStudents.Add(new ImportedStudent { Id = "s_3", FullName = "Chong", ClassName = "2 Bestari", Grade = 2, EmailLocal = "m-3" });

        var vm = new Step5AvatarsViewModel(state);
        string html = vm.GenerateAvatarSheetHtml("Semua Kelas");

        // Should NOT say "Kelas: <strong>Semua Kelas</strong>"
        Assert.DoesNotContain("Kelas: <strong>Semua Kelas</strong>", html);

        // Should divide by class
        Assert.Contains("Kelas: <strong>1 Amanah</strong>", html);
        Assert.Contains("Jumlah Murid: <strong>2</strong>", html);
        Assert.Contains("Kelas: <strong>2 Bestari</strong>", html);
        Assert.Contains("Jumlah Murid: <strong>1</strong>", html);

        // Contains all students
        Assert.Contains("Ahmad", html);
        Assert.Contains("Badrul", html);
        Assert.Contains("Chong", html);

        // Includes multiple class-sheet blocks with page-break styling
        Assert.Contains("class-sheet", html);
        Assert.Contains("page-break-after: always", html);
    }

    [Fact]
    public void InitializeAvatars_AssignsThreeIconPicturePasswordSequence_And_PersistsToState()
    {
        var state = new AdminWizardState();
        state.RosterStudents.Add(new ImportedStudent { Id = "s_101", FullName = "Nur Aishah", ClassName = "2 Cemerlang", Grade = 2, EmailLocal = "m-12345678" });

        var vm = new Step5AvatarsViewModel(state);
        var item = vm.AvatarItems[0];

        Assert.Equal(3, item.PicturePassword.Count);
        Assert.NotEmpty(item.PicturePasswordDisplay);
        Assert.NotEmpty(item.PicturePasswordDetailedDisplay);

        vm.SaveToState();
        Assert.True(state.StudentPicturePasswords.ContainsKey("s_101"));
        Assert.Equal(item.PicturePassword, state.StudentPicturePasswords["s_101"]);
    }

    [Fact]
    public void RandomizeAvatar_And_SaveToState_PersistsChangesToAdminWizardState()
    {
        var state = new AdminWizardState();
        state.RosterStudents.Add(new ImportedStudent { Id = "s_201", FullName = "Farhan", ClassName = "2 Cemerlang", Grade = 2, EmailLocal = "m-201" });

        var vm = new Step5AvatarsViewModel(state);
        var item = vm.AvatarItems[0];
        string initialSeed = item.DiceBearSeed;

        vm.RandomizeAvatar(item);
        string newSeed = item.DiceBearSeed;
        Assert.NotEqual(initialSeed, newSeed);
        Assert.NotEmpty(item.Avatar);

        vm.SaveToState();
        Assert.True(state.StudentAvatars.ContainsKey("s_201"));
        Assert.Equal(item.Avatar, state.StudentAvatars["s_201"]);
    }

    [Fact]
    public void CycleAvatar_DelegatesToRandomizeAvatar()
    {
        var state = new AdminWizardState();
        state.RosterStudents.Add(new ImportedStudent { Id = "s_202", FullName = "Zara", ClassName = "2 Cemerlang", Grade = 2, EmailLocal = "m-202" });

        var vm = new Step5AvatarsViewModel(state);
        var item = vm.AvatarItems[0];
        string before = item.DiceBearSeed;

        vm.CycleAvatar(item);
        Assert.NotEqual(before, item.DiceBearSeed);
    }
}

