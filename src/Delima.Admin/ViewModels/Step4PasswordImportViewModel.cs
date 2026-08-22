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

    [ObservableProperty]
    private string _filterText = "";

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

    public void InitializeGridFromRoster()
    {
        PasswordItems.Clear();

        foreach (var student in _state.RosterStudents)
        {
            string? pwd = _state.StudentPasswords.TryGetValue(student.Id, out var p) ? p : null;
            PasswordItems.Add(new PasswordGridItem
            {
                StudentId = student.Id,
                StudentName = student.FullName,
                ClassName = student.ClassName,
                DelimaDigits = student.DelimaDigits,
                EmailLocal = student.EmailLocal,
                RegisterNo = student.RegisterNoJoinKey,
                RawPassword = pwd
            });
        }

        RecalculateSharedPasswords();
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

    public void SavePasswordTemplate(string targetPath)
    {
        TemplateGenerator.SavePasswordTemplate(targetPath, _state.RosterStudents);
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
