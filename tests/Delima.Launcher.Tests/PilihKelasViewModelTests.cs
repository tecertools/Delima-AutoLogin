using Delima.Core.Roster;
using Delima.Launcher.Services;
using Delima.Launcher.ViewModels;

namespace Delima.Launcher.Tests;

public class PilihKelasViewModelTests
{
    [Fact]
    public void InitialState_HasTahunList_AndKelasDropdownIsDisabled()
    {
        var school = SampleDataService.CreateSampleSchool();
        var classes = SampleDataService.CreateSampleClasses();
        var lastClass = classes.First(c => c.Id == "2_cemerlang");

        var vm = new PilihKelasViewModel(
            school,
            classes,
            lastClass,
            onClassConfirmed: _ => { }
        );

        Assert.Equal("Sekolah Kebangsaan Contoh", vm.SchoolName);
        Assert.Equal("Berilmu Berdisiplin", vm.SchoolMotto);
        Assert.True(vm.HasLastClass);
        Assert.Equal("Tahun 2 Cemerlang", vm.LastClassDisplayText);
        Assert.Null(vm.SelectedTahun);
        Assert.False(vm.IsKelasDropdownEnabled);
        Assert.False(vm.CanProceed);
        Assert.Empty(vm.AvailableClasses);
        Assert.Equal([1, 2, 3, 4, 5, 6], vm.AvailableTahun.ToList());
    }

    [Fact]
    public void SelectingTahun_PopulatesMatchingClasses_AndEnablesDropdown()
    {
        var school = SampleDataService.CreateSampleSchool();
        var classes = SampleDataService.CreateSampleClasses();

        var vm = new PilihKelasViewModel(
            school,
            classes,
            null,
            onClassConfirmed: _ => { }
        );

        vm.SelectedTahun = 2;

        Assert.True(vm.IsKelasDropdownEnabled);
        Assert.Equal(4, vm.AvailableClasses.Count);
        Assert.All(vm.AvailableClasses, c => Assert.Equal(2, c.Grade));
        Assert.False(vm.CanProceed); // Still false until class is selected
    }

    [Fact]
    public void SelectingClass_EnablesProceedCommand_AndExecutesCallback()
    {
        var school = SampleDataService.CreateSampleSchool();
        var classes = SampleDataService.CreateSampleClasses();
        ClassInfo? confirmedClass = null;

        var vm = new PilihKelasViewModel(
            school,
            classes,
            null,
            onClassConfirmed: c => confirmedClass = c
        );

        vm.SelectedTahun = 2;
        var chosenClass = vm.AvailableClasses.First(c => c.Id == "2_gemilang");
        vm.SelectedClass = chosenClass;

        Assert.True(vm.CanProceed);

        vm.ProceedCommand.Execute(null);

        Assert.NotNull(confirmedClass);
        Assert.Equal("2_gemilang", confirmedClass.Id);
    }

    [Fact]
    public void SelectLastClassCommand_DirectlyInvokesCallbackWithLastClass()
    {
        var school = SampleDataService.CreateSampleSchool();
        var classes = SampleDataService.CreateSampleClasses();
        var lastClass = classes.First(c => c.Id == "2_cemerlang");
        ClassInfo? confirmedClass = null;

        var vm = new PilihKelasViewModel(
            school,
            classes,
            lastClass,
            onClassConfirmed: c => confirmedClass = c
        );

        vm.SelectLastClassCommand.Execute(null);

        Assert.NotNull(confirmedClass);
        Assert.Equal("2_cemerlang", confirmedClass.Id);
    }

    [Fact]
    public void ToggleLanguage_TogglesBetweenBMAndEN()
    {
        var school = SampleDataService.CreateSampleSchool();
        var classes = SampleDataService.CreateSampleClasses();

        var vm = new PilihKelasViewModel(
            school,
            classes,
            null,
            onClassConfirmed: _ => { }
        );

        Assert.Equal("BM", vm.SelectedLanguage);
        vm.ToggleLanguageCommand.Execute(null);
        Assert.Equal("EN", vm.SelectedLanguage);
        vm.ToggleLanguageCommand.Execute(null);
        Assert.Equal("BM", vm.SelectedLanguage);
    }
}
