using Delima.Core.Roster;
using Delima.Launcher.Services;
using Delima.Launcher.ViewModels;

namespace Delima.Launcher.Tests;

public class CariNamaViewModelTests
{
    [Theory]
    [InlineData(30, 7)] // 30+1 = 31 -> 7 cols
    [InlineData(34, 7)] // 34+1 = 35 -> 7 cols
    [InlineData(36, 8)] // 36+1 = 37 -> 8 cols
    [InlineData(39, 8)] // 39+1 = 40 -> 8 cols
    [InlineData(40, 9)] // 40+1 = 41 -> 9 cols
    [InlineData(44, 9)] // 44+1 = 45 -> 9 cols
    public void ColumnCount_IsComputedCorrectly_ForClassSizes(int pupilCount, int expectedColumns)
    {
        var school = SampleDataService.CreateSampleSchool();
        var classInfo = new ClassInfo { Id = "2_cemerlang", Name = "2 Cemerlang", Grade = 2, ColourIndex = 0 };

        var students = new List<Student>();
        for (int i = 1; i <= pupilCount; i++)
        {
            students.Add(new Student
            {
                Id = $"s_{i:D4}",
                Name = $"Murid Contoh {i}",
                ClassId = "2_cemerlang",
                EmailLocal = $"m-{10000000 + i}",
                Avatar = "kucing",
                Active = true
            });
        }

        var vm = new CariNamaViewModel(
            school,
            classInfo,
            students,
            onBackRequested: () => { },
            onStudentSelected: _ => { },
            onMissingStudentRequested: () => { }
        );

        Assert.Equal(expectedColumns, vm.ColumnCount);
        // Total filtered items = pupils + 1 escape card
        Assert.Equal(pupilCount + 1, vm.FilteredPupilCards.Count);
        Assert.True(vm.FilteredPupilCards.Last().IsMissingEscapeCard);
    }

    [Fact]
    public void SearchQuery_FiltersPupilsInRealTime_PreservingEscapeCard()
    {
        var school = SampleDataService.CreateSampleSchool();
        var classInfo = new ClassInfo { Id = "2_cemerlang", Name = "2 Cemerlang", Grade = 2, ColourIndex = 0 };
        var students = SampleDataService.CreateSampleClassStudents("2_cemerlang");

        var vm = new CariNamaViewModel(
            school,
            classInfo,
            students,
            onBackRequested: () => { },
            onStudentSelected: _ => { },
            onMissingStudentRequested: () => { }
        );

        int totalCount = students.Count + 1;
        Assert.Equal(totalCount, vm.FilteredPupilCards.Count);

        // Search by calling name
        vm.SearchQuery = "Aishah";
        var matchingAishah = vm.FilteredPupilCards.Where(c => !c.IsMissingEscapeCard).ToList();
        Assert.True(matchingAishah.Count >= 3); // Nur Aishah Binti Ahmad, Nur Aishah Binti Osman, Nur Aishah Binti Ali
        Assert.True(vm.FilteredPupilCards.Last().IsMissingEscapeCard);

        // Search by Chinese name
        vm.SearchQuery = "Wei Ming";
        var matchingTan = vm.FilteredPupilCards.Where(c => !c.IsMissingEscapeCard).ToList();
        Assert.Single(matchingTan);
        Assert.Equal("Wei Ming", matchingTan[0].DisplayName);

        // Clear search restores all
        vm.SearchQuery = "";
        Assert.Equal(totalCount, vm.FilteredPupilCards.Count);
    }

    [Fact]
    public void Disambiguation_NurAishahColleagues_HaveInitials()
    {
        var school = SampleDataService.CreateSampleSchool();
        var classInfo = new ClassInfo { Id = "2_cemerlang", Name = "2 Cemerlang", Grade = 2, ColourIndex = 0 };
        var students = SampleDataService.CreateSampleClassStudents("2_cemerlang");

        var vm = new CariNamaViewModel(
            school,
            classInfo,
            students,
            onBackRequested: () => { },
            onStudentSelected: _ => { },
            onMissingStudentRequested: () => { }
        );

        var aishahCards = vm.FilteredPupilCards
            .Where(c => c.Student != null && c.Student.Name.Contains("Nur Aishah"))
            .ToList();

        Assert.Equal(3, aishahCards.Count);

        // Ensure each label is distinct and disambiguated
        var displayNames = aishahCards.Select(c => c.DisplayName).Distinct().ToList();
        Assert.Equal(3, displayNames.Count);
        Assert.Contains(aishahCards, c => c.DisplayName.Contains("Nur Aishah"));
    }

    [Fact]
    public void SelectCard_WhenNormalPupil_InvokesOnStudentSelected()
    {
        var school = SampleDataService.CreateSampleSchool();
        var classInfo = new ClassInfo { Id = "2_cemerlang", Name = "2 Cemerlang", Grade = 2, ColourIndex = 0 };
        var students = SampleDataService.CreateSampleClassStudents("2_cemerlang");
        Student? selectedStudent = null;

        var vm = new CariNamaViewModel(
            school,
            classInfo,
            students,
            onBackRequested: () => { },
            onStudentSelected: s => selectedStudent = s,
            onMissingStudentRequested: () => { }
        );

        var targetCard = vm.FilteredPupilCards.First(c => !c.IsMissingEscapeCard);
        vm.SelectCardCommand.Execute(targetCard);

        Assert.NotNull(selectedStudent);
        Assert.Equal(targetCard.Student!.Id, selectedStudent.Id);
    }

    [Fact]
    public void SelectCard_WhenMissingCard_InvokesOnMissingStudentRequested()
    {
        var school = SampleDataService.CreateSampleSchool();
        var classInfo = new ClassInfo { Id = "2_cemerlang", Name = "2 Cemerlang", Grade = 2, ColourIndex = 0 };
        var students = SampleDataService.CreateSampleClassStudents("2_cemerlang");
        bool missingInvoked = false;

        var vm = new CariNamaViewModel(
            school,
            classInfo,
            students,
            onBackRequested: () => { },
            onStudentSelected: _ => { },
            onMissingStudentRequested: () => missingInvoked = true
        );

        var escapeCard = vm.FilteredPupilCards.Last();
        Assert.True(escapeCard.IsMissingEscapeCard);

        vm.SelectCardCommand.Execute(escapeCard);
        Assert.True(missingInvoked);
    }
}
