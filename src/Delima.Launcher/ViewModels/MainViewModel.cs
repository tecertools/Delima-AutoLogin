using CommunityToolkit.Mvvm.ComponentModel;
using Delima.Core.Roster;
using Delima.Core.Store;
using Delima.Launcher.Services;
using ClassInfo = Delima.Core.Roster.ClassInfo;

namespace Delima.Launcher.ViewModels;

/// <summary>
/// Root ViewModel coordinating screen navigation and roster data.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableObject? _currentView;

    public School School { get; private set; }
    public ThemeInfo Theme { get; private set; }
    public List<ClassInfo> Classes { get; private set; }
    public List<Student> Students { get; private set; }
    public ClassInfo? LastClass { get; private set; }

    public MainViewModel()
    {
        // Load default/sample school dataset
        School = SampleDataService.CreateSampleSchool();
        Theme = SampleDataService.CreateSampleTheme();
        Classes = SampleDataService.CreateSampleClasses();
        Students = SampleDataService.CreateSampleClassStudents("2_cemerlang");

        // Set initial LastClass (e.g. 2 Cemerlang)
        LastClass = Classes.FirstOrDefault(c => c.Id == "2_cemerlang") ?? Classes.FirstOrDefault();

        NavigateToPilihKelas();
    }

    public MainViewModel(
        School school,
        ThemeInfo theme,
        List<ClassInfo> classes,
        List<Student> students,
        ClassInfo? lastClass = null)
    {
        School = school;
        Theme = theme;
        Classes = classes;
        Students = students;
        LastClass = lastClass;

        NavigateToPilihKelas();
    }

    public void NavigateToPilihKelas()
    {
        CurrentView = new PilihKelasViewModel(
            School,
            Classes,
            LastClass,
            onClassConfirmed: OnClassConfirmed,
            onTeacherModeRequested: OnTeacherModeRequested
        );
    }

    public void NavigateToCariNama(ClassInfo classInfo)
    {
        // Filter students for this class or create sample students
        var classStudents = Students.Where(s => s.ClassId == classInfo.Id).ToList();
        if (classStudents.Count == 0)
        {
            classStudents = SampleDataService.CreateSampleClassStudents(classInfo.Id);
        }

        CurrentView = new CariNamaViewModel(
            School,
            classInfo,
            classStudents,
            onBackRequested: NavigateToPilihKelas,
            onStudentSelected: OnStudentSelected,
            onMissingStudentRequested: OnMissingStudentRequested
        );
    }

    private void OnClassConfirmed(ClassInfo selectedClass)
    {
        LastClass = selectedClass;
        NavigateToCariNama(selectedClass);
    }

    private void OnStudentSelected(Student student)
    {
        if (LastClass != null)
        {
            NavigateToKataLaluanGambar(student, LastClass);
        }
    }

    public void NavigateToKataLaluanGambar(Student student, ClassInfo classInfo)
    {
        CurrentView = new KataLaluanGambarViewModel(
            School,
            classInfo,
            student,
            onBackRequested: () => NavigateToCariNama(classInfo),
            onSuccess: OnPicturePasswordVerified
        );
    }

    private void OnPicturePasswordVerified(Student student)
    {
        // Successful picture password verification (closes Blocker B1)
        // Passes pupil to next step (Pergi ke Mana / launch)
        System.Diagnostics.Debug.WriteLine($"Picture password verified for pupil: {student.DisplayName} ({student.Id})");
    }

    private void OnMissingStudentRequested()
    {
        // Escape hatch: "Nama saya tiada"
        System.Diagnostics.Debug.WriteLine("Missing student escape hatch clicked.");
    }

    private void OnTeacherModeRequested()
    {
        // Placeholder for Mod Guru PIN dialog
        System.Diagnostics.Debug.WriteLine("Teacher mode requested.");
    }
}
