using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Delima.Core.Store;
using Delima.Win32;
using Delima.Win32.Store;

namespace Delima.Provision;

/// <summary>
/// Result of a provisioning execution.
/// </summary>
public sealed class ProvisionResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SchoolCode { get; set; }
    public string? SchoolName { get; set; }
    public string? DeviceId { get; set; }
    public int StudentCount { get; set; }
    public DateTimeOffset StoreGeneratedAt { get; set; }
    public bool ChecklistUpdated { get; set; }
    public string? InstalledLauncherPath { get; set; }
}

/// <summary>
/// Implements the streamlined provisioning and setup workflow.
/// </summary>
public static class ProvisionEngine
{
    public const int ExitSuccess = 0;
    public const int ExitInvalidArgsOrNotFound = 1;
    public const int ExitAuthenticationFailed = 2;
    public const int ExitStoreWriteFailed = 3;

    /// <summary>
    /// Executes the provisioning workflow with optional progress reporting.
    /// </summary>
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    public static ProvisionResult Execute(
        ProvisionOptions options,
        TextReader? inReader = null,
        TextWriter? outWriter = null,
        TextWriter? errWriter = null,
        Action<int, string>? progressCallback = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        inReader ??= Console.In;
        outWriter ??= Console.Out;
        errWriter ??= Console.Error;

        if (options.ShowHelp)
        {
            outWriter.WriteLine(ProvisionOptions.GetHelpText());
            return new ProvisionResult { Success = true, ExitCode = ExitSuccess };
        }

        // ====================================================================
        // Step 1: Read school.dlmpack from USB, local path, or UNC path
        // ====================================================================
        progressCallback?.Invoke(1, "Mencari dan membaca fail bungkusan 'school.dlmpack'...");

        string? packPath = ResolvePackPath(options, inReader, outWriter);
        if (string.IsNullOrWhiteSpace(packPath) || !File.Exists(packPath))
        {
            errWriter.WriteLine($"[RALAT] Fail pakej '{packPath ?? "school.dlmpack"}' tidak dijumpai.");
            return new ProvisionResult
            {
                Success = false,
                ExitCode = ExitInvalidArgsOrNotFound,
                ErrorMessage = $"Fail bungkusan 'school.dlmpack' tidak dijumpai: {packPath ?? "school.dlmpack"}"
            };
        }

        byte[] bundleBytes;
        try
        {
            bundleBytes = File.ReadAllBytes(packPath);
        }
        catch (Exception ex)
        {
            errWriter.WriteLine($"[RALAT] Gagal membaca fail pakej '{packPath}': {ex.Message}");
            return new ProvisionResult
            {
                Success = false,
                ExitCode = ExitInvalidArgsOrNotFound,
                ErrorMessage = $"Gagal membaca fail pakej '{packPath}': {ex.Message}"
            };
        }

        SecurePasswordBuffer? passphraseBuffer = null;
        try
        {
            // ================================================================
            // Step 2: Prompt / get admin passphrase (memory only, zeroed on exit)
            // ================================================================
            progressCallback?.Invoke(2, "Mengesahkan kata laluan pentadbir...");

            if (!string.IsNullOrEmpty(options.Passphrase))
            {
                passphraseBuffer = new SecurePasswordBuffer(options.Passphrase);
            }
            else if (options.PassphraseStdin)
            {
                string? line = inReader.ReadLine();
                if (line == null)
                {
                    errWriter.WriteLine("[RALAT] Tiada kata laluan dibekalkan melalui stdin.");
                    return new ProvisionResult
                    {
                        Success = false,
                        ExitCode = ExitInvalidArgsOrNotFound,
                        ErrorMessage = "Tiada kata laluan dibekalkan melalui stdin."
                    };
                }

                string clean = line.TrimEnd('\r', '\n');
                if (clean.Length == 0)
                {
                    errWriter.WriteLine("[RALAT] Kata laluan dari stdin adalah kosong.");
                    return new ProvisionResult
                    {
                        Success = false,
                        ExitCode = ExitInvalidArgsOrNotFound,
                        ErrorMessage = "Kata laluan dari stdin adalah kosong."
                    };
                }

                passphraseBuffer = new SecurePasswordBuffer(clean);
            }
            else
            {
                if (options.Quiet)
                {
                    errWriter.WriteLine("[RALAT] Mod senyap (--quiet) memerlukan --passphrase-stdin atau pilihan Passphrase.");
                    return new ProvisionResult
                    {
                        Success = false,
                        ExitCode = ExitInvalidArgsOrNotFound,
                        ErrorMessage = "Mod senyap memerlukan kata laluan dibekalkan."
                    };
                }

                passphraseBuffer = ReadMaskedPassphrase(outWriter);
                if (passphraseBuffer.PasswordSpan.Length == 0)
                {
                    errWriter.WriteLine("[RALAT] Kata laluan tidak boleh kosong.");
                    return new ProvisionResult
                    {
                        Success = false,
                        ExitCode = ExitInvalidArgsOrNotFound,
                        ErrorMessage = "Kata laluan tidak boleh kosong."
                    };
                }
            }

            // ================================================================
            // Step 3: Argon2id -> decrypt -> validate HMAC and schema version
            // ================================================================
            if (!options.Quiet)
            {
                outWriter.WriteLine("[1/5] Menyahsulit dan mengesahkan pakej induk (Argon2id + AES-256-GCM)...");
            }
            progressCallback?.Invoke(3, "Menyahsulit dan mengesahkan data sekolah (AES-256-GCM)...");

            MasterBundlePayload payload;
            try
            {
                payload = MasterBundle.Unpack(bundleBytes, passphraseBuffer.PasswordSpan);
            }
            catch (MasterBundleException ex)
            {
                errWriter.WriteLine($"[RALAT PENGESAHAN] Kata laluan salah atau fail pakej rosak: {ex.Message}");
                return new ProvisionResult
                {
                    Success = false,
                    ExitCode = ExitAuthenticationFailed,
                    ErrorMessage = "Kata laluan pentadbir tidak sah atau fail pakej rosak."
                };
            }
            catch (Exception ex)
            {
                errWriter.WriteLine($"[RALAT] Gagal menyahsulit pakej: {ex.Message}");
                return new ProvisionResult
                {
                    Success = false,
                    ExitCode = ExitAuthenticationFailed,
                    ErrorMessage = $"Gagal menyahsulit pakej: {ex.Message}"
                };
            }

            if (string.IsNullOrWhiteSpace(payload.School?.Code))
            {
                errWriter.WriteLine("[RALAT] Pakej tidak mengandungi kod sekolah yang sah.");
                return new ProvisionResult
                {
                    Success = false,
                    ExitCode = ExitAuthenticationFailed,
                    ErrorMessage = "Pakej tidak mengandungi kod sekolah yang sah."
                };
            }

            // Override preferred browser if explicitly selected by the administrator during setup
            if (!string.IsNullOrWhiteSpace(options.PreferredBrowser))
            {
                payload.Config.PreferredBrowser = options.PreferredBrowser.ToLowerInvariant();
            }

            string targetDir = options.TargetDirectory ?? DpapiCredentialStore.GetDefaultStoreDirectory();

            // ================================================================
            // Step 4: Re-wrap with DPAPI LocalMachine + entropy -> write credentials.dat
            // ================================================================
            if (!options.Quiet)
            {
                outWriter.WriteLine($"[2/5] Menulis storan per-PC DPAPI (LocalMachine) untuk sekolah {payload.School.Code} ({payload.Students.Count} murid)...");
            }
            progressCallback?.Invoke(4, $"Menyimpan storan selamat DPAPI untuk {payload.Students.Count} orang murid...");

            if (!options.DryRun)
            {
                try
                {
                    DpapiCredentialStore.WriteStore(payload, targetDir, pupilAccount: options.PupilAccount, applyAcls: options.ApplyAcls);
                }
                catch (Exception ex)
                {
                    errWriter.WriteLine($"[RALAT STORAN] Gagal menulis storan DPAPI: {ex.Message}");
                    return new ProvisionResult
                    {
                        Success = false,
                        ExitCode = ExitStoreWriteFailed,
                        ErrorMessage = $"Gagal menulis storan DPAPI: {ex.Message}"
                    };
                }
            }

            // ================================================================
            // Step 5: Write device_id (GUID, first run only) and the store date
            // ================================================================
            if (!options.Quiet)
            {
                outWriter.WriteLine("[3/5] Menguruskan ID peranti dan metadata storan...");
            }

            string deviceId;
            try
            {
                deviceId = ManageDeviceId(targetDir, options.PupilAccount, options.DryRun, options.ApplyAcls);
                if (!options.DryRun)
                {
                    WriteProvisionMetadata(targetDir, deviceId, payload, options.PupilAccount, options.ApplyAcls);
                }
            }
            catch (Exception ex)
            {
                errWriter.WriteLine($"[RALAT STORAN] Gagal menguruskan metadata storan: {ex.Message}");
                return new ProvisionResult
                {
                    Success = false,
                    ExitCode = ExitStoreWriteFailed,
                    ErrorMessage = $"Gagal menguruskan metadata peranti: {ex.Message}"
                };
            }

            // ================================================================
            // Step 6: Install Delima.Launcher & Create Desktop Shortcuts / Policies
            // ================================================================
            progressCallback?.Invoke(5, "Memasang Pelancar DELIMa & menyediakan pintasan Desktop...");

            string? installedLauncherPath = null;
            if (!options.DryRun)
            {
                try
                {
                    installedLauncherPath = SetupLauncherAndShortcuts(options, packPath, outWriter);
                }
                catch (Exception ex)
                {
                    outWriter.WriteLine($"[AMARAN] Ralat semasa persediaan pintasan/aplikasi: {ex.Message}");
                }
            }

            // ================================================================
            // Step 7: Append to the lab checklist file on the share, if present
            // ================================================================
            if (!options.Quiet)
            {
                outWriter.WriteLine("[4/5] Mengemas kini senarai semak makmal (lab checklist)...");
            }

            bool checklistUpdated = UpdateLabChecklist(options, packPath, deviceId, payload.School.Code, payload.GeneratedAt, outWriter, errWriter);

            // ================================================================
            // Step 8: Complete and clean up
            // ================================================================
            if (!options.Quiet)
            {
                outWriter.WriteLine("[5/5] Selesai membersihkan memori.");
                outWriter.WriteLine();
                outWriter.WriteLine($"[BERJAYA] Komputer ini berjaya diprovisikan untuk {payload.School.Name} ({payload.School.Code}).");
                outWriter.WriteLine($"  - Device ID       : {deviceId}");
                outWriter.WriteLine($"  - Bilangan Murid  : {payload.Students.Count}");
                outWriter.WriteLine($"  - Tarikh Storan   : {payload.GeneratedAt:yyyy-MM-dd HH:mm:ss zzz}");
                outWriter.WriteLine($"  - Lokasi Storan   : {targetDir}");
                if (!string.IsNullOrEmpty(installedLauncherPath))
                {
                    outWriter.WriteLine($"  - Lokasi Pelancar : {installedLauncherPath}");
                }
            }

            progressCallback?.Invoke(6, "Persediaan berjaya diselesaikan!");

            return new ProvisionResult
            {
                Success = true,
                ExitCode = ExitSuccess,
                SchoolCode = payload.School.Code,
                SchoolName = payload.School.Name,
                DeviceId = deviceId,
                StudentCount = payload.Students.Count,
                StoreGeneratedAt = payload.GeneratedAt,
                ChecklistUpdated = checklistUpdated,
                InstalledLauncherPath = installedLauncherPath
            };
        }
        finally
        {
            // Decryption discipline: wipe all sensitive buffers from memory
            passphraseBuffer?.Dispose();
            CryptographicOperations.ZeroMemory(bundleBytes);
        }
    }

