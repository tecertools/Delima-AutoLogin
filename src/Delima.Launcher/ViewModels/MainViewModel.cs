using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Delima.Core.Roster;
using Delima.Core.Store;
using Delima.Launcher.Services;
using Delima.Launcher.Views;
using Delima.Win32;
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
    public ICredentialStore? CredentialStore { get; private set; }

    private FloatingResetBarWindow? _resetBarWindow;

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
        ClassInfo? lastClass = null,
        ICredentialStore? credentialStore = null)
    {
        School = school;
        Theme = theme;
        Classes = classes;
        Students = students;
        LastClass = lastClass;
        CredentialStore = credentialStore;

        NavigateToPilihKelas();
    }

    public void NavigateToPilihKelas()
    {
        CloseResetBar();

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
        CloseResetBar();

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
        CloseResetBar();

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
        ICredential? credential = null;
        try
        {
            if (CredentialStore != null && CredentialStore.HasCredential(student.Id))
            {
                credential = CredentialStore.OpenCredential(student.Id);
            }
            else
            {
                // Fallback sample credential for development and test harnesses
                credential = new SecurePasswordBuffer("StandardPassword123!"u8);
            }

            NavigateToSedangMasuk(student, credential);
        }
        catch (Exception ex)
        {
            credential?.Dispose();
            NavigateToRalat(student, FailureCodes.E09_StoreDecryptFailure, customTeacherAction: ex.Message);
        }
    }

    public void NavigateToSedangMasuk(Student student, ICredential credential)
    {
        CurrentView = new SedangMasukViewModel(
            School,
            student,
            credential,
            onSuccess: session => OnInjectionSucceeded(student, session),
            onFailure: result => OnInjectionFailed(student, result),
            onCancel: () =>
            {
                if (LastClass != null) NavigateToCariNama(LastClass);
                else NavigateToPilihKelas();
            }
        );
    }

    private void OnInjectionSucceeded(Student student, ChromeSession session)
    {
        // Display Floating Reset Bar (PRD §7.4) and minimize Launcher window
        try
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (Application.Current?.MainWindow != null)
                {
                    Application.Current.MainWindow.WindowState = WindowState.Minimized;
                }

                _resetBarWindow = new FloatingResetBarWindow(student, session, onReset: () =>
                {
                    if (Application.Current?.MainWindow != null)
                    {
                        Application.Current.MainWindow.WindowState = WindowState.Normal;
                        Application.Current.MainWindow.Activate();
                    }

                    if (LastClass != null) NavigateToCariNama(LastClass);
                    else NavigateToPilihKelas();
                });
                _resetBarWindow.Show();
            });
        }
        catch
        {
            // If running in headless test environment without WPF Application instance
        }
    }

    private void OnInjectionFailed(Student student, RouteCResult result)
    {
        NavigateToRalat(
            student,
            result.ErrorCode ?? FailureCodes.E02_WindowNotVerified,
            result.PupilMessage,
            result.TeacherAction);
    }

    public void NavigateToRalat(
        Student? student,
        string errorCode,
        string? customPupilMessage = null,
        string? customTeacherAction = null)
    {
        CloseResetBar();

        CurrentView = new RalatViewModel(
            School,
            errorCode,
            onRetry: () =>
            {
                if (LastClass != null) NavigateToCariNama(LastClass);
                else NavigateToPilihKelas();
            },
            student: student,
            customPupilMessage: customPupilMessage,
            customTeacherAction: customTeacherAction
        );
    }

    private void CloseResetBar()
    {
        try
        {
            _resetBarWindow?.Close();
            _resetBarWindow = null;
        }
        catch
        {
            // Ignore
        }
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
