using Delima.Core.Crypto;
using Delima.Core.Roster;
using Delima.Core.Store;
using Delima.Launcher.Services;
using Delima.Launcher.ViewModels;
using Xunit;
using ClassInfo = Delima.Core.Roster.ClassInfo;

namespace Delima.Launcher.Tests;

public class PilihDestinasiViewModelTests
{
    [Fact]
    public void PilihDestinasiViewModel_Initializes_With_Defaults_When_No_Destinations_Provided()
    {
        var school = SampleDataService.CreateSampleSchool();
        var classes = SampleDataService.CreateSampleClasses();
        var student = SampleDataService.CreateSampleClassStudents("2_cemerlang")[0];
        using var credential = new SecurePasswordBuffer("Password123!"u8);

        var vm = new PilihDestinasiViewModel(
            school: school,
            classInfo: classes[0],
            student: student,
            credential: credential,
            destinations: null,
            onDestinationSelected: _ => { },
            onBackRequested: () => { });

        Assert.Equal(school.Name, vm.SchoolName);
        Assert.Equal(student.DisplayName, vm.PupilName);
        Assert.Equal(classes[0].Name, vm.ClassName);
        Assert.Contains(student.DisplayName, vm.GreetingTitle);
        Assert.NotEmpty(vm.AvailableDestinations);
        Assert.True(vm.AvailableDestinations.Count >= 4);
    }

    [Fact]
    public void PilihDestinasiViewModel_Uses_Configured_Destinations_When_Provided()
    {
        var school = SampleDataService.CreateSampleSchool();
        var classes = SampleDataService.CreateSampleClasses();
        var student = SampleDataService.CreateSampleClassStudents("2_cemerlang")[0];
        using var credential = new SecurePasswordBuffer("Password123!"u8);

        var customDestinations = new List<DestinationConfig>
        {
            new() { Id = "portal_sekolah", Label = "Portal Rasmi SK Seri Cemerlang", Url = "https://sksericemerlang.edu.my" },
            new() { Id = "canva_lab", Label = "Canva Makmal", Url = "https://www.canva.com/education/" }
        };

        var vm = new PilihDestinasiViewModel(
            school: school,
            classInfo: classes[0],
            student: student,
            credential: credential,
            destinations: customDestinations,
            onDestinationSelected: _ => { },
            onBackRequested: () => { });

        Assert.Equal(2, vm.AvailableDestinations.Count);
        Assert.Equal("Portal Rasmi SK Seri Cemerlang", vm.AvailableDestinations[0].Label);
        Assert.Equal("Canva Makmal", vm.AvailableDestinations[1].Label);
    }

    [Fact]
    public void PilihDestinasiViewModel_SelectDestinationCommand_InvokesCallback()
    {
        var school = SampleDataService.CreateSampleSchool();
        var classes = SampleDataService.CreateSampleClasses();
        var student = SampleDataService.CreateSampleClassStudents("2_cemerlang")[0];
        using var credential = new SecurePasswordBuffer("Password123!"u8);

        DestinationConfig? selected = null;

        var vm = new PilihDestinasiViewModel(
            school: school,
            classInfo: classes[0],
            student: student,
            credential: credential,
            destinations: null,
            onDestinationSelected: dest => selected = dest,
            onBackRequested: () => { });

        var targetCard = vm.AvailableDestinations[0];
        vm.SelectDestinationCommand.Execute(targetCard);

        Assert.NotNull(selected);
        Assert.Equal(targetCard.Config.Id, selected.Id);
    }

    [Fact]
    public void PilihDestinasiViewModel_BackCommand_InvokesCallback()
    {
        var school = SampleDataService.CreateSampleSchool();
        var classes = SampleDataService.CreateSampleClasses();
        var student = SampleDataService.CreateSampleClassStudents("2_cemerlang")[0];
        using var credential = new SecurePasswordBuffer("Password123!"u8);

        bool backCalled = false;

        var vm = new PilihDestinasiViewModel(
            school: school,
            classInfo: classes[0],
            student: student,
            credential: credential,
            destinations: null,
            onDestinationSelected: _ => { },
            onBackRequested: () => backCalled = true);

        vm.BackCommand.Execute(null);
        Assert.True(backCalled);
    }

    [Fact]
    public void PilihDestinasiViewModel_OpenTeacherModeCommand_InvokesCallback()
    {
        var school = SampleDataService.CreateSampleSchool();
        var classes = SampleDataService.CreateSampleClasses();
        var student = SampleDataService.CreateSampleClassStudents("2_cemerlang")[0];
        using var credential = new SecurePasswordBuffer("Password123!"u8);

        bool teacherModeCalled = false;

        var vm = new PilihDestinasiViewModel(
            school: school,
            classInfo: classes[0],
            student: student,
            credential: credential,
            destinations: null,
            onDestinationSelected: _ => { },
            onBackRequested: () => { },
            onTeacherModeRequested: () => teacherModeCalled = true);

        vm.OpenTeacherModeCommand.Execute(null);
        Assert.True(teacherModeCalled);
    }

    [Theory]
    [InlineData("delima", "DELIMa 3.0", "https://d3.delima.edu.my", "🎓", "Utama")]
    [InlineData("classroom", "Google Classroom", "https://classroom.google.com", "📚", "Google")]
    [InlineData("ains", "AINS NILAM", "https://ains.moe.gov.my", "📖", "KPM")]
    [InlineData("canva", "Canva for Education", "https://www.canva.com/login/", "🎨", "Kreatif")]
    [InlineData("textbook", "Buku Teks Digital", "https://textbook.moe.gov.my", "📕", "KPM")]
    [InlineData("custom", "Laman Sekolah", "https://sekolah.edu.my", "🌐", "Laman")]
    public void DestinationCardViewModel_Infers_Metadata_Correctly(string id, string label, string url, string expectedIcon, string expectedBadge)
    {
        var config = new DestinationConfig { Id = id, Label = label, Url = url };
        var card = new DestinationCardViewModel(config);

        Assert.Equal(expectedIcon, card.IconEmoji);
        Assert.Equal(expectedBadge, card.BadgeText);
        Assert.NotEmpty(card.Subtitle);
        Assert.NotEmpty(card.AccentColor);
        Assert.NotEmpty(card.AccessibleName);
    }
}
