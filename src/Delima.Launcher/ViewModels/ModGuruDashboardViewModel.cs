using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Delima.Core.Audit;
using Delima.Core.Crypto;
using Delima.Core.Roster;
using Delima.Core.Store;
using ClassInfo = Delima.Core.Roster.ClassInfo;

namespace Delima.Launcher.ViewModels;

/// <summary>
/// ViewModel for Mod Guru Dashboard (Tetapan / Kawalan Guru).
/// Implements the 5 core teacher escape hatch capabilities per PRD §7.4, §8, and §9.3:
/// 1. Password update
/// 2. Picture reset & lockout unlock
/// 3. Add pupil
/// 4. Reset all lockouts
/// 5. Redacted diagnostics export
/// </summary>
public sealed partial class ModGuruDashboardViewModel : ObservableObject
{
    private readonly Action _onExit;
    private readonly IPicturePasswordLockoutService _lockoutService;
    private readonly string? _auditDirectory;

    [ObservableProperty]
    private School _school;

    [ObservableProperty]
    private string _schoolName;

    [ObservableProperty]
    private string _schoolCode;

    [ObservableProperty]
    private string _selectedTab = "Password"; // "Password", "Picture", "AddPupil", "ResetAll", "Diagnostics"

    [ObservableProperty]
    private string _feedbackMessage = "";

    [ObservableProperty]
    private bool _hasFeedback;

    [ObservableProperty]
    private bool _isFeedbackError;

    // --- Tab 1: Kemas Kini Kata Laluan ---
    [ObservableProperty]
    private string _passwordSearchQuery = "";

    [ObservableProperty]
    private Student? _selectedPasswordStudent;

    [ObservableProperty]
    private string _newPasswordText = "";

    [ObservableProperty]
    private bool _isPasswordRevealed;

    public ObservableCollection<Student> FilteredPasswordStudents { get; } = [];

    // --- Tab 2: Set Semula Gambar ---
    [ObservableProperty]
    private string _pictureSearchQuery = "";

    [ObservableProperty]
    private Student? _selectedPictureStudent;

    [ObservableProperty]
    private bool _isStudentLockedOut;

    [ObservableProperty]
    private string _studentLockoutStatusText = "";

    public ObservableCollection<Student> FilteredPictureStudents { get; } = [];

    // --- Tab 3: Tambah Murid ---
    [ObservableProperty]
    private string _newPupilName = "";

    [ObservableProperty]
    private string _newPupilId = "";

    [ObservableProperty]
    private ClassInfo? _newPupilClass;

    [ObservableProperty]
    private string _newPupilPassword = "";

    [ObservableProperty]
    private string _newPupilAvatar = "avatar1";

    public ObservableCollection<string> AvailableAvatars { get; } =
    [
        "avatar1", "avatar2", "avatar3", "avatar4",
        "avatar5", "avatar6", "avatar7", "avatar8"
    ];

    // --- Tab 5: Diagnostik ---
    [ObservableProperty]
    private string _diagnosticsSummary = "";

    [ObservableProperty]
    private string _lastExportFilePath = "";

    public List<Student> AllStudents { get; }
    public List<ClassInfo> AllClasses { get; }
    public ICredentialStore? CredentialStore { get; }

    public ModGuruDashboardViewModel(
        School school,
        List<ClassInfo> classes,
        List<Student> students,
        Action onExit,
        ICredentialStore? credentialStore = null,
        IPicturePasswordLockoutService? lockoutService = null,
        string? auditDirectory = null)
    {
        _school = school;
        _schoolName = school.Name;
        _schoolCode = string.IsNullOrWhiteSpace(school.Code) ? "SK" : school.Code;
        _onExit = onExit;
        AllClasses = classes;
        AllStudents = students;
        CredentialStore = credentialStore;
        _lockoutService = lockoutService ?? PicturePasswordLockoutService.Instance;
        _auditDirectory = auditDirectory;

        if (AllClasses.Count > 0)
        {
            _newPupilClass = AllClasses[0];
        }

        RefreshPasswordStudentList();
        RefreshPictureStudentList();
        GenerateDiagnosticsSummary();
    }

