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
        // All 22 avatars should be distinct within this 22-pupil class
        var distinctAvatars = vm.AvatarItems.Select(a => a.Avatar).Distinct().Count();
        Assert.Equal(22, distinctAvatars);
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
    public void GenerateAvatarSheetHtml_ContainsSchoolCodeClassAndPupilCards()
    {
        var state = new AdminWizardState
        {
            School = new Delima.Core.Store.SchoolInfo { Code = "SKS24", Name = "SK Seksyen 24" }
        };
        state.RosterStudents.Add(new ImportedStudent { Id = "s_101", FullName = "Nur Aishah", ClassName = "2 Cemerlang", EmailLocal = "m-12345678" });

        var vm = new Step5AvatarsViewModel(state);
        string html = vm.GenerateAvatarSheetHtml("2 Cemerlang");

        Assert.Contains("SKS24", html);
        Assert.Contains("SK Seksyen 24", html);
        Assert.Contains("2 Cemerlang", html);
        Assert.Contains("Nur Aishah", html);
        Assert.Contains("m-12345678", html);
        Assert.Contains("window.print()", html);
    }

    [Fact]
    public void CycleAvatar_And_SaveToState_PersistsChangesToAdminWizardState()
    {
        var state = new AdminWizardState();
        state.RosterStudents.Add(new ImportedStudent { Id = "s_201", FullName = "Farhan", ClassName = "2 Cemerlang", EmailLocal = "m-201" });

        var vm = new Step5AvatarsViewModel(state);
        var item = vm.AvatarItems[0];
        string initialAvatar = item.Avatar;

        vm.CycleAvatar(item);
        string cycledAvatar = item.Avatar;
        Assert.NotEqual(initialAvatar, cycledAvatar);

        vm.SaveToState();
        Assert.True(state.StudentAvatars.ContainsKey("s_201"));
        Assert.Equal(cycledAvatar, state.StudentAvatars["s_201"]);
    }
}
