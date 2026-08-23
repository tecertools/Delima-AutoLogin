using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Delima.Admin.Models;
using Delima.Core.Audit;
using Delima.Core.Crypto;
using Delima.Core.Store;

namespace Delima.Admin.ViewModels;

public sealed partial class Step7ProvisionViewModel : ObservableObject
{
    private readonly AdminWizardState _state;

    [ObservableProperty]
    private string _selectedRoute = "Usb"; // "Usb", "Network", "Script"

    [ObservableProperty]
    private string _usbTargetFolder = "";

    [ObservableProperty]
    private string _networkSharePath = @"\\SERVER\MakmalShare\DELIMa";

    [ObservableProperty]
    private string _powerShellScript = "";

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _isSuccess;

    [ObservableProperty]
    private byte[]? _generatedBundleBytes;

    public ObservableCollection<LabChecklistItem> LabChecklist { get; } = [];

    public Step7ProvisionViewModel(AdminWizardState state)
    {
        _state = state;
        GenerateDefaultScript();
        InitializeLabChecklist();
    }

    private void GenerateDefaultScript()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Skrip Penyediaan Makmal DELIMa (PDQ / GPO)");
        sb.AppendLine($"$PackPath = \"{NetworkSharePath}\\school.dlmpack\"");
        sb.AppendLine("$Pass = Read-Host -Prompt \"Masukkan Kata Laluan Pentadbir\" -AsSecureString");
        sb.AppendLine("$Bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($Pass)");
        sb.AppendLine("$Plain = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($Bstr)");
        sb.AppendLine("$Plain | & \"C:\\Program Files\\DELIMa Launcher\\Delima.Provision.exe\" --quiet --pack $PackPath --passphrase-stdin");
        sb.AppendLine("[System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($Bstr)");
        PowerShellScript = sb.ToString();
    }

    public void InitializeLabChecklist()
    {
        LabChecklist.Clear();
        int pcCount = Math.Max(20, Math.Min(40, _state.RosterStudents.Count > 0 ? _state.RosterStudents.GroupBy(s => s.ClassName).Max(g => g.Count()) : 20));

        for (int i = 1; i <= pcCount; i++)
        {
            LabChecklist.Add(new LabChecklistItem
            {
                PcName = $"MAKMAL-{i:D2}",
                IsProvisioned = false,
                Version = "2.0.0",
                StoreDate = "—"
            });
        }
    }

    public byte[] BuildMasterBundle()
    {
        var payload = new MasterBundlePayload
        {
            SchemaVersion = 2,
            School = _state.School,
            Theme = _state.Theme,
            Config = _state.Config,
            GeneratedAt = DateTimeOffset.UtcNow
        };

        // Convert classes
        var classGroups = _state.RosterStudents.GroupBy(s => s.ClassName).ToList();
        int colourIdx = 0;
        foreach (var group in classGroups)
        {
            var firstStudent = group.First();
            payload.Classes.Add(new ClassInfo
            {
                Id = group.Key,
                Name = group.Key,
                Grade = firstStudent.Grade,
                ColourIndex = colourIdx++ % Math.Max(1, _state.Theme.ClassColours.Count)
            });
        }

        // Convert students
        foreach (var student in _state.RosterStudents)
        {
            string? pwd = _state.StudentPasswords.TryGetValue(student.Id, out var p) ? p : null;
            string avatar = _state.StudentAvatars.TryGetValue(student.Id, out var av) && !string.IsNullOrWhiteSpace(av) ? av : "kucing";

            PicturePasswordInfo? picPwInfo = null;
            if (_state.StudentPicturePasswords.TryGetValue(student.Id, out var picSeq) && picSeq.Count == 3)
            {
                picPwInfo = PicturePasswordHasher.CreatePicturePassword(picSeq);
            }
            else
            {
                picPwInfo = PicturePasswordHasher.CreatePicturePassword(["kucing", "bunga", "kereta"]);
            }

            payload.Students.Add(new StudentInfo
            {
                Id = student.Id,
                Name = student.FullName,
                ClassId = student.ClassName,
                EmailLocal = student.EmailLocal,
                Avatar = avatar,
                Password = pwd,
                PicturePassword = picPwInfo,
                Active = true,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        string passphrase = _state.AdminPassphrase ?? "DEFAULT_PASSPHRASE_12_CHARS";
        byte[] bundleBytes = MasterBundle.Pack(payload, passphrase);
        GeneratedBundleBytes = bundleBytes;

        AuditLogger.RecordEntry(new AuditLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Event = "bundle_built",
            Outcome = "SUCCESS",
            SchoolCode = _state.School.Code,
            SoftwareVersion = "2.0.0",
            WindowsUser = Environment.UserName,
            Details = $"Master bundle 'school.dlmpack' built successfully ({bundleBytes.Length} bytes, {payload.Students.Count} pupils)."
        });

        return bundleBytes;
    }

    public void SaveBundleToFile(string outputPath)
    {
        byte[] bytes = GeneratedBundleBytes ?? BuildMasterBundle();
        File.WriteAllBytes(outputPath, bytes);
        StatusMessage = $"Bungkusan 'school.dlmpack' berjaya disimpan ke: {outputPath}";
        IsSuccess = true;
    }

    public void CopyScriptToClipboard()
    {
        try
        {
            Clipboard.SetText(PowerShellScript);
            StatusMessage = "Skrip PowerShell berjaya disalin ke papan keratan.";
            IsSuccess = true;
        }
        catch
        {
            StatusMessage = "Gagal menyalin ke papan keratan.";
            IsSuccess = false;
        }
    }

    public void ExportChecklistCsv(string outputPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("PC,Disediakan,Versi,Tarikh_Simpan,AppLocker_Disahkan");
        foreach (var item in LabChecklist)
        {
            sb.AppendLine($"{item.PcName},{(item.IsProvisioned ? "Ya" : "Belum")},{item.Version},{item.StoreDate},{(item.AppLockerVerified ? "Ya" : "Belum")}");
        }
        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        StatusMessage = $"Senarai semak makmal dieksport ke: {outputPath}";
        IsSuccess = true;
    }
}
