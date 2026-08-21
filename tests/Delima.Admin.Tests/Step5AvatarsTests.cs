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
        for (int i = 1; i <= 10; i++)
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

        Assert.Equal(10, vm.AvatarItems.Count);
        // All 10 avatars should be distinct within this 10-pupil class
        var distinctAvatars = vm.AvatarItems.Select(a => a.Avatar).Distinct().Count();
        Assert.Equal(10, distinctAvatars);
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
}