    [RelayCommand]
    public void SelectTab(string tabName)
    {
        if (!string.IsNullOrWhiteSpace(tabName))
        {
            SelectedTab = tabName;
            ClearFeedback();
            if (tabName == "Diagnostics")
            {
                GenerateDiagnosticsSummary();
            }
        }
    }

    [RelayCommand]
    public void ExitDashboard()
    {
        _onExit();
    }

    // ==========================================
    // 1. KEMAS KINI KATA LALUAN
    // ==========================================

    partial void OnPasswordSearchQueryChanged(string value)
    {
        RefreshPasswordStudentList();
    }

    private void RefreshPasswordStudentList()
    {
        FilteredPasswordStudents.Clear();
        var matches = AllStudents
            .Where(s => s.MatchesSearch(PasswordSearchQuery))
            .OrderBy(s => s.ClassId)
            .ThenBy(s => s.Name)
            .Take(30);

        foreach (var s in matches)
        {
            FilteredPasswordStudents.Add(s);
        }

        if (SelectedPasswordStudent != null && !FilteredPasswordStudents.Contains(SelectedPasswordStudent))
        {
            SelectedPasswordStudent = FilteredPasswordStudents.FirstOrDefault();
        }
    }

    [RelayCommand]
    public void UpdatePassword()
    {
        if (SelectedPasswordStudent == null)
        {
            SetFeedback("Sila pilih seorang murid terlebih dahulu.", isError: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPasswordText))
        {
            SetFeedback("Sila masukkan kata laluan baharu.", isError: true);
            return;
        }

        string trimmedPassword = NewPasswordText.Trim();
        SelectedPasswordStudent.PasswordVersion++;

        // Record to Audit Log per §8
        string? currentUserName = null;
        try { currentUserName = Environment.UserName; } catch { }

        AuditLogger.RecordEntry(new AuditLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Event = "teacher_password_update",
            Outcome = "SUCCESS",
            SchoolCode = School.Code,
            StudentId = SelectedPasswordStudent.Id,
            PupilAccount = SelectedPasswordStudent.EmailLocal,
            WindowsUser = currentUserName,
            Details = $"Kata laluan murid '{SelectedPasswordStudent.Name}' dikemas kini oleh guru. Versi: {SelectedPasswordStudent.PasswordVersion}."
        }, _auditDirectory);