    /// <summary>
    /// Installs the Launcher executable and creates Desktop / Start Menu shortcuts.
    /// </summary>
    public static string? SetupLauncherAndShortcuts(ProvisionOptions options, string? packPath, TextWriter outWriter)
    {
        string defaultProgramFiles = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "DELIMa Launcher");

        string installDir = options.InstallDestinationPath ?? defaultProgramFiles;
        string targetExePath = Path.Combine(installDir, "Delima.Launcher.exe");

        // 1. Locate source Delima.Launcher.exe
        string? sourceExe = ResolveLauncherSourceExe(options, packPath);

        if (options.InstallLauncher && !string.IsNullOrEmpty(sourceExe) && File.Exists(sourceExe))
        {
            try
            {
                if (!Directory.Exists(installDir))
                {
                    Directory.CreateDirectory(installDir);
                }

                // Copy executable if source and target are different
                if (!string.Equals(Path.GetFullPath(sourceExe), Path.GetFullPath(targetExePath), StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(sourceExe, targetExePath, overwrite: true);
                }

                // Copy avatars folder if exists in source directory
                string? sourceDir = Path.GetDirectoryName(sourceExe);
                if (!string.IsNullOrEmpty(sourceDir))
                {
                    string sourceAvatars = Path.Combine(sourceDir, "avatars");
                    string destAvatars = Path.Combine(installDir, "avatars");

                    if (Directory.Exists(sourceAvatars) && !string.Equals(Path.GetFullPath(sourceAvatars), Path.GetFullPath(destAvatars), StringComparison.OrdinalIgnoreCase))
                    {
                        CopyDirectory(sourceAvatars, destAvatars);
                    }
                }
            }
            catch (Exception ex)
            {
                outWriter.WriteLine($"[AMARAN] Gagal menyalin fail aplikasi pelancar: {ex.Message}");
            }
        }

        // Determine the actual executable path to point shortcuts to
        string executableToLink = File.Exists(targetExePath)
            ? targetExePath
            : (sourceExe ?? targetExePath);

        if (File.Exists(executableToLink))
        {
            // 2. Create Desktop Shortcut
            if (options.CreateDesktopShortcut)
            {
                try
                {
                    ShortcutHelper.CreateDesktopShortcut(
                        targetPath: executableToLink,
                        shortcutName: "DELIMa Smart Launcher",
                        publicDesktop: true,
                        arguments: options.EnableKioskStartup ? "--kiosk" : null);

                    ShortcutHelper.CreateStartMenuShortcut(
                        targetPath: executableToLink,
                        shortcutName: "DELIMa Smart Launcher",
                        subFolder: "DELIMa Launcher",
                        publicStartMenu: true);
                }
                catch (Exception ex)
                {
                    outWriter.WriteLine($"[AMARAN] Gagal mencipta pintasan Desktop: {ex.Message}");
                }
            }

            // 3. Configure Kiosk Startup
            if (options.EnableKioskStartup)
            {
                try
                {
                    LaunchAtLogonConfigurator.Enable(
                        executablePath: executableToLink,
                        arguments: "--kiosk",
                        machineWide: true);
                }
                catch (Exception ex)
                {
                    outWriter.WriteLine($"[AMARAN] Gagal mendaftarkan Mod Kiosk semasa log masuk: {ex.Message}");
                }
            }
        }

        // 4. Configure Browser Policies
        if (options.ApplyBrowserPolicies)
        {
            try
            {
                BrowserPolicyConfigurator.ApplyPolicies(BrowserKind.Chrome);
                BrowserPolicyConfigurator.ApplyPolicies(BrowserKind.Edge);
            }
            catch (Exception ex)
            {
                outWriter.WriteLine($"[AMARAN] Gagal menetapkan dasar pelayar: {ex.Message}");
            }
        }

        return File.Exists(executableToLink) ? executableToLink : null;
    }

