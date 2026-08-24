using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Delima.Admin.Models;
using Delima.Core.Audit;
using Delima.Import;

namespace Delima.Admin.ViewModels;

public sealed partial class Step4PasswordImportViewModel : ObservableObject
{
    private readonly AdminWizardState _state;
    private DispatcherTimer? _reMaskTimer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProceed))]
    private string _activeSubView = "Consent"; // "Consent" or "Grid"

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConsentIsMatched))]
    [NotifyPropertyChangedFor(nameof(CanProceedConsent))]
    [NotifyPropertyChangedFor(nameof(ConsentValidationMessage))]
    private string _consentTypedCode = "";

    public bool ConsentIsMatched =>
        string.Equals(ConsentTypedCode.Trim(), _state.School.Code.Trim(), StringComparison.OrdinalIgnoreCase);

    public bool CanProceedConsent => ConsentIsMatched;

    public string ConsentValidationMessage =>
        ConsentIsMatched ? "" : $"Sila taip kod sekolah yang betul ('{_state.School.Code}') untuk meneruskan.";

    public ObservableCollection<PasswordGridItem> PasswordItems { get; } = [];
    public ObservableCollection<PasswordGridItem> FilteredPasswordItems { get; } = [];
    public ObservableCollection<string> YearNames { get; } = [];
    public ObservableCollection<string> ClassNames { get; } = [];

    [ObservableProperty]
    private string? _selectedYearFilter;

    [ObservableProperty]
    private string? _selectedClassFilter;

    [ObservableProperty]
    private PasswordGridItem? _activePopoverItem;

    [ObservableProperty]
    private bool _isPopoverOpen;

    [ObservableProperty]
    private string _popoverPassphrase = "";

    [ObservableProperty]
    private string _popoverError = "";

    public int TotalPupilsCount => PasswordItems.Count;
    public int WithPasswordCount => PasswordItems.Count(p => p.HasPassword);
    public int MissingPasswordCount => PasswordItems.Count(p => !p.HasPassword);
    public int SharedPasswordCount => PasswordItems.Count(p => p.IsShared);

    public int FilteredTotalCount => FilteredPasswordItems.Count;
    public int FilteredWithCount => FilteredPasswordItems.Count(p => p.HasPassword);
    public int FilteredMissingCount => FilteredPasswordItems.Count(p => !p.HasPassword);
    public int FilteredSharedCount => FilteredPasswordItems.Count(p => p.IsShared);

    public bool CanProceed => ActiveSubView == "Grid";

    public Step4PasswordImportViewModel(AdminWizardState state)
    {
        _state = state;
        if (state.HasAcknowledgedConsent)
        {
            _activeSubView = "Grid";
            _consentTypedCode = state.School.Code;
        }

        InitializeGridFromRoster();
    }

    partial void OnSelectedYearFilterChanged(string? value)
    {
        UpdateClassListForSelectedYear();
        UpdateFilteredPasswordItems();
    }

    partial void OnSelectedClassFilterChanged(string? value)
    {
        UpdateFilteredPasswordItems();
    }

    private void UpdateClassListForSelectedYear()
    {
        string? previousClassSelection = SelectedClassFilter;
        ClassNames.Clear();

        int filterGrade = ParseYearFilterToGrade(SelectedYearFilter);

        var query = PasswordItems.AsEnumerable();
        if (filterGrade > 0)
        {
            query = query.Where(a => a.Grade == filterGrade);
        }

        var classes = query.Select(s => s.ClassName)
                           .Where(c => !string.IsNullOrWhiteSpace(c))
                           .Distinct()
                           .OrderBy(c => c)
                           .ToList();

        ClassNames.Add("Semua Kelas");
        foreach (var c in classes)
        {
            ClassNames.Add(c);
        }

        SelectedClassFilter = "Semua Kelas";
    }

    public static int ParseYearFilterToGrade(string? yearFilter)
    {
        if (string.IsNullOrWhiteSpace(yearFilter) || yearFilter.Equals("Semua Tahun", StringComparison.OrdinalIgnoreCase))
            return 0;

        string digits = new(yearFilter.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out int g) ? g : 0;
    }

    public void UpdateFilteredPasswordItems()
    {
        FilteredPasswordItems.Clear();
        int filterGrade = ParseYearFilterToGrade(SelectedYearFilter);
        var classFilter = SelectedClassFilter;

        IEnumerable<PasswordGridItem> items = PasswordItems;

        if (filterGrade > 0)
        {
            items = items.Where(a => a.Grade == filterGrade);
        }

        if (!string.IsNullOrWhiteSpace(classFilter) && !classFilter.Equals("Semua Kelas", StringComparison.OrdinalIgnoreCase))
        {
            items = items.Where(a => string.Equals(a.ClassName, classFilter, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in items)
        {
            FilteredPasswordItems.Add(item);
        }

        NotifyCountersChanged();
    }

    public void InitializeGridFromRoster()
    {
        PasswordItems.Clear();
        YearNames.Clear();
        ClassNames.Clear();

        // Populate YearNames (Semua Tahun, Tahun 1..6)
        var distinctGrades = _state.RosterStudents.Select(s => s.Grade > 0 ? s.Grade : RosterImporter.NormalizeClassAndGrade(s.ClassName, null).Grade)
                                                 .Where(g => g >= 1 && g <= 6)
                                                 .Distinct()
                                                 .OrderBy(g => g)
                                                 .ToList();

        YearNames.Add("Semua Tahun");
        if (distinctGrades.Count > 0)
        {
            foreach (var g in distinctGrades)
            {
                YearNames.Add($"Tahun {g}");
            }
        }
        else
        {
            for (int g = 1; g <= 6; g++)
            {
                YearNames.Add($"Tahun {g}");
            }
        }

        foreach (var student in _state.RosterStudents)
        {
            int grade = student.Grade > 0 ? student.Grade : RosterImporter.NormalizeClassAndGrade(student.ClassName, null).Grade;
            string? pwd = _state.StudentPasswords.TryGetValue(student.Id, out var p) ? p : null;
            PasswordItems.Add(new PasswordGridItem
            {
                StudentId = student.Id,
                StudentName = student.FullName,
                ClassName = student.ClassName,
                Grade = grade,
                DelimaDigits = student.DelimaDigits,
                EmailLocal = student.EmailLocal,
                RegisterNo = student.RegisterNoJoinKey,
                RawPassword = pwd
            });
        }

        RecalculateSharedPasswords();

        SelectedYearFilter = "Semua Tahun";
        UpdateClassListForSelectedYear();
        UpdateFilteredPasswordItems();
    }

    public void AcknowledgeConsent()
    {
        if (!ConsentIsMatched) return;

        _state.HasAcknowledgedConsent = true;
        ActiveSubView = "Grid";

        AuditLogger.RecordEntry(new AuditLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Event = "consent_acknowledged",
            Outcome = "SUCCESS",
            SchoolCode = _state.School.Code,
            SoftwareVersion = "2.0.0",
            WindowsUser = Environment.UserName,
            Details = "Administrator acknowledged credential storage consent and policy responsibility."
        });

        OnPropertyChanged(nameof(CanProceed));
    }

    public void SavePasswordTemplate(string targetPath, string? yearFilter = null, string? classFilter = null)
    {
        string effectiveYear = yearFilter ?? SelectedYearFilter ?? "Semua Tahun";
        string effectiveClass = classFilter ?? SelectedClassFilter ?? "Semua Kelas";
        int filterGrade = ParseYearFilterToGrade(effectiveYear);

        TemplateGenerator.SavePasswordTemplate(targetPath, _state.RosterStudents, filterGrade, effectiveClass);
    }

    public void LoadPasswordFile(string filePath)
    {
        if (!File.Exists(filePath)) return;

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        string fileName = Path.GetFileName(filePath);
        var (headers, rows, _) = DataFileReader.ReadFile(stream, fileName);

        var mapping = ColumnMapping.AutoDetect(headers);
        string? delimaCol = mapping.DelimaIdColumn;
        string? passCol = mapping.PasswordColumn;
        string? regCol = mapping.RegisterNoColumn;
        string? nameCol = mapping.FullNameColumn;

        // If not detected, look for any column containing 'kata_laluan' or 'password'
        if (passCol == null)
            passCol = headers.FirstOrDefault(h => h.Contains("pass", StringComparison.OrdinalIgnoreCase) ||
                                                  h.Contains("laluan", StringComparison.OrdinalIgnoreCase) ||
                                                  h.Contains("katalaluan", StringComparison.OrdinalIgnoreCase));

        if (delimaCol == null)
            delimaCol = headers.FirstOrDefault(h => h.Contains("delima", StringComparison.OrdinalIgnoreCase) ||
                                                    h.Contains("id", StringComparison.OrdinalIgnoreCase) ||
                                                    h.Contains("emel", StringComparison.OrdinalIgnoreCase));

        var passwordsByDelima = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var passwordsByReg = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var passwordsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in rows)
        {
            string rawDelima = r.GetValue(delimaCol);
            string rawPass = r.GetValue(passCol);
            string rawReg = r.GetValue(regCol);
            string rawName = r.GetValue(nameCol);

            var (digits, _) = RosterImporter.NormalizeDelimaId(rawDelima);
            if (digits != null && !string.IsNullOrWhiteSpace(rawPass))
                passwordsByDelima[digits] = rawPass;

            if (!string.IsNullOrWhiteSpace(rawReg) && !string.IsNullOrWhiteSpace(rawPass))
                passwordsByReg[rawReg] = rawPass;

            if (!string.IsNullOrWhiteSpace(rawName) && !string.IsNullOrWhiteSpace(rawPass))
                passwordsByName[rawName] = rawPass;
        }

        // Apply to items
        foreach (var item in PasswordItems)
        {
            if (passwordsByDelima.TryGetValue(item.DelimaDigits, out var pwd))
            {
                item.RawPassword = pwd;
                _state.StudentPasswords[item.StudentId] = pwd;
            }
            else if (!string.IsNullOrEmpty(item.RegisterNo) && passwordsByReg.TryGetValue(item.RegisterNo, out var pwdReg))
            {
                item.RawPassword = pwdReg;
                _state.StudentPasswords[item.StudentId] = pwdReg;
            }
            else if (passwordsByName.TryGetValue(item.StudentName, out var pwdName))
            {
                item.RawPassword = pwdName;
                _state.StudentPasswords[item.StudentId] = pwdName;
            }
        }

        RecalculateSharedPasswords();
        NotifyCountersChanged();
    }

    public void RecalculateSharedPasswords()
    {
        var passwordCounts = PasswordItems
            .Where(p => !string.IsNullOrEmpty(p.RawPassword))
            .GroupBy(p => p.RawPassword!)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var item in PasswordItems)
        {
            if (!string.IsNullOrEmpty(item.RawPassword) && passwordCounts.TryGetValue(item.RawPassword, out int count) && count > 1)
            {
                item.IsShared = true;
            }
            else
            {
                item.IsShared = false;
            }
        }
    }

    public void OpenRevealPopover(PasswordGridItem item)
    {
        if (!item.HasPassword) return;

        ActivePopoverItem = item;
        PopoverPassphrase = "";
        PopoverError = "";
        IsPopoverOpen = true;
    }

    public bool VerifyAndReveal(string enteredPassphrase)
    {
        if (ActivePopoverItem == null) return false;

        string actualPassphrase = _state.AdminPassphrase ?? "";
        if (enteredPassphrase != actualPassphrase)
        {
            PopoverError = "Kata laluan pentadbir salah.";
            return false;
        }

        // Passphrase correct -> Reveal password for this specific row
        ActivePopoverItem.IsRevealed = true;
        ActivePopoverItem.RevealCountdownSeconds = 10;
        IsPopoverOpen = false;

        // Log to audit log
        AuditLogger.RecordEntry(new AuditLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Event = "password_reveal",
            Outcome = "SUCCESS",
            StudentId = ActivePopoverItem.StudentId,
            PupilAccount = ActivePopoverItem.EmailLocal,
            SchoolCode = _state.School.Code,
            WindowsUser = Environment.UserName,
            Details = $"Administrator revealed password for student ID {ActivePopoverItem.StudentId}."
        });

        // Start 10s auto-re-mask timer
        StartReMaskTimer(ActivePopoverItem);
        return true;
    }

    private void StartReMaskTimer(PasswordGridItem item)
    {
        _reMaskTimer?.Stop();
        _reMaskTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        _reMaskTimer.Tick += (s, e) =>
        {
            if (item.RevealCountdownSeconds > 1)
            {
                item.RevealCountdownSeconds--;
            }
            else
            {
                item.IsRevealed = false;
                item.RevealCountdownSeconds = 0;
                _reMaskTimer?.Stop();
            }
        };

        _reMaskTimer.Start();
    }

    public void MaskAll()
    {
        _reMaskTimer?.Stop();
        foreach (var item in PasswordItems)
        {
            item.IsRevealed = false;
            item.RevealCountdownSeconds = 0;
        }
        IsPopoverOpen = false;
    }

    public static void SecureDeleteFile(string path)
    {
        if (!File.Exists(path)) return;

        try
        {
            long length = new FileInfo(path).Length;
            byte[] zeros = new byte[length];
            File.WriteAllBytes(path, zeros);
            File.Delete(path);
        }
        catch
        {
            // fallback normal delete if overwrite fails
            try { File.Delete(path); } catch { }
        }
    }

    private void NotifyCountersChanged()
    {
        OnPropertyChanged(nameof(TotalPupilsCount));
        OnPropertyChanged(nameof(WithPasswordCount));
        OnPropertyChanged(nameof(MissingPasswordCount));
        OnPropertyChanged(nameof(SharedPasswordCount));

        OnPropertyChanged(nameof(FilteredTotalCount));
        OnPropertyChanged(nameof(FilteredWithCount));
        OnPropertyChanged(nameof(FilteredMissingCount));
        OnPropertyChanged(nameof(FilteredSharedCount));
    }

    public void SaveToState()
    {
        foreach (var item in PasswordItems)
        {
            if (!string.IsNullOrEmpty(item.RawPassword))
                _state.StudentPasswords[item.StudentId] = item.RawPassword;
        }
    }
}

