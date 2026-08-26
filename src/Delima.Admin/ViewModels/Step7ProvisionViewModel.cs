using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Delima.Admin.Models;
using Delima.Core.Audit;
using Delima.Core.Crypto;
using Delima.Core.Services;
using Delima.Core.Store;
using Delima.Import;

namespace Delima.Admin.ViewModels;

public sealed class UsbDriveItem
{
    public string RootDirectory { get; init; } = "";
    public string VolumeLabel { get; init; } = "";
    public long TotalFreeSpaceBytes { get; init; }
    public long TotalSizeBytes { get; init; }

    public string FreeSpaceDisplay => $"{TotalFreeSpaceBytes / (1024.0 * 1024 * 1024):F1} GB Bebas";
    public string DisplayName => string.IsNullOrWhiteSpace(VolumeLabel)
        ? $"Pendrive ({RootDirectory.TrimEnd('\\')}) — {FreeSpaceDisplay}"
        : $"{VolumeLabel} ({RootDirectory.TrimEnd('\\')}) — {FreeSpaceDisplay}";
}

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
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty]
    private string _busyMessage = "";

    [ObservableProperty]
    private string _errorMessage = "";

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private byte[]? _generatedBundleBytes;

    [ObservableProperty]
    private string _lastExportedDirectory = "";

    public ObservableCollection<UsbDriveItem> DetectedUsbDrives { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUsbDriveSelected))]
    private UsbDriveItem? _selectedUsbDrive;

    public bool HasUsbDriveSelected => SelectedUsbDrive != null;
    public bool HasAnyUsbDrive => DetectedUsbDrives.Count > 0;

    public ObservableCollection<LabChecklistItem> LabChecklist { get; } = [];

    public Step7ProvisionViewModel(AdminWizardState state)
    {
        _state = state;
        GenerateDefaultScript();
        InitializeLabChecklist();
        RefreshUsbDrives();
    }

    public void RefreshUsbDrives()
    {
        DetectedUsbDrives.Clear();
        try
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Removable && d.IsReady)
                .Select(d => new UsbDriveItem
                {
                    RootDirectory = d.RootDirectory.FullName,
                    VolumeLabel = string.IsNullOrWhiteSpace(d.VolumeLabel) ? "USB Drive" : d.VolumeLabel,
                    TotalFreeSpaceBytes = d.TotalFreeSpace,
                    TotalSizeBytes = d.TotalSize
                })
                .ToList();

            foreach (var d in drives)
            {
                DetectedUsbDrives.Add(d);
            }

            SelectedUsbDrive = DetectedUsbDrives.FirstOrDefault();
        }
        catch
        {
            // Ignore drive detection errors
        }

        OnPropertyChanged(nameof(HasAnyUsbDrive));
        OnPropertyChanged(nameof(HasUsbDriveSelected));
    }

    public void SaveBundleToUsb(UsbDriveItem? drive = null)
    {
        var targetDrive = drive ?? SelectedUsbDrive;
        if (targetDrive == null) return;

        string targetDir = Path.Combine(targetDrive.RootDirectory, "DELIMa_Makmal");
        Directory.CreateDirectory(targetDir);
        string outputPath = Path.Combine(targetDir, "school.dlmpack");
        SaveBundleToFile(outputPath);
        LastExportedDirectory = targetDir;
    }

    public async Task<bool> SaveBundleToUsbAsync(UsbDriveItem? drive = null)
    {
        var targetDrive = drive ?? SelectedUsbDrive;
        if (targetDrive == null) return false;

        IsBusy = true;
        BusyMessage = "Membina bungkusan dan menyalin fail ke pendrive…";
        ErrorMessage = "";
        StatusMessage = "";
        try
        {
            await Task.Run(() => SaveBundleToUsb(targetDrive));
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ralat semasa menyimpan ke pemacu USB: {ex.Message}";
            return false;
        }
        finally
        {
            IsBusy = false;
            BusyMessage = "";
        }
    }

    public async Task<bool> SaveBundleToFileAsync(string outputPath)
    {
        IsBusy = true;
        BusyMessage = "Membina bungkusan dan menyimpan fail…";
        ErrorMessage = "";
        StatusMessage = "";
        try
        {
            await Task.Run(() => SaveBundleToFile(outputPath));
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ralat semasa menyimpan fail: {ex.Message}";
            return false;
        }
        finally
        {
            IsBusy = false;
            BusyMessage = "";
        }
    }

    public async Task<bool> SaveToNetworkAsync(string networkPath)
    {
        IsBusy = true;
        BusyMessage = "Menyalin bungkusan ke laluan rangkaian…";
        ErrorMessage = "";
        StatusMessage = "";
        try
        {
            await Task.Run(() =>
            {
                if (!Directory.Exists(networkPath))
                    Directory.CreateDirectory(networkPath);
                string targetFile = Path.Combine(networkPath, "school.dlmpack");
                SaveBundleToFile(targetFile);
            });
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ralat menyimpan ke laluan rangkaian: {ex.Message}";
            return false;
        }
        finally
        {
            IsBusy = false;
            BusyMessage = "";
        }
    }

    public void OpenExportedFolder()
    {
        string target = string.IsNullOrWhiteSpace(LastExportedDirectory) ? AppDomain.CurrentDomain.BaseDirectory : LastExportedDirectory;
        if (Directory.Exists(target))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            });
        }
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
                Version = "2.1.0",
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

        // Convert classes grouped by Grade and ClassName
        var classGroups = _state.RosterStudents
            .GroupBy(s => new { s.Grade, s.ClassName })
            .OrderBy(g => g.Key.Grade)
            .ThenBy(g => g.Key.ClassName)
            .ToList();
        int colourIdx = 0;
        foreach (var group in classGroups)
        {
            var firstStudent = group.First();
            int grade = firstStudent.Grade > 0
                ? firstStudent.Grade
                : RosterImporter.NormalizeClassAndGrade(firstStudent.ClassName, null).Grade;

            payload.Classes.Add(new ClassInfo
            {
                Id = group.Key.ClassName,
                Name = group.Key.ClassName,
                Grade = grade,
                ColourIndex = colourIdx++ % Math.Max(1, _state.Theme.ClassColours.Count)
            });
        }

        // Convert students
        foreach (var student in _state.RosterStudents)
        {
            string? pwd = _state.StudentPasswords.TryGetValue(student.Id, out var p) ? p : null;
            // Resolve the DiceBear seed: legacy/blank values fall back to student.Id
            string avatarSeed = DiceBearService.ResolveSeed(
                _state.StudentAvatars.TryGetValue(student.Id, out var av) ? av : null,
                student.Id);
            string avatar = avatarSeed;

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

        string? targetDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(targetDir))
        {
            CreateTurnkeyDeploymentKit(targetDir);
        }

        StatusMessage = $"Bungkusan lengkap sedia digunakan di: {targetDir ?? outputPath}\n" +
                        "Palamkan pendrive pada PC makmal dan klik dua kali pada '1_Sediakan_Makmal.exe'.";
        IsSuccess = true;
    }

    private static void CreateTurnkeyDeploymentKit(string targetDir)
    {
        try
        {
            // 1. Locate and copy Delima.Provision.exe as 1_Sediakan_Makmal.exe
            string? provisionExe = FindFileInSurroundingDirs("Delima.Provision.exe");
            if (!string.IsNullOrEmpty(provisionExe) && File.Exists(provisionExe))
            {
                string destFriendly = Path.Combine(targetDir, "1_Sediakan_Makmal.exe");
                string destStandard = Path.Combine(targetDir, "Delima.Provision.exe");

                File.Copy(provisionExe, destFriendly, true);
                if (!File.Exists(destStandard))
                {
                    File.Copy(provisionExe, destStandard, true);
                }
            }

            // 2. Locate and copy Delima.Launcher.exe
            string? launcherExe = FindFileInSurroundingDirs("Delima.Launcher.exe");
            if (!string.IsNullOrEmpty(launcherExe) && File.Exists(launcherExe))
            {
                string destLauncher = Path.Combine(targetDir, "Delima.Launcher.exe");
                File.Copy(launcherExe, destLauncher, true);

                // Copy avatars folder if present
                string? sourceDir = Path.GetDirectoryName(launcherExe);
                if (!string.IsNullOrEmpty(sourceDir))
                {
                    string sourceAvatars = Path.Combine(sourceDir, "avatars");
                    string destAvatars = Path.Combine(targetDir, "avatars");
                    if (Directory.Exists(sourceAvatars) && !string.Equals(Path.GetFullPath(sourceAvatars), Path.GetFullPath(destAvatars), StringComparison.OrdinalIgnoreCase))
                    {
                        CopyDirectoryRecursive(sourceAvatars, destAvatars);
                    }
                }
            }

            // 3. Write friendly readme instructions
            string readmePath = Path.Combine(targetDir, "BACA_SAYA_PANDUAN_MAKMAL.txt");
            string readmeContent =
                "=====================================================================\r\n" +
                "PANDUAN PERSEDIAAN PANTAS PC MAKMAL DELIMa\r\n" +
                "=====================================================================\r\n\r\n" +
                "Langkah Penyediaan untuk Guru Penyelaras ICT:\r\n\r\n" +
                "1. Palamkan pendrive ini pada setiap PC Makmal Komputer.\r\n" +
                "2. Buka pendrive dan klik dua kali pada fail:\r\n" +
                "   -> '1_Sediakan_Makmal.exe'\r\n\r\n" +
                "3. Masukkan Kata Laluan Pentadbir yang telah anda tetapkan.\r\n" +
                "4. Klik butang besar '🚀 Sediakan Komputer Ini Sekarang'.\r\n\r\n" +
                "Selesai! Sistem akan memasang aplikasi Pelancar DELIMa,\r\n" +
                "mencipta pintasan Desktop secara automatik, dan mengunci storan murid.\r\n" +
                "=====================================================================\r\n";
            File.WriteAllText(readmePath, readmeContent, Encoding.UTF8);
        }
        catch
        {
            // Silently continue if companion binaries cannot be copied
        }
    }

    private static string? FindFileInSurroundingDirs(string filename)
    {
        List<string> candidateDirs =
        [
            AppDomain.CurrentDomain.BaseDirectory,
            Environment.CurrentDirectory,
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "publish", "Provision"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "publish", "Launcher"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Provision"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Launcher")
        ];

        foreach (var dir in candidateDirs)
        {
            if (Directory.Exists(dir))
            {
                string fullPath = Path.Combine(dir, filename);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
        }

        return null;
    }

    private static void CopyDirectoryRecursive(string sourceDir, string destinationDir)
    {
        var dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists) return;

        Directory.CreateDirectory(destinationDir);

        foreach (var file in dir.GetFiles("*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDir, file.FullName);
            string destFile = Path.Combine(destinationDir, relativePath);
            string? destSubDir = Path.GetDirectoryName(destFile);
            if (!string.IsNullOrEmpty(destSubDir) && !Directory.Exists(destSubDir))
            {
                Directory.CreateDirectory(destSubDir);
            }
            file.CopyTo(destFile, true);
        }
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