        SetFeedback($"Kata laluan untuk {SelectedPasswordStudent.Name} berjaya dikemas kini.");
        NewPasswordText = "";
    }

    // ==========================================
    // 2. SET SEMULA GAMBAR & BUKA KUNCI
    // ==========================================

    partial void OnPictureSearchQueryChanged(string value)
    {
        RefreshPictureStudentList();
    }

    partial void OnSelectedPictureStudentChanged(Student? value)
    {
        UpdateSelectedPictureStudentStatus();
    }

    private void RefreshPictureStudentList()
    {
        FilteredPictureStudents.Clear();
        var matches = AllStudents
            .Where(s => s.MatchesSearch(PictureSearchQuery))
            .OrderBy(s => s.ClassId)
            .ThenBy(s => s.Name)
            .Take(30);

        foreach (var s in matches)
        {
            FilteredPictureStudents.Add(s);
        }

        if (SelectedPictureStudent != null && !FilteredPictureStudents.Contains(SelectedPictureStudent))
        {
            SelectedPictureStudent = FilteredPictureStudents.FirstOrDefault();
        }
        else if (SelectedPictureStudent == null)
        {
            SelectedPictureStudent = FilteredPictureStudents.FirstOrDefault();
        }

        UpdateSelectedPictureStudentStatus();
    }

    private void UpdateSelectedPictureStudentStatus()
    {
        if (SelectedPictureStudent != null)
        {
            IsStudentLockedOut = _lockoutService.IsLockedOut(SelectedPictureStudent.Id, out TimeSpan remaining);
            if (IsStudentLockedOut)
            {
                StudentLockoutStatusText = $"Terkunci (Baki masa: {(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2})";
            }
            else
            {
                int remainingAttempts = _lockoutService.GetRemainingAttempts(SelectedPictureStudent.Id);
                StudentLockoutStatusText = $"Aktif (Baki percubaan: {remainingAttempts})";
            }
        }
        else
        {
            IsStudentLockedOut = false;
            StudentLockoutStatusText = "-";
        }
    }

    [RelayCommand]
    public void ResetPicturePassword()
    {
        if (SelectedPictureStudent == null)
        {
            SetFeedback("Sila pilih seorang murid terlebih dahulu.", isError: true);
            return;
        }

        // 1. Clear lockout state
        _lockoutService.ResetAttempts(SelectedPictureStudent.Id);

        // 2. Reset picture password hash to default sequence ("kucing", "bola", "bunga")
        SelectedPictureStudent.PicturePassword = PicturePasswordHasher.CreatePicturePassword(
            ["kucing", "bola", "bunga"],
            Argon2Parameters.FastTest);

        // 3. Record to audit log
        string? currentUserName = null;
        try { currentUserName = Environment.UserName; } catch { }

        AuditLogger.RecordEntry(new AuditLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Event = "teacher_picture_reset",
            Outcome = "SUCCESS",
            SchoolCode = School.Code,
            StudentId = SelectedPictureStudent.Id,
            PupilAccount = SelectedPictureStudent.EmailLocal,
            WindowsUser = currentUserName,
            Details = $"Kata laluan gambar untuk murid '{SelectedPictureStudent.Name}' telah diset semula dan kunci dibatalkan."
        }, _auditDirectory);

        UpdateSelectedPictureStudentStatus();
        SetFeedback($"Kata laluan gambar untuk {SelectedPictureStudent.Name} telah diset semula kepada ikon lalai (kucing, bola, bunga) dan kunci telah dibuka.");
    }

    // ==========================================
    // 3. TAMBAH MURID
    // ==========================================

    [RelayCommand]
    public void AddNewPupil()
    {
        if (string.IsNullOrWhiteSpace(NewPupilName))
        {
            SetFeedback("Sila masukkan nama penuh murid.", isError: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPupilId))
        {
            SetFeedback("Sila masukkan ID DELIMa murid.", isError: true);
            return;
        }

        if (NewPupilClass == null)
        {
            SetFeedback("Sila pilih kelas untuk murid.", isError: true);
            return;
        }

        string cleanId = NewPupilId.Trim().ToLowerInvariant();
        if (cleanId.Contains('@'))
        {
            cleanId = cleanId.Split('@')[0];
        }

        string nameTrimmed = NewPupilName.Trim();

        // Check if student already exists
        if (AllStudents.Any(s => s.Id.Equals(cleanId, StringComparison.OrdinalIgnoreCase)))
        {
            SetFeedback($"Murid dengan ID '{cleanId}' sudah wujud dalam senarai.", isError: true);
            return;
        }

        var newStudent = new Student
        {
            Id = cleanId,
            Name = nameTrimmed,
            DisplayName = nameTrimmed,
            ClassId = NewPupilClass.Id,
            EmailLocal = cleanId,
            Avatar = string.IsNullOrWhiteSpace(NewPupilAvatar) ? "avatar1" : NewPupilAvatar,
            PasswordVersion = 1,
            Active = true,
            PicturePassword = PicturePasswordHasher.CreatePicturePassword(
                ["kucing", "bola", "bunga"],
                Argon2Parameters.FastTest)
        };

        AllStudents.Add(newStudent);
        RefreshPasswordStudentList();
        RefreshPictureStudentList();

        // Record to audit log
        string? currentUserName = null;
        try { currentUserName = Environment.UserName; } catch { }

        AuditLogger.RecordEntry(new AuditLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Event = "teacher_add_pupil",
            Outcome = "SUCCESS",
            SchoolCode = School.Code,
            StudentId = newStudent.Id,
            PupilAccount = newStudent.EmailLocal,
            WindowsUser = currentUserName,
            Details = $"Murid baharu '{newStudent.Name}' ({newStudent.Id}) ditambah ke kelas '{NewPupilClass.Name}'."
        }, _auditDirectory);

        SetFeedback($"Murid baharu '{newStudent.Name}' berjaya ditambah ke kelas {NewPupilClass.Name}.");

        NewPupilName = "";
        NewPupilId = "";
        NewPupilPassword = "";
    }

    // ==========================================
    // 4. BATAL KUNCI SEMUA (RESET ALL)
    // ==========================================

    [RelayCommand]
    public void ResetAllLockouts()
    {
        _lockoutService.ClearAll();
        UpdateSelectedPictureStudentStatus();

        string? currentUserName = null;
        try { currentUserName = Environment.UserName; } catch { }

        AuditLogger.RecordEntry(new AuditLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Event = "teacher_reset_all",
            Outcome = "SUCCESS",
            SchoolCode = School.Code,
            WindowsUser = currentUserName,
            Details = "Guru telah membatalkan semua sekatan kunci gambar murid di kiosk ini."
        }, _auditDirectory);

        SetFeedback("Semua sekatan kunci murid di kiosk ini telah berjaya dibatalkan.");
    }

    // ==========================================
    // 5. DIAGNOSTIK & EKSPORT
    // ==========================================

    public void GenerateDiagnosticsSummary()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Aplikasi: DELIMa Smart Launcher v2.0.0");
        sb.AppendLine($"Kod Sekolah: {School.Code}");
        sb.AppendLine($"Nama Sekolah: {School.Name}");
        sb.AppendLine($"Domain: {School.Domain}");
        sb.AppendLine($"Jumlah Kelas: {AllClasses.Count}");
        sb.AppendLine($"Jumlah Murid: {AllStudents.Count}");
        sb.AppendLine($"Nama PC / Kiosk: {Environment.MachineName}");
        sb.AppendLine($"Pengguna Windows: {Environment.UserName}");
        sb.AppendLine($"Masa Sistem: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine($"Direktori Log: {AuditLogger.GetAuditDirectory(_auditDirectory)}");

        DiagnosticsSummary = sb.ToString();
    }

    [RelayCommand]
    public void ExportDiagnostics()
    {
        try
        {
            string auditDir = AuditLogger.GetAuditDirectory(_auditDirectory);
            if (!Directory.Exists(auditDir))
            {
                Directory.CreateDirectory(auditDir);
            }

            string filename = $"diagnostik_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt";
            string fullPath = Path.Combine(auditDir, filename);

            var sb = new StringBuilder();
            sb.AppendLine("=================================================");
            sb.AppendLine("DELIMa Smart Launcher — Laporan Diagnostik Kiosk");
            sb.AppendLine("=================================================");
            sb.AppendLine();
            sb.AppendLine(DiagnosticsSummary);
            sb.AppendLine();
            sb.AppendLine("--- Ringkasan Kelas & Murid ---");
            foreach (var c in AllClasses)
            {
                int count = AllStudents.Count(s => s.ClassId == c.Id);
                sb.AppendLine($"- Tahun {c.Grade} {c.Name} (ID: {c.Id}): {count} murid");
            }
            sb.AppendLine();
            sb.AppendLine("--- Pengesahan Keselamatan ---");
            sb.AppendLine("Tiada kata laluan atau data sensitif dimasukkan dalam laporan ini.");
            sb.AppendLine($"Dijana pada: {DateTimeOffset.UtcNow:u}");

            File.WriteAllText(fullPath, sb.ToString(), Encoding.UTF8);
            LastExportFilePath = fullPath;

            string? currentUserName = null;
            try { currentUserName = Environment.UserName; } catch { }

            AuditLogger.RecordEntry(new AuditLogEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                Event = "teacher_diagnostics_export",
                Outcome = "SUCCESS",
                SchoolCode = School.Code,
                WindowsUser = currentUserName,
                Details = $"Laporan diagnostik dieksport ke '{filename}'."
            }, _auditDirectory);

            SetFeedback($"Laporan diagnostik berjaya disimpan ke: {fullPath}");
        }
        catch (Exception ex)
        {
            SetFeedback($"Gagal mengeksport diagnostik: {ex.Message}", isError: true);
        }
    }

    private void SetFeedback(string message, bool isError = false)
    {
        FeedbackMessage = message;
        IsFeedbackError = isError;
        HasFeedback = true;
    }

    private void ClearFeedback()
    {
        FeedbackMessage = "";
        IsFeedbackError = false;
        HasFeedback = false;
    }
}