    private static string? ResolveLauncherSourceExe(ProvisionOptions options, string? packPath)
    {
        if (!string.IsNullOrWhiteSpace(options.LauncherSourcePath) && File.Exists(options.LauncherSourcePath))
        {
            return Path.GetFullPath(options.LauncherSourcePath);
        }

        List<string> candidateDirs =
        [
            AppDomain.CurrentDomain.BaseDirectory,
            Environment.CurrentDirectory,
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Launcher"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "publish", "Launcher")
        ];

        if (!string.IsNullOrWhiteSpace(packPath))
        {
            string? packDir = Path.GetDirectoryName(packPath);
            if (!string.IsNullOrEmpty(packDir) && !candidateDirs.Contains(packDir))
            {
                candidateDirs.Insert(0, packDir);
                candidateDirs.Insert(1, Path.Combine(packDir, "Launcher"));
            }
        }

        foreach (var dir in candidateDirs)
        {
            if (Directory.Exists(dir))
            {
                string candidate = Path.Combine(dir, "Delima.Launcher.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
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

    public static string? ResolvePackPath(ProvisionOptions options, TextReader? inReader = null, TextWriter? outWriter = null)
    {
        if (!string.IsNullOrWhiteSpace(options.PackPath))
        {
            return Path.GetFullPath(options.PackPath);
        }

        // Check current directory
        string localDlm = Path.Combine(Environment.CurrentDirectory, "school.dlmpack");
        if (File.Exists(localDlm))
        {
            return localDlm;
        }

        // Check app directory
        string appDlm = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "school.dlmpack");
        if (File.Exists(appDlm))
        {
            return appDlm;
        }

        // Search for any *.dlmpack in current directory or base directory
        try
        {
            var files = Directory.GetFiles(Environment.CurrentDirectory, "*.dlmpack");
            if (files.Length == 1)
            {
                return files[0];
            }

            var appFiles = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.dlmpack");
            if (appFiles.Length == 1)
            {
                return appFiles[0];
            }

            // Search attached removable USB drives
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady && drive.DriveType == DriveType.Removable)
                {
                    string usbDlm = Path.Combine(drive.RootDirectory.FullName, "school.dlmpack");
                    if (File.Exists(usbDlm))
                    {
                        return usbDlm;
                    }

                    var usbFiles = Directory.GetFiles(drive.RootDirectory.FullName, "*.dlmpack");
                    if (usbFiles.Length == 1)
                    {
                        return usbFiles[0];
                    }
                }
            }
        }
        catch
        {
            // Fall through
        }

        // If interactive console mode, prompt user for path
        if (!options.Quiet && inReader != null && outWriter != null)
        {
            outWriter.Write("Masukkan laluan fail school.dlmpack: ");
            string? input = inReader.ReadLine()?.Trim('\"', ' ');
            if (!string.IsNullOrWhiteSpace(input))
            {
                return Path.GetFullPath(input);
            }
        }

        return null;
    }

    private static SecurePasswordBuffer ReadMaskedPassphrase(TextWriter outWriter)
    {
        outWriter.Write("Sila masukkan kata laluan pentadbir: ");

        char[] buffer = new char[256];
        int length = 0;

        while (true)
        {
            ConsoleKeyInfo keyInfo = Console.ReadKey(intercept: true);

            if (keyInfo.Key == ConsoleKey.Enter)
            {
                outWriter.WriteLine();
                break;
            }
            else if (keyInfo.Key == ConsoleKey.Backspace)
            {
                if (length > 0)
                {
                    length--;
                    buffer[length] = '\0';
                    outWriter.Write("\b \b");
                }
            }
            else if (!char.IsControl(keyInfo.KeyChar))
            {
                if (length < buffer.Length)
                {
                    buffer[length++] = keyInfo.KeyChar;
                    outWriter.Write('*');
                }
            }
        }

        var secureBuffer = new SecurePasswordBuffer(buffer.AsSpan(0, length));
        Array.Clear(buffer, 0, buffer.Length);
        return secureBuffer;
    }

    private static string ManageDeviceId(string targetDir, string pupilAccount, bool dryRun, bool applyAcls)
    {
        string deviceIdPath = Path.Combine(targetDir, "device.id");
        string legacyDeviceIdPath = Path.Combine(targetDir, "device_id");
        string deviceId;

        if (File.Exists(deviceIdPath))
        {
            deviceId = File.ReadAllText(deviceIdPath).Trim();
        }
        else if (File.Exists(legacyDeviceIdPath))
        {
            deviceId = File.ReadAllText(legacyDeviceIdPath).Trim();
        }
        else
        {
            deviceId = Guid.NewGuid().ToString("D").ToUpperInvariant();
            if (!dryRun)
            {
                File.WriteAllText(deviceIdPath, deviceId, Encoding.UTF8);

                if (applyAcls)
                {
                    try
                    {
                        StoreAclConfigurator.ApplyStoreFileAcls(deviceIdPath, pupilAccount);
                    }
                    catch
                    {
                        // Ignore ACL failure on device_id if already protected
                    }
                }
            }
        }

        return deviceId;
    }

    private static void WriteProvisionMetadata(string targetDir, string deviceId, MasterBundlePayload payload, string pupilAccount, bool applyAcls)
    {
        string metaPath = Path.Combine(targetDir, "provision.json");
        var meta = new
        {
            schema_version = payload.SchemaVersion,
            device_id = deviceId,
            school_code = payload.School?.Code,
            school_name = payload.School?.Name,
            student_count = payload.Students?.Count ?? 0,
            provisioned_at = DateTimeOffset.UtcNow,
            bundle_generated_at = payload.GeneratedAt,
            software_version = "2.0.0"
        };

        string json = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(metaPath, json, Encoding.UTF8);

        if (applyAcls)
        {
            try
            {
                StoreAclConfigurator.ApplyStoreFileAcls(metaPath, pupilAccount);
            }
            catch
            {
                // Fall through
            }
        }
    }

    private static bool UpdateLabChecklist(
        ProvisionOptions options,
        string packPath,
        string deviceId,
        string schoolCode,
        DateTimeOffset storeDate,
        TextWriter outWriter,
        TextWriter errWriter)
    {
        string? checklistPath = options.ChecklistPath;

        if (string.IsNullOrWhiteSpace(checklistPath))
        {
            string? packDir = Path.GetDirectoryName(packPath);
            if (!string.IsNullOrWhiteSpace(packDir))
            {
                string candidate = Path.Combine(packDir, "lab_checklist.csv");
                if (File.Exists(candidate))
                {
                    checklistPath = candidate;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(checklistPath))
        {
            return false;
        }

        try
        {
            const string header = "Timestamp,PC_Name,Device_ID,School_Code,Software_Version,Store_Date,Status";
            string machineName = Environment.MachineName;
            string newEntry = $"{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss},{machineName},{deviceId},{schoolCode},2.0.0,{storeDate:yyyy-MM-dd HH:mm:ss},SUCCESS";

            bool fileExists = File.Exists(checklistPath);
            bool needHeader = !fileExists || new FileInfo(checklistPath).Length == 0;

            using (var sw = new StreamWriter(checklistPath, append: true, Encoding.UTF8))
            {
                if (needHeader)
                {
                    sw.WriteLine(header);
                }
                sw.WriteLine(newEntry);
            }

            outWriter.WriteLine($"  - Senarai Semak   : Dikemas kini ({checklistPath})");
            return true;
        }
        catch (Exception ex)
        {
            errWriter.WriteLine($"[AMARAN] Gagal mengemas kini senarai semak makmal: {ex.Message}");
            return false;
        }
    }
}
